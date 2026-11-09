using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Resolves the names in a story against its <see cref="SymbolTable"/>: speakers and their values, commands, markup, icons,
/// tags, functions, variables and nodes. It checks every argument against its parameter, and the type of every
/// expression: in variables' values, parameters' defaults, conditions, arguments, <c>@set</c>, <c>@pause</c> and text.
/// </summary>
/// <remarks>
/// An expression is bound with the type expected where it appears, which is how a bare name is read: as a member of the
/// expected enum, an actor or a node. Anything whose type can't be known gets <see cref="TypeSymbol.Error"/>, which
/// converts to and from every type, so each problem is reported once.
/// </remarks>
internal sealed partial class Binder(SymbolTable symbols, List<Diagnostic> diagnostics, ReferenceIndex references, Bindings bindings) : AnalysisPass(diagnostics, references)
{
    /// <summary>Binds every file's declarations and node bodies against the symbol table.</summary>
    /// <param name="trees">The story's files.</param>
    /// <param name="symbols">The symbol table the declaration pass made.</param>
    /// <param name="diagnostics">Where problems with names, types and arguments are reported.</param>
    /// <param name="references">Where each name that refers to a symbol is recorded.</param>
    /// <param name="bindings">Where each name, expression and argument is bound.</param>
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

                case PauseStatementSyntax pause:
                    BindPacing(pause.Duration, TypeSymbol.Duration, "`@pause` takes a `duration`", "The time to `@pause`", "Write a time longer than zero, like `@pause 0.5s`.");
                    break;
            }
        }
    }

    private void BindTextLine(TextLineSyntax line)
    {
        if (line.Speaker is { Name: { } name } speaker)
        {
            if (symbols.Actors.TryGetValue(name.Text, out ActorSymbol? actor))
            {
                Refers(name, actor);
                BindValues(speaker.Values, actor.Channels, actor.DisplayName, line);
            }
            else
            {
                string meant = DiagnosticCatalog.DidYouMean(Suggestions.Closest(name.Text, symbols.Actors.Keys));
                Report(DiagnosticCatalog.UnknownSpeaker, name.Span, name.Text, meant, EscapeColon(PrefixOf(line)));
            }
        }
        else if (line.Speaker is { } narration)
        {
            BindValues(narration.Values, symbols.Narration.Channels, "Narration", line);
        }
        else
        {
            CheckNarration(line);
        }

        BindInline(line.Content);
        BindTags(line.Tags);
    }

    /// <summary>Binds each value in a speaker's parenthesis to the channel that has it, and reports a second value of one channel.</summary>
    private void BindValues(IReadOnlyList<NameSyntax> written, IReadOnlyList<SpeakerChannel> channels, string owner, TextLineSyntax line)
    {
        Dictionary<ChannelSymbol, NameSyntax>? set = null;
        foreach (NameSyntax value in written)
        {
            if (FindValue(channels, value.Text) is not var (channel, known))
            {
                ReportUnknownValue(channels, owner, value, line);
                continue;
            }

            Refers(value, known);
            set ??= [];
            if (set.TryGetValue(channel, out NameSyntax? first))
                Report(DiagnosticCatalog.TwoValuesInChannel, value.Span, first.Text, value.Text, owner, channel.Name);
            else
                set.Add(channel, value);
        }
    }

    /// <summary>Finds the value a speaker's parenthesis names, and the channel that has it.</summary>
    private static (ChannelSymbol Channel, ChannelValueSymbol Value)? FindValue(IReadOnlyList<SpeakerChannel> channels, string name)
    {
        foreach (SpeakerChannel has in channels)
        {
            foreach (ChannelValueSymbol known in has.Values)
            {
                if (known.Name == name)
                    return (has.Channel, known);
            }
        }

        return null;
    }

    private void ReportUnknownValue(IReadOnlyList<SpeakerChannel> channels, string owner, NameSyntax value, TextLineSyntax line)
    {
        string declaration = line.Speaker is { Name: { } actor } ? $"@actor {actor.Text}" : "@narration";
        string[] values = [.. channels.SelectMany(channel => channel.Values).Select(known => known.Name)];
        ChannelSymbol? dropped = symbols.Channels.Values.FirstOrDefault(channel => channels.All(has => has.Channel != channel) && channel.Defaults.Any(known => known.Name == value.Text));
        string help = dropped is not null ? $"{owner} has no `{dropped.Name}`, which has `{value.Text}`."
            : Suggestions.Closest(value.Text, values) is { } suggestion ? DiagnosticCatalog.DidYouMean(suggestion).TrimEnd()
            : values.Length switch
            {
                0 when symbols.Channels.Keys.FirstOrDefault() is { } channel => $"{owner} has no values to set. List {owner}'s values under `{declaration}`: `{channel}: neutral, happy`.",
                0 => $"{owner} has no values to set. Declare a channel such as `@channel pose`, then list {owner}'s values under `{declaration}`: `pose: neutral, happy`.",
                1 => $"{owner}'s only value is {Phrase.And(values)}.",
                _ => $"{owner}'s values are {Phrase.And(values)}.",
            };
        if (line.Speaker is { Name: null })
            help += $" If this is text, put a backslash before the `(`: `\\{PrefixOf(line)}`.";

        Report(DiagnosticCatalog.UnknownChannelValue, value.Span, owner, value.Text, help);
    }

    /// <summary>
    /// Reports narration that almost looks like a speaker, so a mistake never shows as narration: a parenthesis that
    /// isn't a list of names, anything between a declared actor's values and the colon, a word between a declared actor
    /// and the colon, or a declared actor with no space after the colon.
    /// </summary>
    private void CheckNarration(TextLineSyntax line)
    {
        if (SpeakerScanner.Scan(Tree.Source.Text, line.Span.Start, line.Span.End) is not { } prefix)
            return;

        string written = PrefixOf(line, prefix);
        string escaped = EscapeColon(written);
        if (prefix is { Parentheses: { } parentheses, Values: [] })
        {
            Report(DiagnosticCatalog.NotValues, parentheses, TextOf(parentheses), escaped);
        }
        else if (prefix is { Extra: { } extra, Parentheses: { } valuesParentheses } && IsActor(prefix))
        {
            Report(DiagnosticCatalog.AfterValues, extra, TextOf(extra), TextOf(valuesParentheses), escaped);
        }
        else if (prefix is { Extra: { } word, Name: { } actor, Parentheses: null } && IsActor(prefix))
        {
            Report(DiagnosticCatalog.WordBeforeColon, word, TextOf(word), TextOf(actor), escaped);
        }
        else if (IsActor(prefix))
        {
            int textEnd = line.Tags.Count > 0 ? line.Tags[0].Span.Start : line.Span.End;
            string after = TextOf(new(prefix.Colon + 1, textEnd - prefix.Colon - 1)).TrimEnd();
            Report(DiagnosticCatalog.NoSpaceAfterSpeaker, new(line.Span.Start, prefix.Colon + 1 - line.Span.Start), written[..^1], $"{escaped}{after}");
        }
    }

    private bool IsActor(SpeakerPrefix prefix) => prefix.Name is { } name && symbols.Actors.ContainsKey(TextOf(name));

    /// <summary>A text line's start through its speaker's colon, as written.</summary>
    private string PrefixOf(TextLineSyntax line) =>
        SpeakerScanner.Scan(Tree.Source.Text, line.Span.Start, line.Span.End) is { } prefix ? PrefixOf(line, prefix) : "";

    private string PrefixOf(TextLineSyntax line, SpeakerPrefix prefix) => TextOf(new(line.Span.Start, prefix.Colon + 1 - line.Span.Start));

    /// <summary><paramref name="prefix"/>, which ends in the colon, with a backslash before the colon.</summary>
    private static string EscapeColon(string prefix) => $"{prefix[..^1]}\\{prefix[^1]}";

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

                case PauseSyntax pause:
                    BindPacing(pause.Duration, TypeSymbol.Duration, "`{pause}` takes a `duration`", "A pause", "Write a pause longer than zero, like `{pause 0.5}`.");
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
            BindArguments(symbol, symbol.Parameters, markup.Arguments, markup.Name.Span, named: true);
        }
        else
        {
            if (!markup.Name.IsMissing)
                ReportWithOptionalHelp(DiagnosticCatalog.UnknownMarkup, markup.Name.Span, markup.Name.Text, SuggestedMarkup(markup.Name.Text));

            BindUnmatched(markup.Arguments);
        }

        BindInline(markup.Content);
    }

    private string? SuggestedMarkup(string name) =>
        name is "speed" ? "{speed}" : Suggestions.Closest(name, symbols.Markup.Keys);

    /// <summary>A value shown in text, which is text, a number formatted for the player's language, or an actor's display name.</summary>
    private void BindInterpolation(InterpolationSyntax interpolation)
    {
        TypeSymbol type = Bind(interpolation.Value);
        if (type == TypeSymbol.Error || type == TypeSymbol.String || type == TypeSymbol.Number || type == TypeSymbol.Actor)
            return;

        string text = TextOf(interpolation.Value.Span);
        string help = type == TypeSymbol.Bool ? $"Show text that depends on it instead: `{{if {text}}}…{{else}}…{{/if}}`."
            : type is EnumSymbol { Members: [var first, ..] } ? $"Show text that depends on it instead, like `{{if {text} == {first.Name}}}…{{/if}}`."
            : "Show text that depends on it with `{if …}…{/if}`, or call a function that returns text.";
        Report(DiagnosticCatalog.NotShowable, interpolation.Span, text, type.Describe(), help);
    }

    private string? SuggestedCommand(string name, bool inline) =>
        name is "wait" && !inline ? "pause" : Suggestions.Closest(name, symbols.Commands.Keys);

    private void BindCommand(NameSyntax name, IReadOnlyList<ArgumentSyntax> arguments, TextSpan span, bool inline)
    {
        if (!symbols.Commands.TryGetValue(name.Text, out CommandSymbol? command))
        {
            if (!name.IsMissing)
                ReportWithOptionalHelp(DiagnosticCatalog.UnknownCommand, name.Span, name.Text, SuggestedCommand(name.Text, inline));

            BindUnmatched(arguments);
            return;
        }

        Refers(name, command);
        if (inline && !command.IsInline)
            Report(DiagnosticCatalog.NotInline, name.Span, name.Text);

        BindArguments(command, command.Parameters, arguments, span, named: true);
    }

    private void BindTags(IEnumerable<TagSyntax> tags)
    {
        foreach (TagSyntax tag in tags.Where(tag => tag.Name is not ("id" or "was")))
        {
            if (!symbols.Tags.TryGetValue(tag.Name, out TagSymbol? symbol))
            {
                string meant = DiagnosticCatalog.DidYouMean(Suggestions.Closest(tag.Name, symbols.Tags.Keys) is { } closest ? $"#{closest}" : null);
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

        if (type == TypeSymbol.Error)
            return;

        if (tag.Value is null || (tag.Value.Length == 0 && !symbol.AllowsEmpty))
        {
            string example = type is EnumSymbol { Members: [var first, ..] } ? first.Name : "value";
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
        string help = Suggestions.Closest(tag.Value, members) is { } closest ? DiagnosticCatalog.DidYouMean($"#{tag.Name}:{closest}").TrimEnd() : $"Use one of {Phrase.Or(members)}.";
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
        string? meant = type == TypeSymbol.Number ? $"{text} > 0" : type == TypeSymbol.String ? $"{text} != \"\"" : null;
        ReportWithOptionalHelp(DiagnosticCatalog.NotCondition, condition.Span, text, type.Describe(), meant);
    }
}
