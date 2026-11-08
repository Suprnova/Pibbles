namespace Pibbles.Diagnostics;

public static partial class DiagnosticCatalog
{
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
}
