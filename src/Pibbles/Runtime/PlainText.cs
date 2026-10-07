using System.Globalization;
using System.Text;
using Pibbles.Compiler;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>Flattens a template to the plain text a line shows, without any markup.</summary>
internal static class PlainText
{
    private const string NumberFormat = "0.############################";

    /// <summary>
    /// Renders content as text: text runs, the text inside markup, shown values, the chosen branch of conditional text,
    /// <c>{br}</c> as a line break and an icon as U+FFFC. Points contribute nothing. Shown values are inserted literally,
    /// a number with the invariant culture, no digit grouping and no trailing zeros. Leading and trailing whitespace is trimmed.
    /// </summary>
    public static string Render(IEnumerable<TemplateElement> content, IEvaluationContext context)
    {
        var text = new StringBuilder();
        Append(text, content, context);
        return text.ToString().Trim();
    }

    private static void Append(StringBuilder text, IEnumerable<TemplateElement> content, IEvaluationContext context)
    {
        foreach (TemplateElement element in content)
        {
            switch (element)
            {
                case TextElement run:
                    text.Append(run.Text);
                    break;

                case MarkupElement markup:
                    Append(text, markup.Children, context);
                    break;

                case InterpolationElement interpolation:
                    text.Append(Show(Evaluator.Evaluate(interpolation.Value, context)));
                    break;

                case ConditionalElement conditional:
                    ConditionalBranch? chosen = conditional.Branches.FirstOrDefault(branch => Evaluator.Evaluate(branch.Condition, context).AsBool);
                    Append(text, chosen?.Content ?? conditional.Else ?? [], context);
                    break;

                case LineBreakElement:
                    text.Append('\n');
                    break;

                case IconElement:
                    text.Append('￼');
                    break;
            }
        }
    }

    private static string Show(Value value) =>
        value.Type == TypeSymbol.Number ? value.AsDecimal.ToString(NumberFormat, CultureInfo.InvariantCulture)
        : value.Type == TypeSymbol.Actor ? ((ActorSymbol)value.AsSymbol).DisplayName
        : value.AsString;
}
