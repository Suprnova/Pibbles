using System.Text;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Readability: long text, names that aren't <c>snake_case</c>, uneven indentation and speaker spacing.</summary>
internal sealed partial class StyleChecks
{
    /// <summary>PIB5030 and PIB5034: a line's longest page, or an option's text, past the file's limit.</summary>
    private void CheckLength(IReadOnlyList<InlineSyntax> content, bool option)
    {
        if (content.Count == 0)
            return;

        int length = option ? TextLength(content) : LongestPage(content);
        int limit = Settings.Threshold(option ? "pibbles_max_option_length" : "pibbles_max_message_length");
        if (length <= limit)
            return;

        Report(option ? DiagnosticCatalog.LongOption : DiagnosticCatalog.LongLine, SpanOf(content), length, limit);
    }

    /// <summary>The length of a line's longest page: the text between page breaks.</summary>
    private static int LongestPage(IReadOnlyList<InlineSyntax> content)
    {
        int longest = 0;
        int page = 0;
        foreach (InlineSyntax item in content)
        {
            if (item is PageBreakSyntax)
            {
                longest = Math.Max(longest, page);
                page = 0;
            }
            else
            {
                page += TextLength(item);
            }
        }

        return Math.Max(longest, page);
    }

    private static int TextLength(IEnumerable<InlineSyntax> content) => content.Sum(TextLength);

    /// <summary>
    /// How many characters an item shows. Markup and pacing show none of their own, an icon counts as one, conditional
    /// text counts its longest branch, and a value shown with <c>{…}</c> counts nothing, since its length isn't known.
    /// </summary>
    private static int TextLength(InlineSyntax item) => item switch
    {
        TextRunSyntax run => run.Text.Length,
        IconSyntax => 1,
        MarkupSyntax markup => TextLength(markup.Content),
        ConditionalTextSyntax conditional => SyntaxWalk.Branches(conditional).Max(branch => TextLength(branch)),
        _ => 0,
    };

    /// <summary>PIB5031: the names a file declares, and its prefix and node names, written with a capital letter.</summary>
    private void CheckNames()
    {
        IEnumerable<(TextSpan Span, string Written)> names =
        [
            .. Tree.Root.Prefix is { } prefix ? [(prefix.Name.Span, prefix.Name.Text)] : Array.Empty<(TextSpan, string)>(),
            .. Tree.Root.Declarations.SelectMany(DeclaredNames),
            .. Tree.Root.Nodes.Select(node => (node.Name.Span, node.Name.Text)),
        ];

        foreach ((TextSpan span, string written) in names.Where(name => name.Written.Any(char.IsAsciiLetterUpper)))
            Report(DiagnosticCatalog.NamingConvention, span, written, SnakeCase(written));
    }

    private static IEnumerable<(TextSpan Span, string Written)> DeclaredNames(DeclarationSyntax declaration)
    {
        IEnumerable<NameSyntax> names = declaration switch
        {
            ActorDeclarationSyntax actor => [actor.Name, .. actor.Channels.SelectMany(edit => edit.Values)],
            ChannelDeclarationSyntax channel => [channel.Name, .. channel.Values],
            EnumDeclarationSyntax @enum => [@enum.Name, .. @enum.Members],
            CommandDeclarationSyntax command => [command.Name],
            MarkupDeclarationSyntax markup => [markup.Name],
            IconDeclarationSyntax icon => icon.Names,
            FunctionDeclarationSyntax function => [function.Name],
            _ => [],
        };

        return declaration is VariableDeclarationSyntax variable
            ? [(variable.Variable.Span, $"${variable.Variable.Name}")]
            : names.Select(name => (name.Span, name.Text));
    }

    /// <summary>Writes a name in <c>snake_case</c>: <c>FrontDoor</c> is <c>front_door</c>, and <c>HTTPServer</c> is <c>http_server</c>.</summary>
    private static string SnakeCase(string name)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool startsWord = i > 0 && char.IsAsciiLetterUpper(c)
                && (char.IsAsciiLetterLower(name[i - 1]) || char.IsAsciiDigit(name[i - 1])
                    || char.IsAsciiLetterUpper(name[i - 1]) && i + 1 < name.Length && char.IsAsciiLetterLower(name[i + 1]));
            if (startsWord)
                builder.Append('_');

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// PIB5032: how far each block indents past the one it's in. Every block should match <c>indent_size</c> in
    /// <c>.editorconfig</c>, or else the file's first block. A file that indents with the character <c>indent_style</c>
    /// doesn't ask for is reported once. Lines that mix tabs and spaces are PIB1001's, so they're skipped.
    /// </summary>
    private void CheckIndentation()
    {
        Stack<int> widths = new([0]);
        char? character = null;
        (int Width, string Reason)? expected = null;
        for (int line = 0; line < Tree.Source.LineCount; line++)
        {
            if (!HoldsContent(line))
                continue;

            TextSpan span = Tree.Source.GetLineSpan(line);
            string text = Tree.Source.Text.Substring(span.Start, span.Length);
            string indentation = text[..(text.Length - text.TrimStart(' ', '\t').Length)];
            if (indentation.Length == 0)
            {
                widths = new([0]);
                continue;
            }

            var indentationSpan = new TextSpan(span.Start, indentation.Length);
            character ??= indentation[0];
            if (indentation.AsSpan().ContainsAnyExcept(character.Value))
                continue;

            if (Settings.IndentWith() is { } style && style != (character is '\t' ? "tabs" : "spaces"))
            {
                Report(DiagnosticCatalog.IndentationWidth, indentationSpan, $"with {(character is '\t' ? "tabs" : "spaces")}", $"`indent_style` in `.editorconfig` asks for {style}", $"with {style}");
                return;
            }

            while (indentation.Length < widths.Peek())
                widths.Pop();

            if (indentation.Length <= widths.Peek())
                continue;

            int step = indentation.Length - widths.Peek();
            widths.Push(indentation.Length);
            expected ??= character is ' ' && Settings.IndentSize is { } size
                ? (size, $"`indent_size` in `.editorconfig` asks for {Width(size, ' ')}")
                : (step, $"the file's first block is indented {Width(step, character.Value)}");

            if (step != expected.Value.Width)
                Report(DiagnosticCatalog.IndentationWidth, indentationSpan, Width(step, character.Value), expected.Value.Reason, Width(expected.Value.Width, character.Value));
        }
    }

    private static string Width(int count, char character) => (count, character) switch
    {
        (1, ' ') => "1 space",
        (_, ' ') => $"{count} spaces",
        (1, _) => "1 tab",
        _ => $"{count} tabs",
    };

    /// <summary>
    /// PIB5033: a speaker written other than <c>name:</c> or <c>name (a, b):</c>, or <c>name：</c> or <c>name（a、b）：</c> when its
    /// colon is full-width.
    /// </summary>
    private void CheckSpeakerSpacing(TextLineSyntax line)
    {
        if (line.Speaker is not { } speaker)
            return;

        TextSpan lineSpan = Tree.Source.GetLineSpan(LineOf(speaker.Span.Start));
        if (SpeakerScanner.Scan(Tree.Source.Text, speaker.Span.Start, lineSpan.End) is not { IsSpeaker: true } prefix)
            return;

        (string gap, string open, string separator, string close, string colon) = Tree.Source.Text[prefix.Colon] is '：'
            ? ("", "（", "、", "）", "：")
            : (" ", "(", ", ", ")", ":");
        string values = speaker.Values.Count > 0 ? $"{open}{string.Join(separator, speaker.Values.Select(value => value.Text))}{close}" : "";
        string usual = $"{speaker.Name?.Text}{gap}{values}".Trim() + colon;
        var written = new TextSpan(speaker.Span.Start, prefix.Colon + 1 - speaker.Span.Start);
        if (TextOf(written) != usual)
            Report(DiagnosticCatalog.SpeakerSpacing, written, usual);
    }
}
