using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Pibbles.Syntax;
using Pibbles.Tests.Syntax;

namespace Pibbles.Tests.Snapshots;

/// <summary>
/// Writes a syntax tree as text for snapshots: one node per line, indented by depth, with its kind, its 1-based
/// <c>line:column-line:column</c> span and its values. Child nodes are labeled with the property that holds them.
/// </summary>
/// <remarks>A node's fields are its record's constructor parameters, in order, so the dump follows each node's own shape.</remarks>
internal static class SyntaxDump
{
    public static string Write(SyntaxTree tree, ISet<Type>? kinds = null) => Write(tree.Source, tree.Root, kinds);

    /// <summary>Writes a tree without its spans, so a tree built in code compares equal to the same tree parsed from source.</summary>
    public static string WriteShape(SyntaxNode root, ISet<Type>? kinds = null) => Write(source: null, root, kinds);

    private static string Write(SourceText? source, SyntaxNode root, ISet<Type>? kinds)
    {
        var builder = new StringBuilder();
        Write(builder, source, root, label: null, depth: 0, kinds);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, SourceText? source, SyntaxNode node, string? label, int depth, ISet<Type>? kinds)
    {
        Type type = node.GetType();
        kinds?.Add(type);
        PropertyInfo[] fields = NodeFields.Of(type);

        builder.Append(' ', depth * 2);
        if (label is not null)
            builder.Append(label).Append(": ");

        builder.Append(type.Name.Replace("Syntax", "", StringComparison.Ordinal));
        if (source is not null)
            builder.Append(' ').Append(Format(source, node.Span));

        foreach (PropertyInfo field in fields.Where(field => !NodeFields.HoldsChildren(field) && (source is not null || !IsSpan(field.PropertyType))))
            builder.Append(' ').Append(field.Name).Append('=').Append(Format(source, field.GetValue(node)));

        builder.Append('\n');

        foreach (PropertyInfo field in fields.Where(NodeFields.HoldsChildren))
        {
            switch (field.GetValue(node))
            {
                case SyntaxNode child:
                    Write(builder, source, child, field.Name, depth + 1, kinds);
                    break;

                case IEnumerable children when children.Cast<SyntaxNode>().ToList() is { Count: > 0 } list:
                    builder.Append(' ', (depth + 1) * 2).Append(field.Name).Append(":\n");
                    foreach (SyntaxNode child in list)
                        Write(builder, source, child, null, depth + 2, kinds);
                    break;
            }
        }
    }

    private static bool IsSpan(Type type) => type == typeof(TextSpan) || type == typeof(TextSpan?);

    /// <summary>Writes a span as 1-based <c>line:column-line:column</c>.</summary>
    public static string Format(SourceText source, TextSpan span)
    {
        LinePosition start = source.GetLinePosition(span.Start);
        LinePosition end = source.GetLinePosition(span.End);
        return $"{start.Line + 1}:{start.Column + 1}-{end.Line + 1}:{end.Column + 1}";
    }

    private static string Format(SourceText? source, object? value) => value switch
    {
        null => "null",
        TextSpan span => Format(source!, span),
        string text => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };
}
