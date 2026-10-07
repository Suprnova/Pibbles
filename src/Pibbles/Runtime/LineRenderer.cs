using System.Globalization;
using System.Text;
using Pibbles.Compiler;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>
/// Renders a template to a <see cref="Line"/>: its text, and everything positioned in it. One pass builds the text and
/// records each span, marker and icon at the position the text has reached, then the finished text is trimmed and every
/// position is moved to match. Everything is evaluated here, before the line is shown, so a failure leaves nothing half done.
/// </summary>
internal static class LineRenderer
{
    private const string NumberFormat = "0.############################";

    /// <summary>Renders a line or option.</summary>
    /// <param name="id">The line's or option's ID.</param>
    /// <param name="template">What to render.</param>
    /// <param name="story">The story, for the tags it declares.</param>
    /// <param name="context">What expressions in the line read.</param>
    /// <param name="warn">Receives warnings raised while rendering, which the caller holds back until the step is delivered.</param>
    /// <param name="emitNow">Receives warnings raised later, when the host reads an argument.</param>
    public static Line Render(string id, Template template, Story story, IEvaluationContext context, Action<RuntimeWarning> warn, Action<RuntimeWarning> emitNow)
    {
        var pass = new Pass(context, warn, emitNow);
        pass.Append(template.Content);
        return pass.Finish(id, template, story);
    }

    /// <summary>A number as a line shows it: the invariant culture, no digit grouping and no trailing zeros.</summary>
    public static string FormatNumber(decimal number) => number.ToString(NumberFormat, CultureInfo.InvariantCulture);

    private static string Show(Value value) =>
        (value.Type == TypeSymbol.Number ? FormatNumber(value.AsDecimal)
        : value.Type == TypeSymbol.Actor ? ((ActorSymbol)value.AsSymbol).DisplayName
        : value.AsString).Replace('￼', '�');

    private sealed class Pass(IEvaluationContext context, Action<RuntimeWarning> warn, Action<RuntimeWarning> emitNow)
    {
        private readonly StringBuilder text = new();
        private readonly List<OpenSpan> spans = [];
        private readonly List<(int Position, Func<int, Marker> Create)> markers = [];
        private readonly List<(int Position, string Name)> icons = [];

        public void Append(IEnumerable<TemplateElement> content)
        {
            foreach (TemplateElement element in content)
            {
                switch (element)
                {
                    case TextElement run:
                        text.Append(run.Text);
                        break;

                    case MarkupElement markup:
                        AppendSpan(markup);
                        break;

                    case InterpolationElement interpolation:
                        text.Append(Show(Evaluator.Evaluate(interpolation.Value, context)));
                        break;

                    case ConditionalElement conditional:
                        ConditionalBranch? chosen = conditional.Branches.FirstOrDefault(branch => Evaluator.Evaluate(branch.Condition, context).AsBool);
                        Append(chosen?.Content ?? conditional.Else ?? []);
                        break;

                    case LineBreakElement:
                        text.Append('\n');
                        break;

                    case IconElement icon:
                        icons.Add((text.Length, icon.Icon.Name));
                        text.Append('￼');
                        break;

                    default:
                        AppendMarker(element);
                        break;
                }
            }
        }

        public Line Finish(string id, Template template, Story story)
        {
            string raw = text.ToString();
            string trimmed = raw.Trim();
            int lead = raw.Length - raw.TrimStart().Length;
            int Move(int position) => Math.Clamp(position - lead, 0, trimmed.Length);

            return new(
                id,
                template.Speaker?.Name,
                template.Speaker?.DisplayName,
                trimmed,
                [.. spans.Select(span => new Span(span.Name, Move(span.Start), Move(span.End) - Move(span.Start), span.Arguments))],
                [.. markers.Select(marker => marker.Create(Move(marker.Position)))],
                [.. icons.Select(icon => new Icon(Move(icon.Position), icon.Name))],
                Tags(template, story),
                story.FallbackIds.Contains(id));
        }

        private void AppendSpan(MarkupElement markup)
        {
            Value[] values = [.. markup.Arguments.Select(argument => Evaluator.Evaluate(argument, context))];
            var span = new OpenSpan(markup.Markup.Name, text.Length, new($"`[{markup.Markup.Name}]`", markup.Markup.Parameters, values, markup.Location, emitNow));
            spans.Add(span);
            Append(markup.Children);
            span.End = text.Length;
        }

        private void AppendMarker(TemplateElement element)
        {
            int position = text.Length;
            switch (element)
            {
                case InputWaitElement:
                    markers.Add((position, at => new InputWaitMarker(at)));
                    break;

                case PageBreakElement:
                    markers.Add((position, at => new PageBreakMarker(at)));
                    break;

                case PauseElement pause:
                    decimal seconds = Evaluator.Evaluate(pause.Duration, context).AsDecimal;
                    if (seconds <= 0)
                        warn(new(RuntimeWarningKind.NonPositivePause, "This pause isn't for more than zero time, so I skipped it.", pause.Location));
                    else
                        markers.Add((position, at => new PauseMarker(at, Durations.ToTimeSpan(seconds, pause.Location, warn))));
                    break;

                case SpeedElement speed:
                    decimal factor = Evaluator.Evaluate(speed.Factor, context).AsDecimal;
                    if (factor <= 0)
                        warn(new(RuntimeWarningKind.NonPositiveSpeed, "This speed isn't more than zero, so I skipped it.", speed.Location));
                    else
                        markers.Add((position, at => new SpeedMarker(at, factor)));
                    break;

                case SpeedResetElement:
                    markers.Add((position, at => new SpeedMarker(at, 1)));
                    break;

                case CommandElement command:
                    Value[] arguments = [.. command.Arguments.Select(argument => Evaluator.Evaluate(argument, context))];
                    markers.Add((position, at => new CommandMarker(at, new(command.Command, arguments, command.Location, emitNow), command.Waits)));
                    break;

                default:
                    throw new NotSupportedException(element.GetType().Name);
            }
        }

        private static TagCollection Tags(Template template, Story story) => new(
            [.. template.Tags.Select(tag => new Tag(tag.Name, KindOf(tag.Name, story), string.IsNullOrEmpty(tag.Value) ? null : tag.Value))],
            story.Tags);

        private static TagKind KindOf(string name, Story story) =>
            !story.Tags.TryGetValue(name, out TagSymbol? symbol) ? TagKind.Reserved
            : symbol.ValueType is null ? TagKind.Flag
            : symbol.ValueType is EnumSymbol ? TagKind.Enum
            : TagKind.Text;
    }

    private sealed class OpenSpan(string name, int start, Arguments arguments)
    {
        public string Name { get; } = name;

        public int Start { get; } = start;

        public int End { get; set; }

        public Arguments Arguments { get; } = arguments;
    }
}
