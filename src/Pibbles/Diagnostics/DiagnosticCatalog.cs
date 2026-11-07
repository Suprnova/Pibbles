namespace Pibbles.Diagnostics;

/// <summary>Every diagnostic code Pibbles reports.</summary>
public static class DiagnosticCatalog
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

    /// <summary>The sentence a help offers when something close to what was written exists, such as <c>Did you mean `mira`? </c>.</summary>
    /// <param name="suggestion">The closest name, written as it is in a story (with its <c>#</c>, <c>$</c> or <c>@</c>), or <see langword="null"/> if none is close.</param>
    /// <returns>The sentence with a trailing space, so more text can follow it, or an empty string if there is no suggestion.</returns>
    internal static string DidYouMean(string? suggestion) => suggestion is null ? "" : $"Did you mean `{suggestion}`? ";

    /// <summary>The sentence a help offers when a <c>#</c> or <c>@</c> word may be text, such as <c>If `@home` is text, put a backslash before the `@`.</c>.</summary>
    /// <param name="word">The word as written.</param>
    /// <param name="escaped">The character to put the backslash before, if it isn't the word's first.</param>
    internal static string EscapeHelp(string word, char? escaped = null) => $"If `{word}` is text, put a backslash before the `{escaped ?? word[0]}`.";

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

    /// <summary>PIB2001: a second <c>name:</c> line in an <c>@actor</c> or <c>@narration</c> block.</summary>
    public static DiagnosticDescriptor RepeatedDisplayName { get; } = new(
        "PIB2001",
        DiagnosticSeverity.Error,
        "This block already has a `name:`.",
        help: "An actor has one display name. Keep one of the two lines.");

    /// <summary>PIB2002: the character U+FFFC in text, which Pibbles uses to stand for an icon.</summary>
    public static DiagnosticDescriptor ObjectReplacementCharacter { get; } = new(
        "PIB2002",
        DiagnosticSeverity.Error,
        "This text has U+FFFC in it, which I use internally to mark where an icon goes.",
        help: "Remove it. To show an icon, write `{{icon name}}`.");

    /// <summary>PIB2003: a speaker with neither values nor text.</summary>
    /// <remarks>Arguments: the speaker's name as written, and a <see cref="SpeakerMention"/> of it for the help's prose.</remarks>
    public static DiagnosticDescriptor EmptyLine { get; } = new(
        "PIB2003",
        DiagnosticSeverity.Error,
        "`{0}:` has nothing after it.",
        help: "Write what {1} says after the colon, or write `{0}: {{w}}` for a box with only their name. To set their values without a line, write `{0} (value):`.");

    /// <summary>PIB2004: a speaker that isn't a declared actor.</summary>
    /// <remarks>
    /// Arguments: the speaker as written; <see cref="DidYouMean"/>'s sentence for the closest actor, or nothing; and the line's
    /// start through its colon, with the colon escaped.
    /// </remarks>
    public static DiagnosticDescriptor UnknownSpeaker { get; } = new(
        "PIB2004",
        DiagnosticSeverity.Error,
        "I don't know an actor called `{0}`.",
        help: "{1}If this line is narration, escape the colon: `{2}`.");

    /// <summary>PIB2005: a value the speaker doesn't have.</summary>
    /// <remarks>Arguments: the actor's display name; the value; and a suggestion, the actor's values or where the value went, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownChannelValue { get; } = new(
        "PIB2005",
        DiagnosticSeverity.Error,
        "{0} has no value called `{1}`.",
        help: "{2}");

    /// <summary>PIB2006: a parenthesis after a name, before a colon, that doesn't hold a list of names, as in <c>mira (to Rex):</c>.</summary>
    /// <remarks>Arguments: the parenthesis, and the line's start through its colon, with the colon escaped.</remarks>
    public static DiagnosticDescriptor NotValues { get; } = new(
        "PIB2006",
        DiagnosticSeverity.Error,
        "`{0}` isn't a list of values: each value is a single name.",
        help: "For how a line is said, use a `//` comment. If this is narration, escape the colon: `{1}`.");

    /// <summary>PIB2007: a declared actor and a colon with no space after it, as in <c>mira:Hi</c>.</summary>
    /// <remarks>Arguments: the line's start up to its colon, and the line with its colon escaped.</remarks>
    public static DiagnosticDescriptor NoSpaceAfterSpeaker { get; } = new(
        "PIB2007",
        DiagnosticSeverity.Warning,
        "There's no space after `{0}:`.",
        help: "Add one, or escape the colon if this is narration: `{1}`.");

    /// <summary>PIB2008: more between a declared actor's values and the colon, as in <c>mira (smug) (shake):</c> or <c>mira (smug) shake:</c>.</summary>
    /// <remarks>Arguments: what comes after the values; the values' parenthesis; and the line's start through its colon, with the colon escaped.</remarks>
    public static DiagnosticDescriptor AfterValues { get; } = new(
        "PIB2008",
        DiagnosticSeverity.Error,
        "`{0}` has no place after `{1}`, before the colon.",
        help: "A speaker takes its values in one parenthesis and nothing else before the colon. For how a line is said, use a `//` comment. If this is narration, escape the colon: `{2}`.");

    /// <summary>PIB2009: two values of one channel on one line, as in <c>mira (mad, sad):</c>.</summary>
    /// <remarks>Arguments: the first value; the second value; the speaker's display name; and the channel.</remarks>
    public static DiagnosticDescriptor TwoValuesInChannel { get; } = new(
        "PIB2009",
        DiagnosticSeverity.Error,
        "`{0}` and `{1}` are both values of {2}'s `{3}`, and a line sets one value of a channel.",
        help: "Keep the one you mean.");

    /// <summary>PIB2010: a value that two of a speaker's channels share.</summary>
    /// <remarks>Arguments: the value; where it already is, such as <c>`effect`</c> or <c>Mira's `effect`</c>; and the channel that can't have it too.</remarks>
    public static DiagnosticDescriptor ValueInTwoChannels { get; } = new(
        "PIB2010",
        DiagnosticSeverity.Error,
        "`{0}` is already a value of {1}, so `{2}` can't have it too.",
        help: "A line names a value without its channel, so each value belongs to one channel. Give one of them another name.");

    /// <summary>PIB2011: a channel an actor's block names that isn't declared.</summary>
    /// <remarks>Arguments: the channel as written, and the closest declared channel, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownChannel { get; } = new(
        "PIB2011",
        DiagnosticSeverity.Error,
        "I don't know a channel called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2012: a second <c>@narration</c> in the story.</summary>
    /// <remarks>Argument: where the first one is, such as <c>on line 3</c>.</remarks>
    public static DiagnosticDescriptor SecondNarration { get; } = new(
        "PIB2012",
        DiagnosticSeverity.Error,
        "There's already an `@narration`, {0}.",
        help: "Narration has one block. Merge the two, or remove one.");

    /// <summary>PIB2101: a command, <c>{w}</c>, <c>{page}</c> or <c>{speed}</c> in an option's text.</summary>
    /// <remarks>Argument: the point, such as <c>{w}</c> or <c>{@jolt}</c>.</remarks>
    public static DiagnosticDescriptor NotInOption { get; } = new(
        "PIB2101",
        DiagnosticSeverity.Error,
        "An option's text can't contain `{0}`.",
        help: "Pauses, speed changes and commands go in the indented lines under the option.");

    /// <summary>PIB2102: an option with no text.</summary>
    public static DiagnosticDescriptor EmptyOption { get; } = new(
        "PIB2102",
        DiagnosticSeverity.Info,
        "This option has no text.",
        help: "Write what the player picks after the `->`.");

    /// <summary>PIB2201: an <c>@elif</c> or <c>@else</c> with no <c>@if</c> directly above it.</summary>
    /// <remarks>Argument: the keyword, <c>@elif</c> or <c>@else</c>.</remarks>
    public static DiagnosticDescriptor StrayClause { get; } = new(
        "PIB2201",
        DiagnosticSeverity.Error,
        "This `{0}` has no `@if` to belong to.",
        help: "Put it right after the `@if` block, at the same indentation as the `@if`.");

    /// <summary>PIB2202: a block opener with a <c>:</c> after it.</summary>
    /// <remarks>Arguments: the keyword, and the line up to the <c>:</c>.</remarks>
    public static DiagnosticDescriptor OpenerColon { get; } = new(
        "PIB2202",
        DiagnosticSeverity.Error,
        "I didn't expect a `:` on this `{0}` line.",
        help: "Remove it: `{1}`.");

    /// <summary>PIB2203: a condition that isn't a <c>bool</c>.</summary>
    /// <remarks>Arguments: the condition as written; its type with its article; and a condition that's probably meant, such as <c>$attempts &gt; 0</c>, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NotCondition { get; } = new(
        "PIB2203",
        DiagnosticSeverity.Error,
        "`{0}` is {1}, but a condition has to be true or false.",
        help: "Did you mean `{2}`?");

    /// <summary>PIB2301: a name where a variable is expected, written without its <c>$</c>.</summary>
    /// <remarks>Arguments: the name as written, and the line with a <c>$</c> before the name.</remarks>
    public static DiagnosticDescriptor MissingDollar { get; } = new(
        "PIB2301",
        DiagnosticSeverity.Error,
        "Variables start with `$`, so I need one before `{0}`.",
        help: "Write `{1}`.");

    /// <summary>PIB2302: a variable that isn't declared.</summary>
    /// <remarks>Arguments: the variable, with its <c>$</c>, and the closest variable, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownVariable { get; } = new(
        "PIB2302",
        DiagnosticSeverity.Error,
        "I don't know a variable called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2303: a value whose type isn't the type where it's used: a variable's value, a default, or <c>@pause</c>'s time.</summary>
    /// <remarks>
    /// Arguments: what the value is for, such as <c>`$has_key` holds a `bool`</c>; the value's type with its article; and
    /// what values to use instead, or <see langword="null"/> to leave out the help.
    /// </remarks>
    public static DiagnosticDescriptor ValueType { get; } = new(
        "PIB2303",
        DiagnosticSeverity.Error,
        "{0}, but this is {1}.",
        help: "{2}");

    /// <summary>PIB2304: a variable whose initial value is a name, with no type written.</summary>
    /// <remarks>
    /// Arguments: the variable, with its <c>$</c>; the initial value; and the declaration with its type written out. The
    /// help is left out when the value names no enum member, actor or node, or more than one kind of them.
    /// </remarks>
    public static DiagnosticDescriptor UntypedVariable { get; } = new(
        "PIB2304",
        DiagnosticSeverity.Error,
        "`{0}` starts as `{1}`, so I need its type written out.",
        help: "Write the type after the variable: `{2}`.");

    /// <summary>PIB2401: a statement after one that always leaves its block.</summary>
    /// <remarks>Arguments: why it never runs, such as <c>of the `@jump` above it</c>, and the statement that leaves, such as <c>@jump</c>.</remarks>
    public static DiagnosticDescriptor NeverRuns { get; } = new(
        "PIB2401",
        DiagnosticSeverity.Warning,
        "This line never runs, because {0}.",
        help: "Remove it, or move it above the `{1}`.");

    /// <summary>PIB2402: an <c>@if</c> or <c>@elif</c> condition that is always false.</summary>
    public static DiagnosticDescriptor ConditionNeverTrue { get; } = new(
        "PIB2402",
        DiagnosticSeverity.Warning,
        "This condition is never true, so the lines under it never run.",
        help: "Remove them, or write a condition that can change.");

    /// <summary>PIB2403: an <c>@if</c> or <c>@elif</c> condition that is always true, with branches after it.</summary>
    public static DiagnosticDescriptor ConditionAlwaysTrue { get; } = new(
        "PIB2403",
        DiagnosticSeverity.Warning,
        "This condition is always true, so the branches after it never run.",
        help: "Remove the branches after it, or write a condition that can change.");

    /// <summary>PIB2501: a line in a variation block that isn't an alternative.</summary>
    /// <remarks>Argument: the variation's keyword, such as <c>@cycle</c>.</remarks>
    public static DiagnosticDescriptor NotAlternative { get; } = new(
        "PIB2501",
        DiagnosticSeverity.Error,
        "A `{0}` block can only hold alternatives.",
        help: "Start each alternative with `- `.");

    /// <summary>PIB2502: a block opener right after an alternative's <c>- </c>.</summary>
    /// <remarks>Argument: the opener, such as <c>@if</c> or <c>-></c>.</remarks>
    public static DiagnosticDescriptor OpenerInAlternative { get; } = new(
        "PIB2502",
        DiagnosticSeverity.Error,
        "`{0}` can't start an alternative.",
        help: "Put `-` on its own line, and the `{0}` on the line below it, indented.");

    /// <summary>PIB2601: a positional argument after a named one.</summary>
    public static DiagnosticDescriptor ArgumentOrder { get; } = new(
        "PIB2601",
        DiagnosticSeverity.Error,
        "This argument has no name, but it comes after one that does.",
        help: "Put unnamed arguments first, then named ones, then `wait` or `nowait`.");

    /// <summary>PIB2602: a space between a function's name and its <c>(</c>, where only a call can follow the name.</summary>
    /// <remarks>Argument: the function's name.</remarks>
    public static DiagnosticDescriptor SpaceBeforeCall { get; } = new(
        "PIB2602",
        DiagnosticSeverity.Error,
        "There's a space between `{0}` and its `(`.",
        help: "To call `{0}`, remove the space: `{0}(…)`.");

    /// <summary>PIB2603: a negative number as a command argument, without parentheses.</summary>
    /// <remarks>Argument: the negative value as written, such as <c>-1</c>.</remarks>
    public static DiagnosticDescriptor NegativeArgument { get; } = new(
        "PIB2603",
        DiagnosticSeverity.Error,
        "A negative argument has to go in brackets.",
        help: "Write `({0})`.");

    /// <summary>PIB2604: a command that isn't declared.</summary>
    /// <remarks>Arguments: the command's name, without its <c>@</c>, and the closest command, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownCommand { get; } = new(
        "PIB2604",
        DiagnosticSeverity.Error,
        "I don't know a command called `@{0}`.",
        help: "Did you mean `@{1}`?");

    /// <summary>PIB2605: an argument whose type isn't the type of its parameter.</summary>
    /// <remarks>
    /// Arguments: what takes the argument, such as <c>@show</c> or <c>has_item()</c>; the parameter's type with its
    /// article; the parameter; the argument's type with its article; and what values to use instead, or
    /// <see langword="null"/> to leave out the help.
    /// </remarks>
    public static DiagnosticDescriptor ArgumentType { get; } = new(
        "PIB2605",
        DiagnosticSeverity.Error,
        "`{0}` expects {1} for `{2}`, but this is {3}.",
        help: "{4}");

    /// <summary>PIB2606: a command inside a line that isn't declared <c>inline</c>.</summary>
    /// <remarks>Argument: the command's name, without its <c>@</c>.</remarks>
    public static DiagnosticDescriptor NotInline { get; } = new(
        "PIB2606",
        DiagnosticSeverity.Error,
        "`@{0}` can't be used inside a line, because it isn't declared `inline`.",
        help: "Put it on its own `@` line.");

    /// <summary>PIB2607: more arguments than parameters.</summary>
    /// <remarks>Arguments: what takes the arguments, such as <c>has_item()</c>, and how many it takes, such as <c>only 1 argument</c>.</remarks>
    public static DiagnosticDescriptor TooManyArguments { get; } = new(
        "PIB2607",
        DiagnosticSeverity.Error,
        "`{0}` takes {1}.",
        help: "Remove the extra arguments.");

    /// <summary>PIB2608: a named argument for a parameter that doesn't exist.</summary>
    /// <remarks>Arguments: what takes the arguments, such as <c>@show</c>; the name; and the closest parameter, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownParameter { get; } = new(
        "PIB2608",
        DiagnosticSeverity.Error,
        "`{0}` has no parameter called `{1}`.",
        help: "Did you mean `{2}`?");

    /// <summary>PIB2609: a parameter with no default and no argument.</summary>
    /// <remarks>Arguments: what takes the arguments; the missing parameters, such as <c>a value for `to`</c>; and how to add them.</remarks>
    public static DiagnosticDescriptor MissingArgument { get; } = new(
        "PIB2609",
        DiagnosticSeverity.Error,
        "`{0}` needs {1}.",
        help: "{2}");

    /// <summary>PIB2610: a named argument for a parameter that already has one.</summary>
    /// <remarks>Arguments: what takes the arguments, and the parameter.</remarks>
    public static DiagnosticDescriptor RepeatedArgument { get; } = new(
        "PIB2610",
        DiagnosticSeverity.Error,
        "`{0}` already has a value for `{1}`.",
        help: "Remove one of them.");

    /// <summary>PIB2611: a call to a function that isn't declared.</summary>
    /// <remarks>Arguments: the function's name, and the closest function, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownFunction { get; } = new(
        "PIB2611",
        DiagnosticSeverity.Error,
        "I don't know a function called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB2612: a parameter with no default after one that has a default.</summary>
    /// <remarks>Argument: the parameter's name.</remarks>
    public static DiagnosticDescriptor RequiredAfterOptional { get; } = new(
        "PIB2612",
        DiagnosticSeverity.Error,
        "`{0}` has no default, but it comes after a parameter that has one.",
        help: "Put the parameters that have defaults last.");

    /// <summary>PIB3001: a <c>(</c> with no <c>)</c> on its line.</summary>
    public static DiagnosticDescriptor UnclosedParenthesis { get; } = new(
        "PIB3001",
        DiagnosticSeverity.Error,
        "I can't find the `)` that closes this `(`.",
        help: "Add the `)` on the same line.");

    /// <summary>PIB3002: two comparisons in a row, such as <c>$a &lt; $b &lt; $c</c>.</summary>
    /// <remarks>Argument: the whole chain as written.</remarks>
    public static DiagnosticDescriptor ChainedComparison { get; } = new(
        "PIB3002",
        DiagnosticSeverity.Error,
        "I can't compare three things at once: `{0}`.",
        help: "Compare two at a time, joined with `and`: `$a < $b and $b < $c`.");

    /// <summary>PIB3003: an operator used on types it doesn't work on.</summary>
    /// <remarks>Arguments: the operator; the operands' types, such as <c>a `string` and a `number`</c>; and what the operator works on.</remarks>
    public static DiagnosticDescriptor OperatorTypes { get; } = new(
        "PIB3003",
        DiagnosticSeverity.Error,
        "I can't use `{0}` with {1}.",
        help: "{2}");

    /// <summary>PIB3004: a bare name where no name can go, such as a whole condition or an operand of <c>+</c>.</summary>
    /// <remarks>Arguments: the name, and what was probably meant, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NameNotAllowed { get; } = new(
        "PIB3004",
        DiagnosticSeverity.Error,
        "I don't know what `{0}` means here.",
        help: "{1}");

    /// <summary>PIB3005: a bare name that isn't a value of the type expected where it appears.</summary>
    /// <remarks>Arguments: the name; the type with its article; and a suggestion, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor NotValueOfType { get; } = new(
        "PIB3005",
        DiagnosticSeverity.Error,
        "`{0}` isn't {1}.",
        help: "{2}");

    /// <summary>PIB3006: <c>==</c> or <c>!=</c> between two bare names, which have no type to be read against.</summary>
    /// <remarks>Argument: the comparison as written.</remarks>
    public static DiagnosticDescriptor TwoNames { get; } = new(
        "PIB3006",
        DiagnosticSeverity.Error,
        "I can't compare two names: `{0}`.",
        help: "Compare a variable with a name instead, like `$where == left`.");

    /// <summary>PIB3007: a <c>/</c> or <c>%</c> whose right side is a constant zero.</summary>
    /// <remarks>Argument: the operation as written, such as <c>$x / 0</c>.</remarks>
    public static DiagnosticDescriptor DivisionByZero { get; } = new(
        "PIB3007",
        DiagnosticSeverity.Warning,
        "`{0}` divides by zero, so it gives 0.",
        help: "Divide by something that isn't zero.");

    /// <summary>What PIB3101 and PIB3301's help adds when the line has a trailing comment, which runs to the end of the line and so takes any closer written after it.</summary>
    public const string CommentSwallowedCloserHelp = " The `//` on this line starts a comment that runs to the end of it, so put the closer before the `//`.";

    /// <summary>PIB3101: a markup span with no close on its line, or none before a conditional-text boundary.</summary>
    /// <remarks>Arguments: the markup's name, and <see cref="CommentSwallowedCloserHelp"/> if the line has a trailing comment, or an empty string.</remarks>
    public static DiagnosticDescriptor UnclosedMarkup { get; } = new(
        "PIB3101",
        DiagnosticSeverity.Error,
        "I can't find the end of this `[{0}]`.",
        help: "Close it on the same line with `[/{0}]`.{1}");

    /// <summary>PIB3102: a markup close for an outer span while an inner one is still open.</summary>
    /// <remarks>Argument: the name of the innermost open span.</remarks>
    public static DiagnosticDescriptor MarkupOutOfOrder { get; } = new(
        "PIB3102",
        DiagnosticSeverity.Error,
        "I expected `[/{0}]` here, because `[{0}]` was opened last.",
        help: "Close markup in the reverse order you opened it: `[b][i]…[/i][/b]`.");

    /// <summary>PIB3103: a markup close with no matching open span.</summary>
    /// <remarks>Argument: the markup's name.</remarks>
    public static DiagnosticDescriptor UnopenedMarkup { get; } = new(
        "PIB3103",
        DiagnosticSeverity.Error,
        "`[/{0}]` closes markup that was never opened.",
        help: "If you meant the text `[/{0}]`, put a backslash before it: `\\[/{0}]`.");

    /// <summary>PIB3104: markup that isn't declared or built in.</summary>
    /// <remarks>Arguments: the markup's name, and what it was probably meant to be, such as <c>clue</c> or <c>{speed}</c>, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownMarkup { get; } = new(
        "PIB3104",
        DiagnosticSeverity.Error,
        "I don't know markup called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>The help for PIB3201 when the point isn't an old spelling of one.</summary>
    public const string UnknownPointHelp = "To show a variable, write `{$name}`. To call a function, write `{name()}`. If the braces are part of the text, put a backslash before the `{`.";

    /// <summary>PIB3201: a point that isn't a variable, call, command or brace keyword, or is the old spelling <c>{p}</c> of <c>{page}</c>.</summary>
    /// <remarks>Arguments: the point as written, and how to fix it, which is <see cref="UnknownPointHelp"/> unless the point has a known replacement.</remarks>
    public static DiagnosticDescriptor UnknownPoint { get; } = new(
        "PIB3201",
        DiagnosticSeverity.Error,
        "I don't know what `{0}` means.",
        help: "{1}");

    /// <summary>PIB3202: a value after a point that takes none, such as <c>{w 0.5}</c>.</summary>
    /// <remarks>Arguments: the point's keyword, such as <c>w</c>; the value as written; and how to fix it.</remarks>
    public static DiagnosticDescriptor PointTakesNoValue { get; } = new(
        "PIB3202",
        DiagnosticSeverity.Error,
        "`{{{0}}}` takes no value, so I can't use `{1}` after it.",
        help: "{2}");

    /// <summary>PIB3203: an icon that isn't declared.</summary>
    /// <remarks>Arguments: the icon's name, and the closest icon, or <see langword="null"/> to leave out the help.</remarks>
    public static DiagnosticDescriptor UnknownIcon { get; } = new(
        "PIB3203",
        DiagnosticSeverity.Error,
        "I don't know an icon called `{0}`.",
        help: "Did you mean `{1}`?");

    /// <summary>PIB3204: a value shown in text whose type isn't text, a number or an actor.</summary>
    /// <remarks>Arguments: the value as written; its type with its article; and how to show text that depends on it.</remarks>
    public static DiagnosticDescriptor NotShowable { get; } = new(
        "PIB3204",
        DiagnosticSeverity.Error,
        "I can't show `{0}` in text, because it's {1}.",
        help: "{2}");

    /// <summary>PIB3205: a constant pacing value (a speed, a pause or a wait) that isn't more than zero.</summary>
    /// <remarks>Arguments: what the value is, such as <c>A pause</c>; the value as written; and how to write a valid one.</remarks>
    public static DiagnosticDescriptor NotPositive { get; } = new(
        "PIB3205",
        DiagnosticSeverity.Error,
        "{0} has to be more than zero, but `{1}` isn't.",
        help: "{2}");

    /// <summary>PIB3301: a bracket, brace or <c>{if}</c> that isn't closed on its line.</summary>
    /// <remarks>
    /// Arguments: the missing closer, such as <c>}</c> or <c>{/if}</c>; the opener it should close; and
    /// <see cref="CommentSwallowedCloserHelp"/> if the line has a trailing comment, or an empty string.
    /// </remarks>
    public static DiagnosticDescriptor Unclosed { get; } = new(
        "PIB3301",
        DiagnosticSeverity.Error,
        "I can't find the `{0}` that ends this `{1}`.",
        help: "Close it on the same line.{2}");

    /// <summary>PIB5001: a block nested deeper than <c>pibbles_max_nesting</c>.</summary>
    /// <remarks>Arguments: how many blocks deep the line is, and the limit.</remarks>
    public static DiagnosticDescriptor DeepNesting { get; } = new(
        "PIB5001",
        DiagnosticSeverity.Hint,
        "This line is {0} blocks deep, and more than {1} gets hard to follow.",
        help: "Move the inner blocks into a node of their own and `@call` it, or combine conditions with `and`.");

    /// <summary>PIB5002: an option whose body is longer than <c>pibbles_max_option_body</c> lines.</summary>
    /// <remarks>Arguments: the body's length in lines, and the limit.</remarks>
    public static DiagnosticDescriptor LongOptionBody { get; } = new(
        "PIB5002",
        DiagnosticSeverity.Hint,
        "This option's body is {0} lines long, more than {1}, so the rest of the choice ends up far from it.",
        help: "Move the body into a node of its own and `@jump` to it.");

    /// <summary>PIB5003: the same speaker saying the same text in at least <c>pibbles_min_repeated_lines</c> places.</summary>
    /// <remarks>Arguments: who says it, as a <see cref="SpeakerMention"/> or a phrase for narration, and in how many places.</remarks>
    public static DiagnosticDescriptor RepeatedLine { get; } = new(
        "PIB5003",
        DiagnosticSeverity.Hint,
        "{0} in {1} places.",
        help: "Put it in a node of its own and `@call` that node from each place. If this line should be translated or voiced differently, place `// pibbles-ignore PIB5003` above this line.");

    /// <summary>PIB5004: the same <c>[color]</c> value in at least <c>pibbles_min_repeated_colors</c> places.</summary>
    /// <remarks>Arguments: the color, and in how many places it's used.</remarks>
    public static DiagnosticDescriptor RepeatedColor { get; } = new(
        "PIB5004",
        DiagnosticSeverity.Hint,
        "The color `{0}` is used in {1} places.",
        help: "If it means something, such as a clue, give it a name: declare a markup such as `@markup clue`, ask whoever programs the game to style it, and use `[clue]` in each place.");

    /// <summary>PIB5101: a value set by a line with no text that changes again before any line shows.</summary>
    /// <remarks>Arguments: the value; the actor, as a <see cref="SpeakerMention"/>; and the channel.</remarks>
    public static DiagnosticDescriptor UnseenValueChange { get; } = new(
        "PIB5101",
        DiagnosticSeverity.Hint,
        "`{0}` does nothing, because {1}'s `{2}` changes again before a line shows.",
        help: "Remove `{0}`.");

    /// <summary>PIB5102: a value set to the value the actor already has.</summary>
    /// <remarks>Arguments: the value, and the actor, as a <see cref="SpeakerMention"/>.</remarks>
    public static DiagnosticDescriptor RepeatedValue { get; } = new(
        "PIB5102",
        DiagnosticSeverity.Hint,
        "`{0}` does nothing, because {1} already has it.",
        help: "Leave `{0}` out of this line.");

    /// <summary>PIB5103: <c>{w}</c> at the very end of a line.</summary>
    public static DiagnosticDescriptor PauseAtLineEnd { get; } = new(
        "PIB5103",
        DiagnosticSeverity.Hint,
        "This wait does nothing, because the end of the line already waits for a click.",
        help: "Remove it.");

    /// <summary>PIB5104: <c>{w}</c> just before <c>{page}</c>.</summary>
    public static DiagnosticDescriptor PauseBeforePageBreak { get; } = new(
        "PIB5104",
        DiagnosticSeverity.Hint,
        "This wait does nothing, because the `{{page}}` after it already waits.",
        help: "Remove it.");

    /// <summary>PIB5105: two timed pauses side by side.</summary>
    /// <remarks>Argument: the two durations added up, or <see langword="null"/> when that isn't known, which leaves out the help.</remarks>
    public static DiagnosticDescriptor AdjacentPauses { get; } = new(
        "PIB5105",
        DiagnosticSeverity.Hint,
        "This pause comes right after another one, so the two could be one.",
        help: "Write one pause: `{{pause {0}}}`.");

    /// <summary>PIB5106: a markup span with nothing inside it.</summary>
    /// <remarks>Argument: the markup's name.</remarks>
    public static DiagnosticDescriptor EmptyMarkup { get; } = new(
        "PIB5106",
        DiagnosticSeverity.Hint,
        "This `[{0}]` is empty.",
        help: "Remove it.");

    /// <summary>PIB5107: a markup span inside another one just like it.</summary>
    /// <remarks>Argument: the markup's name.</remarks>
    public static DiagnosticDescriptor NestedMarkup { get; } = new(
        "PIB5107",
        DiagnosticSeverity.Hint,
        "This `[{0}]` is inside another one just like it, so it changes nothing.",
        help: "Remove the inner `[{0}]` and its `[/{0}]`.");

    /// <summary>PIB5108: a markup span that starts right where one just like it ends.</summary>
    /// <remarks>Argument: the markup's name.</remarks>
    public static DiagnosticDescriptor AdjacentMarkup { get; } = new(
        "PIB5108",
        DiagnosticSeverity.Hint,
        "This `[{0}]` starts right where one just like it ends, so the two could be one.",
        help: "Join them into one span.");

    /// <summary>PIB5109: an <c>@if</c> or <c>{if}</c> whose branches are all the same.</summary>
    /// <remarks>Argument: <c>@if</c> or <c>{if}</c>.</remarks>
    public static DiagnosticDescriptor RedundantCondition { get; } = new(
        "PIB5109",
        DiagnosticSeverity.Hint,
        "Every branch of this `{0}` is the same, so the condition makes no difference.",
        help: "Keep one copy of the branch, without the condition.");

    /// <summary>PIB5110: <c>@return</c> as the last statement of a node.</summary>
    public static DiagnosticDescriptor RedundantReturn { get; } = new(
        "PIB5110",
        DiagnosticSeverity.Hint,
        "This `@return` does nothing, since the end of a node returns anyway.",
        help: "Remove it.");

    /// <summary>PIB5111: a <c>@prefix</c> that starts with a dot, which does nothing.</summary>
    /// <remarks>Arguments: the prefix as written, and without its dot.</remarks>
    public static DiagnosticDescriptor RedundantPrefixDot { get; } = new(
        "PIB5111",
        DiagnosticSeverity.Info,
        "`@prefix {0}` doesn't need its dot.",
        help: "Remove it: `@prefix {1}`.");

    /// <summary>PIB5201: a comparison with <c>true</c> or <c>false</c>.</summary>
    /// <remarks>Arguments: the literal, and the condition without the comparison.</remarks>
    public static DiagnosticDescriptor ComparisonWithBool { get; } = new(
        "PIB5201",
        DiagnosticSeverity.Hint,
        "Comparing with `{0}` isn't needed.",
        help: "Write `{1}`.");

    /// <summary>PIB5202: <c>@set $x = $x + v</c>, which <c>+=</c> or <c>-=</c> says more briefly.</summary>
    /// <remarks>Arguments: the operator, and the line written with it.</remarks>
    public static DiagnosticDescriptor CompoundAssignment { get; } = new(
        "PIB5202",
        DiagnosticSeverity.Hint,
        "This can be shorter with `{0}`.",
        help: "Write `{1}`.");

    /// <summary>PIB5301: a line whose longest page is longer than <c>pibbles_max_message_length</c>.</summary>
    /// <remarks>Arguments: the length and the limit.</remarks>
    public static DiagnosticDescriptor LongLine { get; } = new(
        "PIB5301",
        DiagnosticSeverity.Hint,
        "This line is {0} characters long, more than {1}.",
        help: "Split it into two lines, or into pages with `{{page}}`.");

    /// <summary>PIB5302: an option longer than <c>pibbles_max_option_length</c>.</summary>
    /// <remarks>Arguments: the length and the limit.</remarks>
    public static DiagnosticDescriptor LongOption { get; } = new(
        "PIB5302",
        DiagnosticSeverity.Hint,
        "This option is {0} characters long, more than {1}.",
        help: "Shorten it to what the player picks. The lines after it can say the rest.");

    /// <summary>PIB5303: a declared name that isn't <c>snake_case</c>.</summary>
    /// <remarks>Arguments: the name as written, and in <c>snake_case</c>.</remarks>
    public static DiagnosticDescriptor NamingConvention { get; } = new(
        "PIB5303",
        DiagnosticSeverity.Hint,
        "`{0}` isn't written in snake_case.",
        help: "Write it as `{1}`, before anything outside the story uses the name.");

    /// <summary>PIB5304: a block indented differently from the file's other blocks, or from <c>.editorconfig</c>.</summary>
    /// <remarks>Arguments: how the block is indented, what it should match, and how to indent it.</remarks>
    public static DiagnosticDescriptor IndentationWidth { get; } = new(
        "PIB5304",
        DiagnosticSeverity.Hint,
        "This block is indented {0}, but {1}.",
        help: "Indent it {2}.");

    /// <summary>PIB5305: a speaker written with spacing other than <c>name (pose):</c>.</summary>
    /// <remarks>Argument: the speaker written the usual way.</remarks>
    public static DiagnosticDescriptor SpeakerSpacing { get; } = new(
        "PIB5305",
        DiagnosticSeverity.Hint,
        "This speaker is spaced differently from the usual `{0}`.",
        help: "Write `{0}`.");

    /// <summary>PIB5401: a variable nothing in the story uses.</summary>
    /// <remarks>Argument: the variable's name, without the <c>$</c>.</remarks>
    public static DiagnosticDescriptor UnusedVariable { get; } = new(
        "PIB5401",
        DiagnosticSeverity.Info,
        "Nothing in the story uses `${0}`.",
        help: "Remove its `@var`, or use it.");

    /// <summary>Every registered descriptor.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        MixedIndentation,
        InconsistentIndentation,
        UnexpectedIndentation,
        MissingBlock,
        UnsupportedNote,
        NonIndentingSpace,
        ByteOrderMark,
        DeclarationInNode,
        OutsideNode,
        NestingLimit,
        InvalidEscape,
        MarkerLookalike,
        Unexpected,
        UnterminatedString,
        MalformedNumber,
        InputMethodForm,
        Missing,
        NumberTooLarge,
        TextAfterTag,
        MalformedTagName,
        TagNotAllowed,
        TagsOnlyLine,
        UnknownTag,
        TagValuePresence,
        TagValueNotMember,
        TagValueType,
        InvalidLineId,
        MissingLineId,
        DuplicateLineId,
        LineIdIsNodeName,
        DottedName,
        DuplicateDeclaration,
        RepeatedName,
        ReservedName,
        NonAsciiName,
        UnknownType,
        UnknownNode,
        DuplicateNode,
        OldNodeName,
        MisplacedPrefix,
        DuplicatePrefix,
        RelativeWithoutPrefix,
        RepeatedDisplayName,
        ObjectReplacementCharacter,
        EmptyLine,
        UnknownSpeaker,
        UnknownChannelValue,
        NotValues,
        NoSpaceAfterSpeaker,
        AfterValues,
        TwoValuesInChannel,
        ValueInTwoChannels,
        UnknownChannel,
        SecondNarration,
        NotInOption,
        EmptyOption,
        StrayClause,
        OpenerColon,
        NotCondition,
        MissingDollar,
        UnknownVariable,
        ValueType,
        UntypedVariable,
        NeverRuns,
        ConditionNeverTrue,
        ConditionAlwaysTrue,
        NotAlternative,
        OpenerInAlternative,
        ArgumentOrder,
        SpaceBeforeCall,
        NegativeArgument,
        UnknownCommand,
        ArgumentType,
        NotInline,
        TooManyArguments,
        UnknownParameter,
        MissingArgument,
        RepeatedArgument,
        UnknownFunction,
        RequiredAfterOptional,
        UnclosedParenthesis,
        ChainedComparison,
        OperatorTypes,
        NameNotAllowed,
        NotValueOfType,
        TwoNames,
        DivisionByZero,
        UnclosedMarkup,
        MarkupOutOfOrder,
        UnopenedMarkup,
        UnknownMarkup,
        UnknownPoint,
        PointTakesNoValue,
        UnknownIcon,
        NotShowable,
        NotPositive,
        Unclosed,
        DeepNesting,
        LongOptionBody,
        RepeatedLine,
        RepeatedColor,
        UnseenValueChange,
        RepeatedValue,
        PauseAtLineEnd,
        PauseBeforePageBreak,
        AdjacentPauses,
        EmptyMarkup,
        NestedMarkup,
        AdjacentMarkup,
        RedundantCondition,
        RedundantReturn,
        RedundantPrefixDot,
        ComparisonWithBool,
        CompoundAssignment,
        LongLine,
        LongOption,
        NamingConvention,
        IndentationWidth,
        SpeakerSpacing,
        UnusedVariable,
    ];
}
