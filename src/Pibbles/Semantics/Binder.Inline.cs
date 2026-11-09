using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Inline content: text with markup, interpolations, inline commands and tags.</summary>
internal sealed partial class Binder
{
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
}
