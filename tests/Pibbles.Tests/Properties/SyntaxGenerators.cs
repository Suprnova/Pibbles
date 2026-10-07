using CsCheck;
using Pibbles.Syntax;

namespace Pibbles.Tests.Properties;

/// <summary>
/// Generates random syntax trees that the parser would produce from valid source, so a printed tree parses back to
/// itself with no diagnostics.
/// </summary>
/// <remarks>
/// Trees keep to the parser's normal form: adjacent text runs are merged, a line's text is trimmed, consecutive
/// options form one choice, and an operand binds at least as tightly as its operator unless it's in parentheses.
/// </remarks>
internal static class SyntaxGenerators
{
    private const int MaxDepth = 3;

    /// <summary>Every reserved, contextual and built-in word, so a generated name is never read as one.</summary>
    private static readonly HashSet<string> Reserved =
    [
        "prefix", "actor", "enum", "var", "command", "markup", "icon", "tag", "function", "if", "elif", "else", "set", "jump",
        "call", "return", "end", "wait", "sequence", "cycle", "once", "term", "resume", "shuffle", "w", "p", "br", "auto",
        "true", "false", "and", "or", "not", "nowait", "speaker", "visits", "random", "bool", "number", "string", "duration",
        "node", "speed", "b", "i", "u", "s", "color", "name", "poses", "inline", "waits", "required", "persona", "id", "was",
        "migrates", "draft", "voice", "unvoiced", "default",
    ];

    private const string Letters = "abcdefghijklmnopqrstuvwxyz";

    private static readonly Gen<string> Identifier =
        Gen.Select(Gen.Char[Letters], Gen.String[Gen.Char[Letters + "0123456789_"], 0, 5], (first, rest) => first + rest)
            .Where(word => !Reserved.Contains(word));

    private static readonly Gen<string> DottedName = Identifier.Array[1, 3].Select(parts => string.Join('.', parts));

    private static readonly Gen<string> NodeName = Gen.Frequency((3, DottedName), (1, DottedName.Select(name => "." + name)));

    private static readonly Gen<string> Piece = Gen.Frequency(
        (30, Gen.Char["abcdefghij klmnop ABC 0123 .,!?'\"-:;()]}[{#@\\/=<>*&%$~^|é"].Select(c => c.ToString())),
        (1, Gen.Const("😀")),
        (1, Gen.Const("é")));

    private static readonly Gen<string> Text = Piece.Array[1, 8].Select(string.Concat);

    private static readonly Gen<double> Decimal = Gen.Int[0, 99999].Select(n => n / 100.0);

    private static readonly Gen<TagSyntax> Tag = Gen.Select(Identifier, Maybe(Gen.String[Gen.Char["abcxyz019_.-:"], 0, 5]), (name, value) => new TagSyntax(name, value) { Span = default });

    private static readonly Gen<TagSyntax> IdTag =
        Gen.Select(Gen.Char[Letters], Gen.String[Gen.Char[Letters + "0123456789_"], 0, 5], (first, rest) => new TagSyntax("id", first + rest) { Span = default });

    /// <summary>Comments' text, as a comment reads back: without the whitespace around it.</summary>
    public static Gen<string[]> Comments { get; } = Gen.String[Gen.Char["abc xyz 019é#@[{/:-"], 0, 12].Select(text => text.Trim()).Array[0, 6];

    public static Gen<FileSyntax> File { get; } = Gen.Select(
        Maybe(DottedName.Select(name => new PrefixSyntax(Name(name)) { Span = default })),
        Declaration().Array[0, 4],
        Node().Array[0, 3],
        (prefix, declarations, nodes) => new FileSyntax(prefix, declarations, nodes) { Span = default });

    private static NameSyntax Name(string text) => new(text) { Span = default };

    private static Gen<T?> Maybe<T>(Gen<T> gen) where T : class => Gen.Bool.SelectMany(present => present ? gen.Select(T? (value) => value) : Gen.Const((T?)null));

    /// <summary>Builds a generator only when it's used, so recursive generators don't build every level up front.</summary>
    private static Gen<T> Defer<T>(Func<Gen<T>> build) => Gen.Const(0).SelectMany(_ => build());

    private static Gen<TagSyntax[]> Tags(bool other, bool id) => Gen.Select(
        other ? Tag.Array[0, 2] : Gen.Const<TagSyntax[]>([]),
        id ? Maybe(IdTag) : Gen.Const((TagSyntax?)null),
        (tags, idTag) => idTag is null ? tags : [.. tags, idTag]);

    private static Gen<NodeSyntax> Node() => Gen.Select(
        NodeName,
        NodeName.Select(Name).Array[0, 2],
        Statements(MaxDepth, 0, 5),
        (name, aliases, body) => new NodeSyntax(Name(name), aliases, body) { Span = default });

    private static Gen<DeclarationSyntax> Declaration()
    {
        Gen<ParameterSyntax> parameter = Gen.Select(Identifier, Identifier, Maybe(Constant()),
            (name, type, @default) => new ParameterSyntax(Name(name), Name(type), @default) { Span = default });
        Gen<NameSyntax[]> names = Identifier.Select(Name).Array[1, 3];

        Gen<string> displayName = Gen.String[Gen.Char["abcdeABC xyz 019.,'!-?:()#@/é"], 1, 10].Select(name => name.Trim()).Where(name => name.Length > 0);
        Gen<DeclarationSyntax> actor = Gen.Select(Identifier, Maybe(displayName), Identifier.Select(Name).Array[0, 3],
                (name, display, poses) => new ActorDeclarationSyntax(Name(name), display, null, poses) { Span = default })
            .Where(actor => actor.DisplayName is not null || actor.Poses.Count > 0)
            .Select(DeclarationSyntax (actor) => actor);

        Gen<TagEntrySyntax> tagEntry = Gen.Select(Identifier, Maybe(Identifier), Gen.Bool,
            (name, type, empty) => new TagEntrySyntax(Name(name), type is null ? null : Name(type), type is not null && empty) { Span = default });

        return Gen.OneOf(
            actor,
            Gen.Select(Identifier, names, DeclarationSyntax (name, members) => new EnumDeclarationSyntax(Name(name), members) { Span = default }),
            Gen.Select(Identifier, Maybe(Identifier), Constant(),
                DeclarationSyntax (name, type, value) => new VariableDeclarationSyntax(new(name) { Span = default }, type is null ? null : Name(type), value) { Span = default }),
            Gen.Select(Identifier, parameter.Array[0, 2], Gen.Bool, Gen.Bool,
                DeclarationSyntax (name, parameters, isInline, waits) => new CommandDeclarationSyntax(Name(name), parameters, isInline, waits) { Span = default }),
            Gen.Select(Identifier, parameter.Array[0, 2], DeclarationSyntax (name, parameters) => new MarkupDeclarationSyntax(Name(name), parameters) { Span = default }),
            names.Select(DeclarationSyntax (icons) => new IconDeclarationSyntax(icons) { Span = default }),
            tagEntry.Array[1, 3].Select(DeclarationSyntax (entries) => new TagDeclarationSyntax(entries) { Span = default }),
            Gen.Select(Identifier, parameter.Array[0, 2], Identifier,
                DeclarationSyntax (name, parameters, type) => new FunctionDeclarationSyntax(Name(name), parameters, Name(type)) { Span = default }));
    }

    /// <summary>A constant: a literal, a negative number or duration, or a bare name.</summary>
    private static Gen<ExpressionSyntax> Constant() => Gen.OneOf(
        Literal(),
        Gen.Select(Gen.Bool, Decimal, ExpressionSyntax (isDuration, value) =>
            new UnaryExpressionSyntax(UnaryOperator.Negate, default, isDuration ? new DurationLiteralSyntax(value) { Span = default } : new NumberLiteralSyntax(value) { Span = default }) { Span = default }),
        DottedName.Select(ExpressionSyntax (name) => new NameExpressionSyntax(name) { Span = default }));

    private static Gen<StatementSyntax[]> Statements(int depth, int min, int max) => Statement(depth).Array[min, max].Select(MergeChoices);

    /// <summary>Joins consecutive choices, since consecutive options always parse as one choice.</summary>
    private static StatementSyntax[] MergeChoices(StatementSyntax[] statements)
    {
        List<StatementSyntax> merged = [];
        foreach (StatementSyntax statement in statements)
        {
            if (statement is ChoiceSyntax next && merged is [.., ChoiceSyntax previous])
                merged[^1] = new ChoiceSyntax([.. previous.Options, .. next.Options]) { Span = default };
            else
                merged.Add(statement);
        }

        return [.. merged];
    }

    private static Gen<StatementSyntax> Statement(int depth)
    {
        List<(int, Gen<StatementSyntax>)> forms =
        [
            (8, TextLine()),
            (1, Gen.Select(Identifier, Gen.OneOfConst(AssignmentOperator.Assign, AssignmentOperator.Add, AssignmentOperator.Subtract), Expression(2),
                StatementSyntax (variable, @operator, value) => new SetStatementSyntax(new(variable) { Span = default }, @operator, default, value) { Span = default })),
            (1, NodeName.Select(StatementSyntax (target) => new JumpStatementSyntax(Name(target)) { Span = default })),
            (1, Gen.Select(NodeName, Tags(other: true, id: true), StatementSyntax (target, tags) => new CallStatementSyntax(Name(target), tags) { Span = default })),
            (1, Gen.Const<StatementSyntax>(new ReturnStatementSyntax { Span = default })),
            (1, Gen.Const<StatementSyntax>(new EndStatementSyntax { Span = default })),
            (1, Expression(2).Select(StatementSyntax (duration) => new WaitStatementSyntax(duration) { Span = default })),
            (2, Gen.Select(Identifier, Arguments(1, allowWait: true),
                StatementSyntax (name, arguments) => new CommandStatementSyntax(Name(name), arguments.Arguments, arguments.Wait) { Span = default })),
        ];

        if (depth > 0)
        {
            forms.AddRange(
            [
                (2, Defer(() => Option(depth).Array[1, 3]).Select(StatementSyntax (options) => new ChoiceSyntax(options) { Span = default })),
                (2, Defer(() => If(depth))),
                (1, Defer(() => Gen.Select(Gen.Bool, Tags(other: false, id: true), Block(depth - 1).Select(body => new AlternativeSyntax(body) { Span = default }).Array[1, 3],
                    StatementSyntax (cycle, tags, alternatives) => new VariationStatementSyntax(cycle ? VariationKind.Cycle : VariationKind.Sequence, tags, alternatives) { Span = default }))),
                (1, Defer(() => Gen.Select(Tags(other: false, id: true), Block(depth - 1), StatementSyntax (tags, body) => new OnceStatementSyntax(tags, body) { Span = default }))),
            ]);
        }

        return Gen.Frequency([.. forms]);
    }

    private static Gen<StatementSyntax[]> Block(int depth) => Statements(depth, 1, 3);

    private static Gen<StatementSyntax> If(int depth) => Gen.Select(
        Expression(2),
        Block(depth - 1),
        Gen.Select(Expression(2), Block(depth - 1), (condition, body) => new ElseIfClauseSyntax(condition, body) { Span = default }).Array[0, 2],
        Maybe(Block(depth - 1).Select(body => new ElseClauseSyntax(body) { Span = default })),
        StatementSyntax (condition, body, elseIfs, @else) => new IfStatementSyntax(condition, body, elseIfs, @else) { Span = default });

    private static Gen<OptionSyntax> Option(int depth) => Gen.Select(
        LineContent(option: true, required: false),
        Maybe(Expression(2)),
        Gen.Bool,
        Tags(other: true, id: true),
        Statements(depth - 1, 0, depth > 1 ? 3 : 0),
        (text, condition, isOnce, tags, body) => new OptionSyntax(text, condition, isOnce, tags, body) { Span = default });

    private static Gen<StatementSyntax> TextLine()
    {
        Gen<InlineSyntax[]> content = LineContent(option: false, required: true);
        return Gen.OneOf(
            Gen.Select(content, Tags(other: true, id: true), StatementSyntax (text, tags) => new TextLineSyntax(null, null, text, tags) { Span = default }),
            Gen.Select(Identifier, Maybe(Identifier), content, Tags(other: true, id: true),
                StatementSyntax (speaker, pose, text, tags) => new TextLineSyntax(Name(speaker), pose is null ? null : Name(pose), text, tags) { Span = default }),
            Gen.Select(Identifier, Identifier, Tags(other: true, id: false),
                StatementSyntax (speaker, pose, tags) => new TextLineSyntax(Name(speaker), Name(pose), [], tags) { Span = default }));
    }

    /// <summary>A line's inline text: merged, with leading and trailing whitespace trimmed, as the parser reads it.</summary>
    private static Gen<InlineSyntax[]> LineContent(bool option, bool required) =>
        InlineItem(2, option).Array[required ? 1 : 0, 5]
            .Select(items => Trim(Merge(items)))
            .Where(items => !required || items.Length > 0);

    private static Gen<InlineSyntax[]> InlineContent(int depth, bool option) => InlineItem(depth, option).Array[0, 3].Select(Merge);

    private static Gen<InlineSyntax> InlineItem(int depth, bool option)
    {
        List<(int, Gen<InlineSyntax>)> forms =
        [
            (8, Text.Select(InlineSyntax (text) => new TextRunSyntax(text) { Span = default })),
            (1, Identifier.Select(InlineSyntax (name) => new InterpolationSyntax(new VariableExpressionSyntax(name) { Span = default }) { Span = default })),
            (1, Defer(() => Call(1)).Select(InlineSyntax (call) => new InterpolationSyntax(call) { Span = default })),
            (1, Gen.Const<InlineSyntax>(new LineBreakSyntax { Span = default })),
            (1, Identifier.Select(InlineSyntax (name) => new IconSyntax(Name(name)) { Span = default })),
        ];

        if (!option)
        {
            forms.AddRange(
            [
                (1, Gen.Select(Identifier, Arguments(1, allowWait: true),
                    InlineSyntax (name, arguments) => new InlineCommandSyntax(Name(name), arguments.Arguments, arguments.Wait) { Span = default })),
                (1, Maybe(Value(1)).Select(InlineSyntax (duration) => new PauseSyntax(duration) { Span = default })),
                (1, Maybe(Value(1)).Select(InlineSyntax (factor) => new SpeedSyntax(factor) { Span = default })),
                (1, Gen.Const<InlineSyntax>(new PageBreakSyntax { Span = default })),
            ]);
        }

        if (depth > 0)
        {
            forms.AddRange(
            [
                (1, Defer(() => Gen.Select(Identifier, Arguments(1, allowWait: false), InlineContent(depth - 1, option),
                    InlineSyntax (name, arguments, content) => new MarkupSyntax(Name(name), arguments.Arguments, content) { Span = default }))),
                (1, Defer(() => Gen.Select(
                    Expression(2),
                    InlineContent(depth - 1, option),
                    Gen.Select(Expression(2), InlineContent(depth - 1, option), (condition, content) => new ElseIfTextSyntax(condition, content) { Span = default }).Array[0, 2],
                    Maybe(InlineContent(depth - 1, option).Select(content => new ElseTextSyntax(content) { Span = default })),
                    InlineSyntax (condition, content, elseIfs, @else) => new ConditionalTextSyntax(condition, content, elseIfs, @else) { Span = default }))),
            ]);
        }

        return Gen.Frequency([.. forms]);
    }

    /// <summary>Joins adjacent text runs, as the parser reads them as one.</summary>
    private static InlineSyntax[] Merge(InlineSyntax[] items)
    {
        List<InlineSyntax> merged = [];
        foreach (InlineSyntax item in items)
        {
            if (item is TextRunSyntax next && merged is [.., TextRunSyntax previous])
                merged[^1] = new TextRunSyntax(previous.Text + next.Text) { Span = default };
            else
                merged.Add(item);
        }

        return [.. merged];
    }

    private static InlineSyntax[] Trim(InlineSyntax[] items)
    {
        if (items is [TextRunSyntax first, ..])
            items = [new TextRunSyntax(first.Text.TrimStart(' ')) { Span = default }, .. items[1..]];

        if (items is [.., TextRunSyntax last])
            items = [.. items[..^1], new TextRunSyntax(last.Text.TrimEnd(' ')) { Span = default }];

        return [.. items.Where(item => item is not TextRunSyntax { Text.Length: 0 })];
    }

    private static Gen<(ArgumentSyntax[] Arguments, CommandWait Wait)> Arguments(int depth, bool allowWait) => Gen.Select(
        Value(depth).Select(value => new ArgumentSyntax(null, value) { Span = default }).Array[0, 2],
        Gen.Select(Identifier, Value(depth), (name, value) => new ArgumentSyntax(Name(name), value) { Span = default }).Array[0, 2],
        allowWait ? Gen.OneOfConst(CommandWait.Default, CommandWait.Wait, CommandWait.NoWait) : Gen.Const(CommandWait.Default),
        (positional, named, wait) => ((ArgumentSyntax[])[.. positional, .. named], wait));

    private static Gen<ExpressionSyntax> Expression(int depth) => Operand(0, depth);

    /// <summary>
    /// An expression that can stand where precedence <paramref name="level"/> is expected, from 0 (<c>or</c>) to 8 (a
    /// primary). Each operator's operands come from the levels the grammar allows them, so the printed form needs no
    /// parentheses beyond those the tree holds.
    /// </summary>
    private static Gen<ExpressionSyntax> Operand(int level, int depth)
    {
        if (depth == 0)
            return Value(0);

        List<(int, Gen<ExpressionSyntax>)> forms = [(4, Value(depth))];
        if (level <= 0)
            forms.Add((1, Binary(depth, 0, 1, BinaryOperator.Or)));
        if (level <= 1)
            forms.Add((1, Binary(depth, 1, 2, BinaryOperator.And)));
        if (level <= 2)
            forms.Add((1, Unary(depth, UnaryOperator.Not, 2)));
        if (level <= 3)
            forms.Add((1, Binary(depth, 4, 4, BinaryOperator.Equals, BinaryOperator.NotEquals)));
        if (level <= 4)
            forms.Add((1, Binary(depth, 5, 5, BinaryOperator.Less, BinaryOperator.LessOrEqual, BinaryOperator.Greater, BinaryOperator.GreaterOrEqual)));
        if (level <= 5)
            forms.Add((1, Binary(depth, 5, 6, BinaryOperator.Add, BinaryOperator.Subtract)));
        if (level <= 6)
            forms.Add((1, Binary(depth, 6, 7, BinaryOperator.Multiply, BinaryOperator.Divide, BinaryOperator.Remainder)));
        if (level <= 7)
            forms.Add((1, Unary(depth, UnaryOperator.Negate, 7)));

        return Gen.Frequency([.. forms]);
    }

    private static Gen<ExpressionSyntax> Binary(int depth, int left, int right, params BinaryOperator[] operators) => Gen.Select(
        Defer(() => Operand(left, depth - 1)),
        Gen.OneOfConst(operators),
        Defer(() => Operand(right, depth - 1)),
        ExpressionSyntax (l, @operator, r) => new BinaryExpressionSyntax(l, @operator, default, r) { Span = default });

    private static Gen<ExpressionSyntax> Unary(int depth, UnaryOperator @operator, int level) =>
        Defer(() => Operand(level, depth - 1)).Select(ExpressionSyntax (operand) => new UnaryExpressionSyntax(@operator, default, operand) { Span = default });

    /// <summary>A value that needs no operator: a literal, a variable, a bare name, a call or a parenthesized expression.</summary>
    private static Gen<ExpressionSyntax> Value(int depth)
    {
        List<Gen<ExpressionSyntax>> forms =
        [
            Literal(),
            Identifier.Select(ExpressionSyntax (name) => new VariableExpressionSyntax(name) { Span = default }),
            NodeName.Select(ExpressionSyntax (name) => new NameExpressionSyntax(name) { Span = default }),
        ];

        if (depth > 0)
        {
            forms.Add(Defer(() => Call(depth)).Select(ExpressionSyntax (call) => call));
            forms.Add(Defer(() => Expression(depth - 1)).Select(ExpressionSyntax (inner) => new ParenthesizedExpressionSyntax(inner) { Span = default }));
        }

        return Gen.OneOf([.. forms]);
    }

    private static Gen<CallExpressionSyntax> Call(int depth) => Gen.Select(Identifier, Expression(depth - 1).Array[0, 2],
        (function, arguments) => new CallExpressionSyntax(Name(function), arguments) { Span = default });

    private static Gen<ExpressionSyntax> Literal() => Gen.OneOf(
        Decimal.Select(ExpressionSyntax (value) => new NumberLiteralSyntax(value) { Span = default }),
        Decimal.Select(ExpressionSyntax (value) => new DurationLiteralSyntax(value) { Span = default }),
        Piece.Array[0, 6].Select(ExpressionSyntax (pieces) => new StringLiteralSyntax(string.Concat(pieces)) { Span = default }),
        Gen.Bool.Select(ExpressionSyntax (value) => new BooleanLiteralSyntax(value) { Span = default }));
}
