using Pibbles.Diagnostics;
using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Compiler;

/// <summary>
/// Lowers an error-free <see cref="Compilation"/> to a <see cref="Story"/>, reading names and types from the
/// compilation's <see cref="Bindings"/> and never resolving anything itself. A missing binding is a bug in the
/// binder or here, not bad input, so it throws <see cref="InvalidOperationException"/>.
/// </summary>
internal sealed class StoryLowerer(Compilation compilation)
{
    private readonly Bindings bindings = compilation.Bindings;
    private readonly SymbolTable symbols = compilation.Symbols;
    private readonly Dictionary<string, CompiledNode> nodes = [];
    private readonly Dictionary<string, Template> templates = [];
    private readonly Dictionary<string, IdSite> sites = [];
    private readonly HashSet<string> fallbackIds = [];
    private SyntaxTree tree = null!;
    private string nodeName = "";
    private List<Instruction> code = [];

    public static Story Lower(Compilation compilation) => new StoryLowerer(compilation).Lower();

    private Story Lower()
    {
        foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
        {
            tree = syntaxTree;
            foreach (NodeSyntax node in syntaxTree.Root.Nodes)
                LowerNode(node);
        }

        StoryVariable[] variables = [.. symbols.Variables.Values.Select(LowerVariable)];
        return new(
            nodes,
            symbols.Aliases.ToDictionary(alias => alias.Key, alias => alias.Value.Name),
            variables,
            templates,
            sites,
            fallbackIds,
            symbols.Actors,
            symbols.Tags,
            symbols.Enums,
            symbols.Functions.Where(function => function.Value.Location is not null).ToDictionary());
    }

    private StoryVariable LowerVariable(VariableSymbol variable)
    {
        nodeName = $"${variable.Name}";
        return new(variable, LowerIn(variable, variable.StartingValue));
    }

    private void LowerNode(NodeSyntax node)
    {
        nodeName = SymbolOf<NodeSymbol>(node.Name).Name;
        code = [];
        LowerBlock(node.Body);
        code.Add(new ReturnInstruction());
        nodes[nodeName] = new(nodeName, [.. code], LocationOf(node.Name));
    }

    private void LowerBlock(IEnumerable<StatementSyntax> block)
    {
        foreach (StatementSyntax statement in block)
            LowerStatement(statement);
    }

    private void LowerStatement(StatementSyntax statement)
    {
        switch (statement)
        {
            case TextLineSyntax line:
                LowerTextLine(line);
                break;

            case ChoiceSyntax choice:
                LowerChoice(choice);
                break;

            case IfStatementSyntax @if:
                LowerIf(@if);
                break;

            case SetStatementSyntax set:
                code.Add(LowerSet(set));
                break;

            case WaitStatementSyntax wait:
                code.Add(new WaitInstruction(LowerExpression(wait.Duration), LocationOf(wait)));
                break;

            case CommandStatementSyntax command:
                CommandSymbol symbol = SymbolOf<CommandSymbol>(command.Command);
                code.Add(new CommandInstruction(symbol, Arrange(symbol.Parameters, command.Arguments.Select(argument => argument.Value), command), Waits(symbol, command.Wait), LocationOf(command)));
                break;

            case JumpStatementSyntax jump:
                code.Add(new JumpInstruction(SymbolOf<NodeSymbol>(jump.Target).Name));
                break;

            case CallStatementSyntax call:
                code.Add(new CallInstruction(SymbolOf<NodeSymbol>(call.Target).Name, Identify(call.Tags, call.Span, code.Count)));
                break;

            case ReturnStatementSyntax:
                code.Add(new ReturnInstruction());
                break;

            case EndStatementSyntax:
                code.Add(new EndInstruction());
                break;

            case VariationStatementSyntax variation:
                LowerVariation(variation.Kind is VariationKind.Sequence ? BlockKind.Sequence : BlockKind.Cycle, variation.Tags, variation.Span, variation.Alternatives.Select(alternative => alternative.Body));
                break;

            case OnceStatementSyntax once:
                LowerVariation(BlockKind.Once, once.Tags, once.Span, [once.Body]);
                break;

            default:
                throw Missing(statement, "a lowerable statement");
        }
    }

    private void LowerTextLine(TextLineSyntax line)
    {
        ActorSymbol? speaker = line.Speaker is { } name ? SymbolOf<ActorSymbol>(name) : null;
        if (line.Pose is { } pose)
            code.Add(new PoseInstruction(speaker ?? throw Missing(line, "a speaker for the pose"), SymbolOf<PoseSymbol>(pose)));

        if (line.Content.Count == 0)
            return;

        string id = Identify(line.Tags, line.Span, code.Count);
        templates[id] = new(speaker, LowerTags(line.Tags), LowerInline(line.Content));
        code.Add(new LineInstruction(id));
    }

    private void LowerChoice(ChoiceSyntax choice)
    {
        int at = code.Count;
        code.Add(new ReturnInstruction());

        List<(string Id, Template Text, Expr? Condition, bool IsOnce, int Body)> options = [];
        List<int> branches = [];
        foreach (OptionSyntax option in choice.Options)
        {
            string id = Identify(option.Tags, option.Span, at);
            var text = new Template(null, LowerTags(option.Tags), LowerInline(option.Text));
            templates[id] = text;
            options.Add((id, text, option.Condition is { } condition ? LowerExpression(condition) : null, option.IsOnce, option.Body.Count == 0 ? -1 : code.Count));
            if (option.Body.Count > 0)
                LowerBody(option.Body, branches);
        }

        int join = code.Count;
        Patch(branches, join);
        code[at] = new ChoiceInstruction([.. options.Select(option => new CompiledOption(option.Id, option.Text, option.Condition, option.IsOnce, option.Body < 0 ? join : option.Body))], join);
    }

    private void LowerIf(IfStatementSyntax @if)
    {
        List<int> branches = [];
        (ExpressionSyntax Condition, IReadOnlyList<StatementSyntax> Body)[] clauses =
            [(@if.Condition, @if.Body), .. @if.ElseIfs.Select(elseIf => (elseIf.Condition, elseIf.Body))];
        foreach ((ExpressionSyntax condition, IReadOnlyList<StatementSyntax> body) in clauses)
        {
            int test = code.Count;
            code.Add(new ReturnInstruction());
            LowerBody(body, branches);
            code[test] = new BranchIfFalseInstruction(LowerExpression(condition), code.Count);
        }

        LowerBlock(@if.Else?.Body ?? []);
        Patch(branches, code.Count);
    }

    private SetInstruction LowerSet(SetStatementSyntax set)
    {
        VariableSymbol variable = SymbolOf<VariableSymbol>(set.Variable);
        Expr value = LowerExpression(set.Value);
        BinaryOperator? @operator = set.Operator switch
        {
            AssignmentOperator.Add => BinaryOperator.Add,
            AssignmentOperator.Subtract => BinaryOperator.Subtract,
            _ => null,
        };

        return new(variable, @operator is { } op ? new BinaryExpr(op, new VariableExpr(variable), value, variable.Type, LocationOf(set)) : value);
    }

    private void LowerVariation(BlockKind kind, IReadOnlyList<TagSyntax> tags, TextSpan span, IEnumerable<IReadOnlyList<StatementSyntax>> alternatives)
    {
        int at = code.Count;
        code.Add(new ReturnInstruction());
        string id = Identify(tags, span, at);

        List<int> starts = [];
        List<int> branches = [];
        foreach (IReadOnlyList<StatementSyntax> body in alternatives)
        {
            starts.Add(code.Count);
            LowerBody(body, branches);
        }

        int exit = code.Count;
        Patch(branches, exit);
        code[at] = new VariationInstruction(kind, id, starts, exit);
    }

    /// <summary>Lowers a body, then a branch to wherever flow continues, unless the body always leaves. The branch is patched once that place is known.</summary>
    private void LowerBody(IReadOnlyList<StatementSyntax> body, List<int> branches)
    {
        LowerBlock(body);
        if (SyntaxWalk.AlwaysLeaves(body))
            return;

        branches.Add(code.Count);
        code.Add(new BranchInstruction(-1));
    }

    private void Patch(IEnumerable<int> branches, int target)
    {
        foreach (int branch in branches)
            code[branch] = new BranchInstruction(target);
    }

    private string Identify(IEnumerable<TagSyntax> tags, TextSpan span, int index)
    {
        string id;
        if (tags.FirstOrDefault(tag => tag.Name is "id")?.Value is { Length: > 0 } written)
        {
            id = written;
        }
        else
        {
            id = $"~{tree.Source.Path}:{tree.Source.GetLinePosition(span.Start).Line + 1}";
            fallbackIds.Add(id);
        }

        if (!sites.TryAdd(id, new(nodeName, index)))
            throw new InvalidOperationException($"Can't lower `{nodeName}`: the ID `{id}` is already used by {sites[id].Node}[{sites[id].Index}]. Two sources probably share the path `{tree.Source.Path}`.");

        return id;
    }

    private TemplateTag[] LowerTags(IEnumerable<TagSyntax> tags) =>
        [.. tags.Select(tag => new TemplateTag(tag.Name, tag.Value, bindings.TagValueOf(tag) as EnumMemberSymbol))];

    private TemplateElement[] LowerInline(IEnumerable<InlineSyntax> content) => [.. content.Select(LowerElement)];

    private TemplateElement LowerElement(InlineSyntax item) => item switch
    {
        TextRunSyntax run => new TextElement(run.Text),
        MarkupSyntax markup => LowerMarkup(markup),
        InterpolationSyntax interpolation => new InterpolationElement(LowerExpression(interpolation.Value)),
        InlineCommandSyntax command => LowerInlineCommand(command),
        PauseSyntax { Duration: null } => new InputWaitElement(),
        PauseSyntax { Duration: { } duration } => new PauseElement(LowerExpression(duration), LocationOf(item)),
        SpeedSyntax { Factor: null } => new SpeedResetElement(),
        SpeedSyntax { Factor: { } factor } => new SpeedElement(LowerExpression(factor), LocationOf(item)),
        PageBreakSyntax => new PageBreakElement(),
        LineBreakSyntax => new LineBreakElement(),
        IconSyntax icon => new IconElement(SymbolOf<IconSymbol>(icon.Name)),
        ConditionalTextSyntax conditional => LowerConditional(conditional),
        _ => throw Missing(item, "a lowerable piece of text"),
    };

    private MarkupElement LowerMarkup(MarkupSyntax markup)
    {
        MarkupSymbol symbol = SymbolOf<MarkupSymbol>(markup.Name);
        return new(symbol, Arrange(symbol.Parameters, markup.Arguments.Select(argument => argument.Value), markup), LowerInline(markup.Content), LocationOf(markup));
    }

    private CommandElement LowerInlineCommand(InlineCommandSyntax command)
    {
        CommandSymbol symbol = SymbolOf<CommandSymbol>(command.Command);
        return new(symbol, Arrange(symbol.Parameters, command.Arguments.Select(argument => argument.Value), command), Waits(symbol, command.Wait), LocationOf(command));
    }

    private ConditionalElement LowerConditional(ConditionalTextSyntax conditional) => new(
        [
            new(LowerExpression(conditional.Condition), LowerInline(conditional.Content)),
            .. conditional.ElseIfs.Select(elseIf => new ConditionalBranch(LowerExpression(elseIf.Condition), LowerInline(elseIf.Content))),
        ],
        conditional.Else is { } @else ? LowerInline(@else.Content) : null);

    private static bool Waits(CommandSymbol command, CommandWait wait) => wait switch
    {
        CommandWait.Wait => true,
        CommandWait.NoWait => false,
        _ => command.Waits,
    };

    /// <summary>Lowers arguments in the order of <paramref name="parameters"/>, filling each parameter nobody gave an argument for from its default.</summary>
    private Expr[] Arrange(IReadOnlyList<ParameterSymbol> parameters, IEnumerable<ExpressionSyntax> arguments, SyntaxNode owner)
    {
        ExpressionSyntax[] given = [.. arguments];
        return
        [
            .. parameters.Select(parameter =>
                given.FirstOrDefault(argument => bindings.ParameterOf(argument) == parameter) is { } argument
                    ? LowerExpression(argument)
                    : parameter.Default is { } value ? LowerIn(parameter, value) : throw Missing(owner, $"an argument for `{parameter.Name}`")),
        ];
    }

    /// <summary>Lowers an expression written in a declaration, such as a default, which may be in another file than the code using it.</summary>
    private Expr LowerIn(Symbol declaration, ExpressionSyntax expression)
    {
        SyntaxTree used = tree;
        tree = compilation.SyntaxTrees.FirstOrDefault(candidate => candidate.Source.Path == declaration.Location?.Path) ?? used;
        try
        {
            return LowerExpression(expression);
        }
        finally
        {
            tree = used;
        }
    }

    private Expr LowerExpression(ExpressionSyntax expression)
    {
        ExpressionType type = bindings.TypeOf(expression) ?? throw Missing(expression, "a type");
        Expr lowered = expression switch
        {
            NumberLiteralSyntax number => new NumberExpr(number.Value),
            DurationLiteralSyntax duration => new DurationExpr(duration.Seconds),
            StringLiteralSyntax @string => new StringExpr(@string.Value),
            BooleanLiteralSyntax boolean => new BoolExpr(boolean.Value),
            VariableExpressionSyntax variable => new VariableExpr(SymbolOf<VariableSymbol>(variable)),
            NameExpressionSyntax name => LowerName(name, type.Type),
            CallExpressionSyntax call => LowerCall(call),
            ParenthesizedExpressionSyntax parenthesized => LowerExpression(parenthesized.Expression),
            UnaryExpressionSyntax unary => new UnaryExpr(unary.Operator, LowerExpression(unary.Operand), type.Type),
            BinaryExpressionSyntax binary => new BinaryExpr(binary.Operator, LowerExpression(binary.Left), LowerExpression(binary.Right), type.Type, LocationOf(binary)),
            _ => throw Missing(expression, "a lowerable expression"),
        };

        return type.ConvertedType == type.Type ? lowered : new ToDurationExpr(lowered);
    }

    private Expr LowerName(NameExpressionSyntax name, TypeSymbol type) => bindings.SymbolOf(name) switch
    {
        EnumMemberSymbol member => new EnumMemberExpr(member, type),
        ActorSymbol actor => new ActorExpr(actor),
        NodeSymbol node => new NodeExpr(node.Name),
        _ => throw Missing(name, "an enum member, actor or node"),
    };

    private Expr LowerCall(CallExpressionSyntax call)
    {
        FunctionSymbol function = SymbolOf<FunctionSymbol>(call.Function);
        Expr[] arguments = Arrange(function.Parameters, call.Arguments, call);
        return function.Location is null && function.Name is "visits" ? new VisitsExpr(arguments[0]) : new CallExpr(function, arguments, LocationOf(call));
    }

    private T SymbolOf<T>(SyntaxNode name)
        where T : Symbol => bindings.SymbolOf(name) as T ?? throw Missing(name, $"a {typeof(T).Name}");

    private SourceLocation LocationOf(SyntaxNode node) => tree.Source.GetLocation(node.Span);

    private InvalidOperationException Missing(SyntaxNode node, string what)
    {
        SourceLocation location = LocationOf(node);
        return new($"Can't lower `{nodeName}`: nothing was bound for {what} at {location.Path}:{location.Start.Line + 1}.");
    }
}
