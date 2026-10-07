using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Resolves the names in a story against its <see cref="SymbolTable"/>: speakers and poses, commands, markup, icons,
/// tags, functions, variables and nodes. It checks every argument against its parameter, and the type of every
/// expression: in variables' values, parameters' defaults, conditions, arguments, <c>@set</c>, <c>@wait</c> and text.
/// </summary>
/// <remarks>
/// An expression is bound with the type expected where it appears, which is how a bare name is read: as a member of the
/// expected enum, an actor or a node. Anything whose type can't be known gets <see cref="TypeSymbol.Error"/>, which
/// converts to and from every type, so each problem is reported once.
/// </remarks>
internal sealed class Binder(SymbolTable symbols, List<Diagnostic> diagnostics, ReferenceIndex references, Bindings bindings) : AnalysisPass(diagnostics, references)
{
    public static void Run(IReadOnlyList<SyntaxTree> trees, SymbolTable symbols, List<Diagnostic> diagnostics, ReferenceIndex references, Bindings bindings)
    {
        var binder = new Binder(symbols, diagnostics, references, bindings);
        foreach (SyntaxTree tree in trees)
        {
            binder.Tree = tree;
            binder.BindDeclarations(tree.Root.Declarations);
            foreach (NodeSyntax node in tree.Root.Nodes)
                binder.BindStatements(node.Body);
        }
    }

    /// <summary>Records that <paramref name="name"/> refers to <paramref name="symbol"/>, in the reference index and in the bindings.</summary>
    private void Refers(SyntaxNode name, Symbol symbol) => Refers(name, name.Span, symbol);

    /// <summary>Records that <paramref name="name"/>, whose reference is the text at <paramref name="span"/>, refers to <paramref name="symbol"/>.</summary>
    private void Refers(SyntaxNode name, TextSpan span, Symbol symbol)
    {
        Refers(span, symbol);
        bindings.BindName(name, symbol);
    }

    private void BindDeclarations(IEnumerable<DeclarationSyntax> declarations)
    {
        foreach (DeclarationSyntax declaration in declarations)
        {
            if (declaration is VariableDeclarationSyntax variable)
            {
                TypeSymbol type = variable.Type is { } written ? TypeNamed(written) : DeclarationPass.TypeOfStartingValue(variable.Value);
                BindValue(variable.Value, type, $"`${variable.Variable.Name}` holds {type.Describe()}");
            }

            IReadOnlyList<ParameterSyntax> parameters = declaration switch
            {
                CommandDeclarationSyntax command => command.Parameters,
                MarkupDeclarationSyntax markup => markup.Parameters,
                FunctionDeclarationSyntax function => function.Parameters,
                _ => [],
            };

            foreach (ParameterSyntax parameter in parameters.Where(parameter => parameter.Default is not null))
            {
                TypeSymbol type = TypeNamed(parameter.Type);
                BindValue(parameter.Default!, type, $"`{parameter.Name.Text}` takes {type.Describe()}");
            }
        }
    }

    private void BindStatements(IEnumerable<StatementSyntax> statements)
    {
        foreach (StatementSyntax statement in statements)
        {
            switch (statement)
            {
                case IfStatementSyntax @if:
                    BindCondition(@if.Condition);
                    BindStatements(@if.Body);
                    foreach (ElseIfClauseSyntax elseIf in @if.ElseIfs)
                    {
                        BindCondition(elseIf.Condition);
                        BindStatements(elseIf.Body);
                    }

                    BindStatements(@if.Else?.Body ?? []);
                    break;

                case TextLineSyntax line:
                    BindTextLine(line);
                    break;

                case ChoiceSyntax choice:
                    foreach (OptionSyntax option in choice.Options)
                    {
                        BindInline(option.Text);
                        if (option.Condition is { } condition)
                            BindCondition(condition);

                        BindTags(option.Tags);
                        BindStatements(option.Body);
                    }

                    break;

                case CommandStatementSyntax command:
                    BindCommand(command.Command, command.Arguments, command.Span, inline: false);
                    break;

                case JumpStatementSyntax jump:
                    BindNode(jump.Target.Text, jump.Target.Span, jump.Target);
                    break;

                case CallStatementSyntax call:
                    BindNode(call.Target.Text, call.Target.Span, call.Target);
                    BindTags(call.Tags);
                    break;

                case VariationStatementSyntax variation:
                    foreach (AlternativeSyntax alternative in variation.Alternatives)
                        BindStatements(alternative.Body);
                    break;

                case OnceStatementSyntax once:
                    BindStatements(once.Body);
                    break;

                case SetStatementSyntax set:
                    BindSet(set);
                    break;

                case WaitStatementSyntax wait:
                    BindPacing(wait.Duration, TypeSymbol.Duration, "`@wait` takes a `duration`", "The time to `@wait`", "Write a time longer than zero, like `@wait 0.5s`.");
                    break;
            }
        }
    }

    private void BindTextLine(TextLineSyntax line)
    {
        if (line.Speaker is { } speaker)
            BindSpeaker(line, speaker);
        else
            CheckNarration(line);

        BindInline(line.Content);
        BindTags(line.Tags);
    }

    private void BindSpeaker(TextLineSyntax line, NameSyntax speaker)
    {
        if (!symbols.Actors.TryGetValue(speaker.Text, out ActorSymbol? actor))
        {
            string meant = Suggestions.Closest(speaker.Text, symbols.Actors.Keys) is { } closest ? $"Did you mean `{closest}`? " : "";
            Report(DiagnosticCatalog.UnknownSpeaker, speaker.Span, speaker.Text, meant, BeforeColon(line));
            return;
        }

        Refers(speaker, actor);
        if (line.Pose is not { } pose)
            return;

        if (actor.Poses.FirstOrDefault(known => known.Name == pose.Text) is { } posed)
        {
            Refers(pose, posed);
            return;
        }

        string[] poses = [.. actor.Poses.Select(known => known.Name)];
        string help = Suggestions.Closest(pose.Text, poses) is { } suggestion ? $"Did you mean `{suggestion}`?"
            : poses.Length switch
            {
                0 => $"Give {actor.DisplayName} poses with `poses:` under `@actor {actor.Name}`.",
                1 => $"{actor.DisplayName}'s only pose is {Phrase.And(poses)}.",
                _ => $"{actor.DisplayName}'s poses are {Phrase.And(poses)}.",
            };
        Report(DiagnosticCatalog.UnknownPose, pose.Span, actor.DisplayName, pose.Text, help);
    }

    /// <summary>
    /// Reports narration that almost looks like a speaker, so a mistake never shows as narration: a parenthesis that
    /// isn't a single pose, or a declared actor with no space after the colon.
    /// </summary>
    private void CheckNarration(TextLineSyntax line)
    {
        if (SpeakerScanner.Scan(Tree.Source.Text, line.Span.Start, line.Span.End) is not { } prefix)
            return;

        string before = TextOf(new(line.Span.Start, prefix.Colon - line.Span.Start));
        if (prefix is { Parentheses: { } parentheses, Pose: null })
        {
            Report(DiagnosticCatalog.NotPose, parentheses, TextOf(parentheses), before);
        }
        else if (symbols.Actors.ContainsKey(TextOf(prefix.Name)))
        {
            int textEnd = line.Tags.Count > 0 ? line.Tags[0].Span.Start : line.Span.End;
            string after = TextOf(new(prefix.Colon + 1, textEnd - prefix.Colon - 1)).TrimEnd();
            Report(DiagnosticCatalog.NoSpaceAfterSpeaker, new(line.Span.Start, prefix.Colon + 1 - line.Span.Start), before, $"{before}\\:{after}");
        }
    }

    /// <summary>A text line's start up to its speaker's colon, as written.</summary>
    private string BeforeColon(TextLineSyntax line) =>
        SpeakerScanner.Scan(Tree.Source.Text, line.Span.Start, line.Span.End) is { } prefix ? TextOf(new(line.Span.Start, prefix.Colon - line.Span.Start)) : "";

    private void BindInline(IEnumerable<InlineSyntax> content)
    {
        foreach (InlineSyntax item in content)
        {
            switch (item)
            {
                case MarkupSyntax markup:
                    BindMarkup(markup);
                    break;

                case InterpolationSyntax interpolation:
                    BindInterpolation(interpolation);
                    break;

                case InlineCommandSyntax command:
                    BindCommand(command.Command, command.Arguments, command.Span, inline: true);
                    break;

                case PauseSyntax { Duration: { } duration }:
                    BindPacing(duration, TypeSymbol.Duration, "`{w}` takes a `duration`", "A pause", "Write a pause longer than zero, like `{w 0.5}`.");
                    break;

                case SpeedSyntax { Factor: { } factor }:
                    BindPacing(factor, TypeSymbol.Number, "`{speed}` takes a `number`", "A speed", "Write a speed above zero, like `{speed 0.5}`, or `{speed}` to return to the player's setting.");
                    break;

                case IconSyntax icon when symbols.Icons.TryGetValue(icon.Name.Text, out IconSymbol? symbol):
                    Refers(icon.Name, symbol);
                    break;

                case IconSyntax icon when !icon.Name.IsMissing:
                    ReportWithOptionalHelp(DiagnosticCatalog.UnknownIcon, icon.Name.Span, icon.Name.Text, Suggestions.Closest(icon.Name.Text, symbols.Icons.Keys));
                    break;

                case ConditionalTextSyntax conditional:
                    BindCondition(conditional.Condition);
                    BindInline(conditional.Content);
                    foreach (ElseIfTextSyntax elseIf in conditional.ElseIfs)
                    {
                        BindCondition(elseIf.Condition);
                        BindInline(elseIf.Content);
                    }

                    BindInline(conditional.Else?.Content ?? []);
                    break;
            }
        }
    }

    private void BindMarkup(MarkupSyntax markup)
    {
        if (symbols.Markup.TryGetValue(markup.Name.Text, out MarkupSymbol? symbol))
        {
            Refers(markup.Name, symbol);
            BindArguments($"[{symbol.Name}]", symbol.Parameters, markup.Arguments, markup.Name.Span, named: true);
        }
        else
        {
            if (!markup.Name.IsMissing)
                ReportWithOptionalHelp(DiagnosticCatalog.UnknownMarkup, markup.Name.Span, markup.Name.Text, UnknownMarkupHelp(markup.Name.Text));

            BindUnmatched(markup.Arguments);
        }

        BindInline(markup.Content);
    }

    private string? UnknownMarkupHelp(string name) =>
        name is "speed" ? "Did you mean `{speed}`?"
        : Suggestions.Closest(name, symbols.Markup.Keys) is { } closest ? $"Did you mean `{closest}`?"
        : null;

    /// <summary>A value shown in text, which is text, a number formatted for the player's language, or an actor's display name.</summary>
    private void BindInterpolation(InterpolationSyntax interpolation)
    {
        TypeSymbol type = Bind(interpolation.Value);
        if (type == TypeSymbol.Error || type == TypeSymbol.String || type == TypeSymbol.Number || type == TypeSymbol.Actor)
            return;

        string text = TextOf(interpolation.Value.Span);
        string help = type == TypeSymbol.Bool ? $"Show text that depends on it instead: `{{if {text}}}…{{else}}…{{/if}}`."
            : type is EnumSymbol @enum ? $"Show text that depends on it instead, like `{{if {text} == {@enum.Members[0].Name}}}…{{/if}}`."
            : "Show text that depends on it with `{if …}…{/if}`, or call a function that returns text.";
        Report(DiagnosticCatalog.NotShowable, interpolation.Span, text, type.Describe(), help);
    }

    private void BindCommand(NameSyntax name, IReadOnlyList<ArgumentSyntax> arguments, TextSpan span, bool inline)
    {
        if (!symbols.Commands.TryGetValue(name.Text, out CommandSymbol? command))
        {
            if (!name.IsMissing)
                ReportWithOptionalHelp(DiagnosticCatalog.UnknownCommand, name.Span, name.Text, Suggestions.Closest(name.Text, symbols.Commands.Keys));

            BindUnmatched(arguments);
            return;
        }

        Refers(name, command);
        if (inline && !command.IsInline)
            Report(DiagnosticCatalog.NotInline, name.Span, name.Text);

        BindArguments($"@{command.Name}", command.Parameters, arguments, span, named: true);
    }

    private void BindTags(IEnumerable<TagSyntax> tags)
    {
        foreach (TagSyntax tag in tags.Where(tag => tag.Name is not ("id" or "was")))
        {
            if (!symbols.Tags.TryGetValue(tag.Name, out TagSymbol? symbol))
            {
                string meant = Suggestions.Closest(tag.Name, symbols.Tags.Keys) is { } closest ? $"Did you mean `#{closest}`? " : "";
                Report(DiagnosticCatalog.UnknownTag, tag.Span, tag.Name, meant);
            }
            else
            {
                Refers(tag, new(tag.Span.Start + 1, tag.Name.Length), symbol);
                BindTagValue(tag, symbol);
            }
        }
    }

    /// <summary>Checks a declared tag's value: none for a flag, one for a tag with a type, and a member if that type is an enum.</summary>
    private void BindTagValue(TagSyntax tag, TagSymbol symbol)
    {
        if (symbol.ValueType is not { } type)
        {
            if (tag.Value is not null)
                Report(DiagnosticCatalog.TagValuePresence, tag.Span, tag.Name, "doesn't take a value", $"Write `#{tag.Name}` on its own.");

            return;
        }

        if (tag.Value is null || (tag.Value.Length == 0 && !symbol.AllowsEmpty))
        {
            string example = type is EnumSymbol @enum ? @enum.Members[0].Name : "value";
            Report(DiagnosticCatalog.TagValuePresence, tag.Span, tag.Name, "needs a value", $"Write a value after the colon: `#{tag.Name}:{example}`.");
            return;
        }

        if (type is not EnumSymbol values || tag.Value.Length == 0)
            return;

        if (values.Members.FirstOrDefault(member => member.Name == tag.Value) is { } value)
        {
            Refers(new TextSpan(tag.Span.End - tag.Value.Length, tag.Value.Length), value);
            bindings.BindTagValue(tag, value);
            return;
        }

        string[] members = [.. values.Members.Select(member => member.Name)];
        string help = Suggestions.Closest(tag.Value, members) is { } closest ? $"Did you mean `#{tag.Name}:{closest}`?" : $"Use one of {Phrase.Or(members)}.";
        Report(DiagnosticCatalog.TagValueNotMember, tag.Span, tag.Value, values.Describe(), tag.Name, help);
    }

    private void BindSet(SetStatementSyntax set)
    {
        TypeSymbol target = Bind(set.Variable);
        string holds = $"`${set.Variable.Name}` holds {target.Describe()}";
        if (set.Operator is AssignmentOperator.Assign)
        {
            BindValue(set.Value, target, holds);
            return;
        }

        TypeSymbol value = Bind(set.Value, target);
        if (target == TypeSymbol.Error || value == TypeSymbol.Error)
            return;

        BinaryOperator @operator = set.Operator is AssignmentOperator.Add ? BinaryOperator.Add : BinaryOperator.Subtract;
        TypeSymbol? result = OperatorType(@operator, target, value);
        if (result is null)
            Report(DiagnosticCatalog.OperatorTypes, new(set.Variable.Span.Start, set.Value.Span.End - set.Variable.Span.Start), TextOf(set.OperatorSpan), $"{target.Describe()} and {value.Describe()}", OperatorHelp(@operator));
        else if (result != target)
            ReportWithOptionalHelp(DiagnosticCatalog.ValueType, set.Value.Span, holds, value.Describe(), ValueHelp(target));
        else
            bindings.ConvertTo(set.Value, target);
    }

    /// <summary>Binds a value that has to have <paramref name="target"/>'s type, reporting it with what it's for if it doesn't.</summary>
    /// <returns>Whether the value's type converts to <paramref name="target"/>.</returns>
    private bool BindValue(ExpressionSyntax value, TypeSymbol target, string purpose)
    {
        TypeSymbol type = Bind(value, target);
        if (Converts(type, target))
        {
            bindings.ConvertTo(value, target);
            return true;
        }

        ReportWithOptionalHelp(DiagnosticCatalog.ValueType, value.Span, purpose, type.Describe(), ValueHelp(target));
        return false;
    }

    /// <summary>
    /// Binds a pacing value, and reports it if it has the right type but is a constant that isn't more than zero. A value
    /// computed at run time is checked by the runtime.
    /// </summary>
    private void BindPacing(ExpressionSyntax value, TypeSymbol target, string purpose, string what, string help)
    {
        if (BindValue(value, target, purpose) && ConstantValue.Fold(value) is <= 0)
            Report(DiagnosticCatalog.NotPositive, value.Span, what, TextOf(value.Span), help);
    }

    private void BindCondition(ExpressionSyntax condition)
    {
        TypeSymbol type = Bind(condition, TypeSymbol.Bool);
        if (Converts(type, TypeSymbol.Bool))
            return;

        string text = TextOf(condition.Span);
        string? meant = type == TypeSymbol.Number ? $"Did you mean `{text} > 0`?" : type == TypeSymbol.String ? $"Did you mean `{text} != \"\"`?" : null;
        ReportWithOptionalHelp(DiagnosticCatalog.NotCondition, condition.Span, text, type.Describe(), meant);
    }

    /// <summary>Binds an expression and returns its type.</summary>
    /// <param name="expression">The expression to bind.</param>
    /// <param name="expected">The type expected where the expression appears, which a bare name is read against.</param>
    private TypeSymbol Bind(ExpressionSyntax expression, TypeSymbol? expected = null)
    {
        TypeSymbol type = BindCore(expression, expected);
        bindings.BindType(expression, type);
        return type;
    }

    private TypeSymbol BindCore(ExpressionSyntax expression, TypeSymbol? expected) => expression switch
    {
        NumberLiteralSyntax => TypeSymbol.Number,
        DurationLiteralSyntax => TypeSymbol.Duration,
        StringLiteralSyntax => TypeSymbol.String,
        BooleanLiteralSyntax => TypeSymbol.Bool,
        VariableExpressionSyntax variable => BindVariable(variable),
        NameExpressionSyntax name => BindName(name, expected),
        CallExpressionSyntax call => BindCall(call),
        ParenthesizedExpressionSyntax parenthesized => Bind(parenthesized.Expression, expected),
        UnaryExpressionSyntax unary => BindUnary(unary),
        BinaryExpressionSyntax binary => BindBinary(binary),
        _ => TypeSymbol.Error,
    };

    private TypeSymbol BindVariable(VariableExpressionSyntax variable)
    {
        if (variable.Name.Length == 0)
            return TypeSymbol.Error;

        if (symbols.Variables.TryGetValue(variable.Name, out VariableSymbol? symbol))
        {
            Refers(variable, symbol);
            return symbol.Type;
        }

        string? closest = Suggestions.Closest(variable.Name, symbols.Variables.Keys);
        ReportWithOptionalHelp(DiagnosticCatalog.UnknownVariable, variable.Span, $"${variable.Name}", closest is null ? null : $"${closest}");
        return TypeSymbol.Error;
    }

    /// <summary>Reads a bare name against the expected type: a member of an enum, an actor or a node. No other type has names.</summary>
    private TypeSymbol BindName(NameExpressionSyntax name, TypeSymbol? expected)
    {
        if (expected == TypeSymbol.Error)
            return TypeSymbol.Error;

        if (expected == TypeSymbol.Node)
            return BindNode(name.Name, name.Span, name);

        if (expected is EnumSymbol @enum)
        {
            if (@enum.Members.FirstOrDefault(member => member.Name == name.Name) is not { } member)
                return NotValueOf(name, @enum, @enum.Members.Select(member => member.Name), $"Use one of {Phrase.Or(@enum.Members.Select(member => member.Name))}.");

            Refers(name, member);
            return @enum;
        }

        if (expected == TypeSymbol.Actor)
        {
            if (!symbols.Actors.TryGetValue(name.Name, out ActorSymbol? actor))
                return NotValueOf(name, TypeSymbol.Actor, symbols.Actors.Keys, null);

            Refers(name, actor);
            return TypeSymbol.Actor;
        }

        string? meant = symbols.Variables.ContainsKey(name.Name) ? $"Did you mean `${name.Name}`?"
            : expected == TypeSymbol.String ? $"If it's text, put it in quotes: `\"{name.Name}\"`."
            : null;
        ReportWithOptionalHelp(DiagnosticCatalog.NameNotAllowed, name.Span, name.Name, meant);
        return TypeSymbol.Error;
    }

    private TypeSymbol NotValueOf(NameExpressionSyntax name, TypeSymbol type, IEnumerable<string> values, string? otherwise)
    {
        string? help = Suggestions.Closest(name.Name, values) is { } closest ? $"Did you mean `{closest}`?" : otherwise;
        ReportWithOptionalHelp(DiagnosticCatalog.NotValueOfType, name.Span, name.Name, type.Describe(), help);
        return TypeSymbol.Error;
    }

    private TypeSymbol BindNode(string written, TextSpan span, SyntaxNode key)
    {
        if (FullName(written, span) is not { } name)
            return TypeSymbol.Error;

        if (symbols.Nodes.TryGetValue(name, out NodeSymbol? node))
        {
            Refers(key, span, node);
            return TypeSymbol.Node;
        }

        if (symbols.Aliases.TryGetValue(name, out NodeSymbol? current))
        {
            Refers(key, span, current);
            Report(DiagnosticCatalog.OldNodeName, span, written, AsWritten(current.Name, written));
            return TypeSymbol.Node;
        }

        string? closest = Suggestions.Closest(name, symbols.Nodes.Keys);
        ReportWithOptionalHelp(DiagnosticCatalog.UnknownNode, span, written, closest is null ? null : AsWritten(closest, written));
        return TypeSymbol.Error;
    }

    /// <summary>Writes a full node name the way <paramref name="written"/> is: relative to the file's prefix if it is, and the node is under it.</summary>
    private string AsWritten(string fullName, string written) =>
        written.StartsWith('.') && Tree.Root.Prefix?.Name.Text.TrimStart('.') is { } prefix && fullName.StartsWith(prefix + ".", StringComparison.Ordinal)
            ? fullName[prefix.Length..]
            : fullName;

    private TypeSymbol BindCall(CallExpressionSyntax call)
    {
        if (!symbols.Functions.TryGetValue(call.Function.Text, out FunctionSymbol? function))
        {
            if (!call.Function.IsMissing)
                ReportWithOptionalHelp(DiagnosticCatalog.UnknownFunction, call.Function.Span, call.Function.Text, Suggestions.Closest(call.Function.Text, symbols.Functions.Keys));

            foreach (ExpressionSyntax argument in call.Arguments)
                Bind(argument, TypeSymbol.Error);

            return TypeSymbol.Error;
        }

        Refers(call.Function, function);
        ArgumentSyntax[] arguments = [.. call.Arguments.Select(argument => new ArgumentSyntax(null, argument) { Span = argument.Span })];
        BindArguments($"{function.Name}()", function.Parameters, arguments, call.Span, named: false);
        return function.ReturnType;
    }

    /// <summary>
    /// Matches arguments to parameters, binding each against its parameter's type: positional ones in order, then named
    /// ones by name. Every parameter without a default needs one, unless an argument's name is unknown, since that
    /// argument was most likely meant for it.
    /// </summary>
    /// <param name="owner">What takes the arguments, as messages write it: <c>@show</c>, <c>[wave]</c> or <c>has_item()</c>.</param>
    /// <param name="parameters">The owner's parameters.</param>
    /// <param name="arguments">The arguments, positional ones first.</param>
    /// <param name="span">Where a missing argument is reported.</param>
    /// <param name="named">Whether arguments can be named, which says how a missing one can be added.</param>
    private void BindArguments(string owner, IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<ArgumentSyntax> arguments, TextSpan span, bool named)
    {
        HashSet<ParameterSymbol> given = [];
        List<ArgumentSyntax> extra = [];
        bool misnamed = false;
        int next = 0;
        foreach (ArgumentSyntax argument in arguments)
        {
            ParameterSymbol? parameter;
            if (argument.Name is not { } name)
            {
                parameter = parameters.ElementAtOrDefault(next++);
                if (parameter is null)
                    extra.Add(argument);
            }
            else
            {
                parameter = parameters.FirstOrDefault(candidate => candidate.Name == name.Text);
                if (parameter is null)
                {
                    misnamed = true;
                    ReportWithOptionalHelp(DiagnosticCatalog.UnknownParameter, name.Span, owner, name.Text, Suggestions.Closest(name.Text, parameters.Select(candidate => candidate.Name)));
                }
                else
                {
                    Refers(name, parameter);
                    if (given.Contains(parameter))
                    {
                        Report(DiagnosticCatalog.RepeatedArgument, argument.Span, owner, parameter.Name);
                        parameter = null;
                    }
                }
            }

            if (parameter is not null)
                given.Add(parameter);

            TypeSymbol type = Bind(argument.Value, parameter?.Type ?? TypeSymbol.Error);
            if (parameter is null)
                continue;

            bindings.BindArgument(argument.Value, parameter);
            if (Converts(type, parameter.Type))
                bindings.ConvertTo(argument.Value, parameter.Type);
            else
                ReportWithOptionalHelp(DiagnosticCatalog.ArgumentType, argument.Value.Span, owner, parameter.Type.Describe(), parameter.Name, type.Describe(), ValueHelp(parameter.Type));
        }

        if (extra.Count > 0)
        {
            string takes = parameters.Count switch { 0 => "no arguments", 1 => "only 1 argument", var count => $"only {count} arguments" };
            Report(DiagnosticCatalog.TooManyArguments, new(extra[0].Span.Start, extra[^1].Span.End - extra[0].Span.Start), owner, takes);
        }

        string[] missing = [.. parameters.Where(parameter => !parameter.IsOptional && !given.Contains(parameter)).Select(parameter => parameter.Name)];
        if (missing.Length > 0 && !misnamed)
        {
            string needs = missing.Length == 1 ? $"a value for {Phrase.And(missing)}" : $"values for {Phrase.And(missing)}";
            string help = (named, missing.Length) switch
            {
                (true, 1) => $"Add it after the others, or by name: `{missing[0]}=…`.",
                (true, _) => $"Add them after the others, or by name, like `{missing[0]}=…`.",
                (false, 1) => "Add it in the brackets, in order.",
                (false, _) => "Add them in the brackets, in order.",
            };
            Report(DiagnosticCatalog.MissingArgument, span, owner, needs, help);
        }
    }

    /// <summary>Binds the arguments of something that doesn't exist, so problems inside them are still reported, but not their types.</summary>
    private void BindUnmatched(IEnumerable<ArgumentSyntax> arguments)
    {
        foreach (ArgumentSyntax argument in arguments)
            Bind(argument.Value, TypeSymbol.Error);
    }

    private TypeSymbol BindUnary(UnaryExpressionSyntax unary)
    {
        TypeSymbol operand = Bind(unary.Operand);
        TypeSymbol fallback = unary.Operator is UnaryOperator.Not ? TypeSymbol.Bool : TypeSymbol.Error;
        if (operand == TypeSymbol.Error)
            return fallback;

        if (unary.Operator is UnaryOperator.Not ? operand == TypeSymbol.Bool : operand == TypeSymbol.Number || operand == TypeSymbol.Duration)
            return operand;

        string help = unary.Operator is UnaryOperator.Not ? "`not` works on `true` or `false`." : "`-` makes a number or a duration negative.";
        Report(DiagnosticCatalog.OperatorTypes, unary.Span, TextOf(unary.OperatorSpan), operand.Describe(), help);
        return fallback;
    }

    private TypeSymbol BindBinary(BinaryExpressionSyntax binary)
    {
        if (IsChainedComparison(binary))
        {
            Bind(binary.Left, TypeSymbol.Error);
            Bind(binary.Right, TypeSymbol.Error);
            return TypeSymbol.Bool;
        }

        (TypeSymbol left, TypeSymbol right) = binary.Operator is BinaryOperator.Equals or BinaryOperator.NotEquals
            ? BindEqualityOperands(binary)
            : (Bind(binary.Left), Bind(binary.Right));

        TypeSymbol fallback = binary.Operator is BinaryOperator.Or or BinaryOperator.And or BinaryOperator.Equals or BinaryOperator.NotEquals
            or BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual
            ? TypeSymbol.Bool
            : TypeSymbol.Error;
        if (left == TypeSymbol.Error || right == TypeSymbol.Error)
            return fallback;

        if (OperatorType(binary.Operator, left, right) is { } result)
        {
            if (binary.Operator is BinaryOperator.Divide or BinaryOperator.Remainder && ConstantValue.Fold(binary.Right) == 0m)
                Report(DiagnosticCatalog.DivisionByZero, binary.Span, TextOf(binary.Span));

            if (binary.Operator is not (BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Remainder))
            {
                bindings.ConvertTo(binary.Left, right);
                bindings.ConvertTo(binary.Right, left);
            }

            return result;
        }

        Report(DiagnosticCatalog.OperatorTypes, binary.Span, TextOf(binary.OperatorSpan), $"{left.Describe()} and {right.Describe()}", OperatorHelp(binary.Operator));
        return fallback;
    }

    /// <summary>
    /// Binds the operands of <c>==</c> or <c>!=</c>. A bare name on one side is read against the other side's type, so
    /// that side is bound first. Two bare names have no type to be read against.
    /// </summary>
    private (TypeSymbol Left, TypeSymbol Right) BindEqualityOperands(BinaryExpressionSyntax binary)
    {
        bool leftIsName = Unwrap(binary.Left) is NameExpressionSyntax;
        bool rightIsName = Unwrap(binary.Right) is NameExpressionSyntax;
        if (leftIsName && rightIsName)
        {
            Report(DiagnosticCatalog.TwoNames, binary.Span, TextOf(binary.Span));
            return (TypeSymbol.Error, TypeSymbol.Error);
        }

        if (leftIsName)
        {
            TypeSymbol right = Bind(binary.Right);
            return (Bind(binary.Left, right), right);
        }

        TypeSymbol left = Bind(binary.Left);
        return (left, Bind(binary.Right, left));
    }

    /// <summary>
    /// Whether a comparison has another comparison of the same level as its left operand, with no brackets, as in
    /// <c>$a &lt; $b &lt; $c</c>. Comparisons don't chain, and the parser has already reported it (PIB1061).
    /// </summary>
    private static bool IsChainedComparison(BinaryExpressionSyntax binary) =>
        binary.Left is BinaryExpressionSyntax inner && ComparisonLevel(inner.Operator) is { } level && level == ComparisonLevel(binary.Operator);

    private static int? ComparisonLevel(BinaryOperator @operator) => @operator switch
    {
        BinaryOperator.Equals or BinaryOperator.NotEquals => 1,
        BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual => 2,
        _ => null,
    };

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? Unwrap(parenthesized.Expression) : expression;

    /// <summary>
    /// The type an operator gives two operands, from the operator types in <c>docs/language/reference.md</c>, or
    /// <see langword="null"/> if it doesn't work on them. A number stands in for a duration where the other side is one,
    /// except in <c>*</c> and <c>/</c>, where a number is a factor.
    /// </summary>
    private static TypeSymbol? OperatorType(BinaryOperator @operator, TypeSymbol left, TypeSymbol right)
    {
        bool numbers = left == TypeSymbol.Number && right == TypeSymbol.Number;
        bool durations = !numbers && IsTime(left) && IsTime(right);

        return @operator switch
        {
            BinaryOperator.Or or BinaryOperator.And when left == TypeSymbol.Bool && right == TypeSymbol.Bool => TypeSymbol.Bool,
            BinaryOperator.Equals or BinaryOperator.NotEquals when left == right || durations => TypeSymbol.Bool,
            BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual when numbers || durations => TypeSymbol.Bool,
            BinaryOperator.Add when left == TypeSymbol.String && right == TypeSymbol.String => TypeSymbol.String,
            BinaryOperator.Add or BinaryOperator.Subtract when numbers => TypeSymbol.Number,
            BinaryOperator.Add or BinaryOperator.Subtract when durations => TypeSymbol.Duration,
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Remainder when numbers => TypeSymbol.Number,
            BinaryOperator.Multiply when (left == TypeSymbol.Duration && right == TypeSymbol.Number) || (left == TypeSymbol.Number && right == TypeSymbol.Duration) => TypeSymbol.Duration,
            BinaryOperator.Divide when left == TypeSymbol.Duration && right == TypeSymbol.Number => TypeSymbol.Duration,
            BinaryOperator.Divide when left == TypeSymbol.Duration && right == TypeSymbol.Duration => TypeSymbol.Number,
            _ => null,
        };

        static bool IsTime(TypeSymbol type) => type == TypeSymbol.Number || type == TypeSymbol.Duration;
    }

    private static string OperatorHelp(BinaryOperator @operator) => @operator switch
    {
        BinaryOperator.Or or BinaryOperator.And => "`and` and `or` join two conditions, each `true` or `false`.",
        BinaryOperator.Equals or BinaryOperator.NotEquals => "`==` and `!=` compare two values of the same type.",
        BinaryOperator.Add => "`+` adds two numbers or two durations, or joins two pieces of text.",
        BinaryOperator.Subtract => "`-` subtracts two numbers or two durations.",
        BinaryOperator.Multiply => "`*` multiplies two numbers, or a duration by a number.",
        BinaryOperator.Divide => "`/` divides a number by a number, or a duration by a number or a duration.",
        BinaryOperator.Remainder => "`%` gives the remainder of dividing two numbers.",
        _ => "`<`, `<=`, `>` and `>=` compare two numbers or two durations.",
    };

    /// <summary>Says what values a type takes, for the help of a value that has the wrong type.</summary>
    private string? ValueHelp(TypeSymbol type) => type switch
    {
        EnumSymbol @enum => $"Use one of {Phrase.Or(@enum.Members.Select(member => member.Name))}.",
        _ when type == TypeSymbol.Bool => "Use `true` or `false`.",
        _ when type == TypeSymbol.Number => "Write a number, like `1` or `0.5`.",
        _ when type == TypeSymbol.Duration => "Write a duration, like `0.5s` or `300ms`.",
        _ when type == TypeSymbol.String => "Put text in quotes, like `\"key\"`.",
        _ when type == TypeSymbol.Actor => symbols.Actors.Keys.FirstOrDefault() is { } actor ? $"Use an actor's ID, like `{actor}`." : null,
        _ when type == TypeSymbol.Node => symbols.Nodes.Keys.FirstOrDefault() is { } node ? $"Use a node's name, like `{node}`." : null,
        _ => null,
    };

    /// <summary>Whether a value of type <paramref name="from"/> can go where <paramref name="to"/> is expected: the same type, a number where a duration is, or a type that can't be known.</summary>
    private static bool Converts(TypeSymbol from, TypeSymbol to) =>
        from == to || from == TypeSymbol.Error || to == TypeSymbol.Error || (from == TypeSymbol.Number && to == TypeSymbol.Duration);

    /// <summary>The type a declaration names. A type that doesn't exist has already been reported by the declaration pass.</summary>
    private TypeSymbol TypeNamed(NameSyntax name) => symbols.FindType(name.Text) ?? TypeSymbol.Error;
}
