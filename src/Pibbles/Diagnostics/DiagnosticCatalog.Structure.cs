namespace Pibbles.Diagnostics;

public static partial class DiagnosticCatalog
{
    /// <summary>PIB1001: a line's indentation uses a different character than the rest of the file's.</summary>
    /// <remarks>Arguments: what this line indents with, and what the file indents with.</remarks>
    public static DiagnosticDescriptor MixedIndentation { get; } = new(
        "PIB1001",
        DiagnosticSeverity.Error,
        "This line indents with {0}, but the file indents with {1}, so I can't tell which block it belongs to.",
        "Use only spaces or only tabs in a file. Most editors can convert the whole file for you.");

    /// <summary>PIB1001's help when <c>indent_style</c> in <c>.editorconfig</c> says what the project indents with.</summary>
    /// <param name="indent"><c>spaces</c> or <c>tabs</c>.</param>
    internal static string MixedIndentationHelpWithStyle(string indent) =>
        $"Use only {indent} in this file, since `indent_style` in `.editorconfig` asks for {indent}. Most editors can convert the whole file for you.";

    /// <summary>PIB1002: a line is indented less than the line above it, but not as far back as any block it could belong to.</summary>
    public static DiagnosticDescriptor InconsistentIndentation { get; } = new(
        "PIB1002",
        DiagnosticSeverity.Error,
        "I can't tell which block this line belongs to: its indentation doesn't line up with any block above it.",
        help: "Indent it to match the block it belongs to.");

    /// <summary>PIB1003: an indented line under a line that can't have a block.</summary>
    public static DiagnosticDescriptor UnexpectedIndentation { get; } = new(
        "PIB1003",
        DiagnosticSeverity.Error,
        "This line is indented, but the line above it doesn't open a block.",
        help: "Only a block opener such as `@if`, an option (`->`) or an alternative (`- `) can have indented lines under it.");

    /// <summary>PIB1004: a block opener with no indented lines under it.</summary>
    /// <remarks>Argument: the opener's line, such as <c>@if $door_open</c>.</remarks>
    public static DiagnosticDescriptor MissingBlock { get; } = new(
        "PIB1004",
        DiagnosticSeverity.Error,
        "I expected indented lines under `{0}`.",
        help: "Put the lines it controls below it, indented.");

    /// <summary>PIB1005: a <c>///</c> note, anywhere on a line, which v1 reserves for localization.</summary>
    public static DiagnosticDescriptor UnsupportedNote { get; } = new(
        "PIB1005",
        DiagnosticSeverity.Error,
        "`///` notes aren't supported yet.",
        help: "For a comment, use `//`.");

    /// <summary>PIB1006: a line starts with a space separator other than a space or a tab, which doesn't indent.</summary>
    /// <remarks>Argument: the first such character, such as <c>U+3000 (an ideographic space)</c>.</remarks>
    public static DiagnosticDescriptor NonIndentingSpace { get; } = new(
        "PIB1006",
        DiagnosticSeverity.Error,
        "This line starts with {0}, which doesn't indent.",
        help: "Indent with spaces or tabs. A space at the start of a line doesn't show in the game, since Pibbles trims it, so delete it if you only meant to space the text.");

    /// <summary>PIB1007: the text starts with a byte-order mark, which the host should have removed when it decoded the file.</summary>
    public static DiagnosticDescriptor ByteOrderMark { get; } = new(
        "PIB1007",
        DiagnosticSeverity.Error,
        "This file starts with a byte-order mark (U+FEFF), which isn't part of the text.",
        help: "Delete this first character, or save the file as UTF-8 without a byte-order mark. A program that reads story files should decode them as UTF-8, which removes it.");

    /// <summary>PIB1008: a declaration inside a node, which for a kind other than <c>@var</c> is any declaration after the file's first node.</summary>
    /// <remarks>Arguments: the declaration's keyword, and where it can go instead.</remarks>
    public static DiagnosticDescriptor DeclarationInNode { get; } = new(
        "PIB1008",
        DiagnosticSeverity.Error,
        "`{0}` can't go inside a node.",
        help: "{1}");

    /// <summary>PIB1009: a statement before the file's first node.</summary>
    public static DiagnosticDescriptor OutsideNode { get; } = new(
        "PIB1009",
        DiagnosticSeverity.Error,
        "This line isn't inside a node.",
        help: "Add a node header above it: `== name`.");

    /// <summary>PIB1010: blocks, spans, branches, brackets or chained operators nested past the limit the parser reads.</summary>
    /// <remarks>Argument: the limit.</remarks>
    public static DiagnosticDescriptor NestingLimit { get; } = new(
        "PIB1010",
        DiagnosticSeverity.Error,
        "I can't read anything nested more than {0} levels deep.",
        help: "Move part of it elsewhere: a block into a node of its own, or part of an expression into a variable set on an earlier line.");

    /// <summary>PIB1101: a backslash followed by something other than ASCII punctuation.</summary>
    /// <remarks>Argument: the backslash and the character after it.</remarks>
    public static DiagnosticDescriptor InvalidEscape { get; } = new(
        "PIB1101",
        DiagnosticSeverity.Error,
        "I don't know the escape `{0}`.",
        help: @"A backslash only goes before punctuation. To show a backslash, write `\\`.");

    /// <summary>
    /// PIB1102: text that looks like a marker typed through an input method, such as <c>【b】…【/b】</c> for markup or
    /// <c>・・</c> at a line's start for a comment. It stays text.
    /// </summary>
    /// <remarks>Arguments: the text as written; the marker it looks like, half-width; and how to fix it, which also offers the escape.</remarks>
    public static DiagnosticDescriptor MarkerLookalike { get; } = new(
        "PIB1102",
        DiagnosticSeverity.Warning,
        "I show `{0}` as text, but it looks like `{1}` typed through an input method.",
        help: "{2}");

    /// <summary>PIB1103: something the grammar doesn't allow where it appears, when no more specific code applies.</summary>
    /// <remarks>Arguments: what was found, such as <c>`extra`</c> or <c>a node header</c>; and how to fix it, such as <see cref="EscapeHelp"/>'s sentence, or <see langword="null"/> for no help.</remarks>
    public static DiagnosticDescriptor Unexpected { get; } = new(
        "PIB1103",
        DiagnosticSeverity.Error,
        "I didn't expect {0} here.",
        help: "{1}");

    /// <summary>PIB1104: a quoted string with no closing quote on its line.</summary>
    public static DiagnosticDescriptor UnterminatedString { get; } = new(
        "PIB1104",
        DiagnosticSeverity.Error,
        "This quoted text never ends.",
        help: "Add the closing `\"` on the same line.");

    /// <summary>PIB1105: a number followed directly by letters, digits, <c>_</c> or another <c>.</c>.</summary>
    /// <remarks>Argument: the whole malformed number.</remarks>
    public static DiagnosticDescriptor MalformedNumber { get; } = new(
        "PIB1105",
        DiagnosticSeverity.Error,
        "`{0}` isn't a number I can read.",
        help: "Write numbers with digits and at most one `.`, like `3`, `0.5` or `.5`. A duration ends in `s` or `ms`, like `0.5s`.");

    /// <summary>PIB1106: a marker typed through an input method, such as <c>＝</c> for <c>=</c> or <c>」</c> for <c>]</c>, read as the marker.</summary>
    /// <remarks>
    /// Arguments: the form as written, one character or a run of them; their code points, such as <c>U+FF1D</c>; the marker it's
    /// read as; and how to fix it, which is the line with every such form on it replaced by its marker, followed for a marker
    /// that starts a line by <see cref="EscapeHelp"/>'s sentence.
    /// </remarks>
    public static DiagnosticDescriptor InputMethodForm { get; } = new(
        "PIB1106",
        DiagnosticSeverity.Error,
        "I read `{0}` ({1}) as `{2}`, but markers are typed half-width.",
        help: "{3}");

    /// <summary>PIB1107: a required part of a line is missing.</summary>
    /// <remarks>Arguments: what's missing, such as <c>a node name</c>, and the text it should follow.</remarks>
    public static DiagnosticDescriptor Missing { get; } = new(
        "PIB1107",
        DiagnosticSeverity.Error,
        "I expected {0} after `{1}`.");

    /// <summary>PIB1108: a number or duration too large to hold, past about 7.9 × 10^28.</summary>
    /// <remarks>Argument: the number as written.</remarks>
    public static DiagnosticDescriptor NumberTooLarge { get; } = new(
        "PIB1108",
        DiagnosticSeverity.Error,
        "`{0}` is too large of a number for me.",
        help: "Numbers go up to 79228162514264337593543950335. Write a smaller one.");

    /// <summary>PIB1201: text after a tag, which ends the line's text.</summary>
    /// <remarks>
    /// Arguments: the tag the text comes after; whose text it is, such as <c>the text</c>, or a <see cref="SpeakerMention"/>
    /// that reads <c>what Mira says</c>; and
    /// the line with the tag escaped, which the help shows on a line of its own.
    /// </remarks>
    public static DiagnosticDescriptor TextAfterTag { get; } = new(
        "PIB1201",
        DiagnosticSeverity.Error,
        "I read `{0}` as a tag, but tags go at the end of the line.",
        "If `{0}` is part of {1}, put a backslash before the `#`:\n{2}");

    /// <summary>PIB1202: a tag name holding characters other than letters, digits and <c>_</c>.</summary>
    /// <remarks>
    /// Arguments: the tag as written; and how to fix it, which is the same tag with each such character replaced by <c>_</c>,
    /// followed on a line of its own by <see cref="EscapeHelp"/>'s sentence where the tag may be text.
    /// </remarks>
    public static DiagnosticDescriptor MalformedTagName { get; } = new(
        "PIB1202",
        DiagnosticSeverity.Error,
        "`{0}` isn't a tag name: tag names only have letters, digits and `_`.",
        help: "{1}");

    /// <summary>PIB1203: a tag on a line that doesn't take it.</summary>
    /// <remarks>Arguments: the tag, the kind of line it's on, and what that line takes instead.</remarks>
    public static DiagnosticDescriptor TagNotAllowed { get; } = new(
        "PIB1203",
        DiagnosticSeverity.Error,
        "`{0}` can't go on {1}.",
        help: "{2}");

    /// <summary>PIB1204: a text line of only tags, with no speaker and no text for them to go with.</summary>
    /// <remarks>Argument: the tags as written.</remarks>
    public static DiagnosticDescriptor TagsOnlyLine { get; } = new(
        "PIB1204",
        DiagnosticSeverity.Error,
        "`{0}` has no line to go with.",
        help: "Tags go at the end of the line they're for. Move them onto it.");

    /// <summary>PIB1205: a tag that's neither reserved nor declared.</summary>
    /// <remarks>Arguments: the tag's name, and <see cref="DidYouMean"/>'s sentence for the closest tag, or nothing.</remarks>
    public static DiagnosticDescriptor UnknownTag { get; } = new(
        "PIB1205",
        DiagnosticSeverity.Error,
        "I don't know a tag called `#{0}`.",
        help: "{1}If this is text, escape it: `\\#{0}`.");

    /// <summary>PIB1206: a value on a tag that takes none, or none on a tag that needs one.</summary>
    /// <remarks>Arguments: the tag's name; <c>doesn't take a value</c> or <c>needs a value</c>; and the tag written the right way.</remarks>
    public static DiagnosticDescriptor TagValuePresence { get; } = new(
        "PIB1206",
        DiagnosticSeverity.Error,
        "`#{0}` {1}.",
        help: "{2}");

    /// <summary>PIB1207: a tag's value that isn't a member of the enum the tag takes.</summary>
    /// <remarks>Arguments: the value; the enum with its article; the tag's name; and a suggestion or the enum's members, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor TagValueNotMember { get; } = new(
        "PIB1207",
        DiagnosticSeverity.Error,
        "`{0}` isn't {1}, which `#{2}` takes.",
        help: "{3}");

    /// <summary>PIB1208: a tag whose value type is neither <c>string</c> nor an enum.</summary>
    /// <remarks>Argument: the type.</remarks>
    public static DiagnosticDescriptor TagValueType { get; } = new(
        "PIB1208",
        DiagnosticSeverity.Error,
        "A tag's value is text, so it can't be `{0}`.",
        help: "Use `string`, or an enum to allow only certain values.");

    /// <summary>PIB1301: an <c>#id</c> whose value isn't a line ID.</summary>
    /// <remarks>Argument: the tag as written.</remarks>
    public static DiagnosticDescriptor InvalidLineId { get; } = new(
        "PIB1301",
        DiagnosticSeverity.Error,
        "`{0}` isn't a line ID I can use.",
        help: "Line IDs are lowercase letters, digits and `_`, starting with a letter. `pibbles ids` replaces it with a new one.");

    /// <summary>PIB1302: a line that needs a line ID and has none.</summary>
    public static DiagnosticDescriptor MissingLineId { get; } = new(
        "PIB1302",
        DiagnosticSeverity.Warning,
        "This line has no `#id`.",
        help: "Run `pibbles ids` to add one.");

    /// <summary>PIB1303: a line ID that another line already uses.</summary>
    /// <remarks>Arguments: the ID, and where it's already used, such as <c>on line 3</c>.</remarks>
    public static DiagnosticDescriptor DuplicateLineId { get; } = new(
        "PIB1303",
        DiagnosticSeverity.Error,
        "The line ID `{0}` is also used {1}.",
        help: "Delete one of the two IDs and run `pibbles ids` to give that line a new one.");

    /// <summary>PIB1304: a line ID that's also a node's name or old name.</summary>
    /// <remarks>Arguments: the ID, and what it also is, such as <c>the name of a node</c>.</remarks>
    public static DiagnosticDescriptor LineIdIsNodeName { get; } = new(
        "PIB1304",
        DiagnosticSeverity.Error,
        "The line ID `{0}` is also {1}.",
        help: "Delete the ID and run `pibbles ids` to give the line a new one.");

    /// <summary>PIB1401: a dotted or relative name where a single identifier is being declared.</summary>
    /// <remarks>Argument: the name as written.</remarks>
    public static DiagnosticDescriptor DottedName { get; } = new(
        "PIB1401",
        DiagnosticSeverity.Error,
        "I can't use `{0}` as a name here, because only node names can have dots.",
        help: "Use a new name, with `_` between words if it needs them.");

    /// <summary>PIB1402: a declared name that another declaration of the same kind already uses.</summary>
    /// <remarks>
    /// Arguments: the kind with its article, such as <c>an enum</c>; the name; where the first one is, such as <c>on line 3</c>;
    /// and the help, which for a variable says that the two are different variables instead of offering to remove one.
    /// </remarks>
    public static DiagnosticDescriptor DuplicateDeclaration { get; } = new(
        "PIB1402",
        DiagnosticSeverity.Error,
        "There's already {0} called `{1}`, {2}.",
        help: "{3}");

    /// <summary>PIB1403: a channel value, enum member, parameter or channel listed twice in one declaration.</summary>
    /// <remarks>Arguments: the name, and the list it's in, such as <c>Mira's `pose` values</c>.</remarks>
    public static DiagnosticDescriptor RepeatedName { get; } = new(
        "PIB1403",
        DiagnosticSeverity.Error,
        "`{0}` is already one of {1}.",
        help: "Remove the second `{0}`, or give it another name.");

    /// <summary>PIB1404: a declared name that's a reserved word for its kind.</summary>
    /// <remarks>Arguments: the name; the kind with its article, such as <c>a command</c>; and where Pibbles uses the word, such as <c>after `@`</c>.</remarks>
    public static DiagnosticDescriptor ReservedName { get; } = new(
        "PIB1404",
        DiagnosticSeverity.Error,
        "`{0}` can't name {1}, because I use that word {2}.",
        help: "Choose another name.");

    /// <summary>PIB1405: a declared name with a character outside ASCII.</summary>
    /// <remarks>Arguments: the name, and its first character outside ASCII.</remarks>
    public static DiagnosticDescriptor NonAsciiName { get; } = new(
        "PIB1405",
        DiagnosticSeverity.Error,
        "`{0}` has `{1}` in it, but a name can only use English letters, digits and `_`.",
        help: "Replace `{1}` with a letter from a to z.");

    /// <summary>PIB1406: a type that's neither built in nor a declared enum.</summary>
    /// <remarks>Arguments: the type as written, and the closest type, which the help suggests. The help is left out when nothing is close.</remarks>
    public static DiagnosticDescriptor UnknownType { get; } = new(
        "PIB1406",
        DiagnosticSeverity.Error,
        "I don't know a type called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB1501: a node name that names no node.</summary>
    /// <remarks>Arguments: the name as written, and the closest node, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownNode { get; } = new(
        "PIB1501",
        DiagnosticSeverity.Error,
        "I can't find a node called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB1502: a node name or <c>#was:</c> name that another node, or the same one, already uses.</summary>
    /// <remarks>Arguments: the full name, and where it's already used, such as <c>on line 3</c>.</remarks>
    public static DiagnosticDescriptor DuplicateNode { get; } = new(
        "PIB1502",
        DiagnosticSeverity.Error,
        "There's already a node called `{0}`, {1}.",
        help: "A node's name, and each old name in its `#was:`, can only be used once in a story. Rename one of them.");

    /// <summary>PIB1503: a reference to a node by one of its old names.</summary>
    /// <remarks>Arguments: the old name, and the current name, both as the story would write them.</remarks>
    public static DiagnosticDescriptor OldNodeName { get; } = new(
        "PIB1503",
        DiagnosticSeverity.Warning,
        "`{0}` is an old name of `{1}`.",
        help: "Use the current name: `{1}`.");

    /// <summary>PIB1601: a file's only <c>@prefix</c> line, after something other than comments.</summary>
    public static DiagnosticDescriptor MisplacedPrefix { get; } = new(
        "PIB1601",
        DiagnosticSeverity.Error,
        "`@prefix` has to come first in the file.",
        help: "Move it to the top. Only comments can go above it.");

    /// <summary>PIB1602: a second <c>@prefix</c> line in a file.</summary>
    /// <remarks>Argument: the 1-based line of the file's first <c>@prefix</c>.</remarks>
    public static DiagnosticDescriptor DuplicatePrefix { get; } = new(
        "PIB1602",
        DiagnosticSeverity.Error,
        "This file already has a `@prefix`, on line {0}.",
        help: "A file has one prefix at most. To put nodes under two prefixes, split the file in two.");

    /// <summary>PIB1603: a relative node or variable name in a file with no <c>@prefix</c>.</summary>
    /// <remarks>Argument: the name as written, such as <c>.leave</c> or <c>$.attempts</c>.</remarks>
    public static DiagnosticDescriptor RelativeWithoutPrefix { get; } = new(
        "PIB1603",
        DiagnosticSeverity.Error,
        "`{0}` is relative, but this file has no `@prefix`.",
        help: "Write the full name, or add a `@prefix` at the top of the file.");
}
