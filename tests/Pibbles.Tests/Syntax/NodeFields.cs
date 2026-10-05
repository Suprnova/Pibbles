using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Pibbles.Syntax;

namespace Pibbles.Tests.Syntax;

/// <summary>Reads syntax nodes generically. A node's fields are its record's constructor parameters, in order.</summary>
internal static class NodeFields
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Cache = new();

    public static PropertyInfo[] Of(Type type) =>
        Cache.GetOrAdd(type, type => [.. type.GetConstructors().Single().GetParameters().Select(parameter => type.GetProperty(parameter.Name!)!)]);

    public static bool HoldsChildren(PropertyInfo field) =>
        typeof(SyntaxNode).IsAssignableFrom(field.PropertyType) || typeof(IEnumerable<SyntaxNode>).IsAssignableFrom(field.PropertyType);

    /// <summary>The nodes directly under <paramref name="node"/>, in field order.</summary>
    public static IEnumerable<SyntaxNode> Children(SyntaxNode node) =>
        Of(node.GetType()).Where(HoldsChildren).SelectMany(field => field.GetValue(node) switch
        {
            SyntaxNode child => [child],
            IEnumerable children => children.Cast<SyntaxNode>(),
            _ => [],
        });

    /// <summary><paramref name="root"/> and every node under it, parents first.</summary>
    public static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode root) => [root, .. Children(root).SelectMany(DescendantsAndSelf)];
}
