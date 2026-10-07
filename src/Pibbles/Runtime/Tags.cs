using System.Collections;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>What kind of value a tag has.</summary>
public enum TagKind
{
    /// <summary>A tag with no value, such as <c>#thought</c>.</summary>
    Flag,

    /// <summary>A tag whose value is text (the story's `string`), such as <c>#voice:rex_01</c>.</summary>
    Text,

    /// <summary>A tag whose value is a member of an enum, such as <c>#box:phone</c>.</summary>
    Enum,

    /// <summary>A tag Pibbles itself defines, such as <c>#id:k7qp2x</c>.</summary>
    Reserved,
}

/// <summary>A tag on a line or an option.</summary>
/// <param name="Name">The tag's name, without the <c>#</c>.</param>
/// <param name="Kind">What kind of value it has.</param>
/// <param name="Value">The value as written, a member's name for an enum tag, or <see langword="null"/> for a flag and for an empty value.</param>
public sealed record Tag(string Name, TagKind Kind, string? Value);

/// <summary>
/// A line's tags, in the order written, with typed access by name. Asking for a tag the story doesn't declare is a
/// mistake and throws <see cref="ArgumentException"/>, as does the wrong accessor for a tag's kind
/// (<see cref="InvalidOperationException"/>). Asking for a declared tag the line doesn't have is not: <see cref="Has"/> is
/// false and the getters return <see langword="null"/>.
/// </summary>
public sealed class TagCollection : IReadOnlyList<Tag>, IEquatable<TagCollection>
{
    private static readonly string[] ReservedNames = ["id", "was"];

    private readonly IReadOnlyList<Tag> tags;
    private readonly IReadOnlyDictionary<string, TagSymbol> declared;

    internal TagCollection(IReadOnlyList<Tag> tags, IReadOnlyDictionary<string, TagSymbol> declared)
    {
        this.tags = tags;
        this.declared = declared;
    }

    /// <inheritdoc/>
    public int Count => tags.Count;

    /// <inheritdoc/>
    public Tag this[int index] => tags[index];

    /// <summary>Whether the line has the tag.</summary>
    /// <exception cref="ArgumentException">The story doesn't declare a tag with that name.</exception>
    public bool Has(string name)
    {
        Require(name);
        return tags.Any(tag => tag.Name == name);
    }

    /// <summary>The text value of a <c>string</c> tag, or of a reserved one such as <c>id</c>; <see langword="null"/> if the line doesn't have it.</summary>
    public string? GetString(string name)
    {
        TagKind kind = Require(name);
        return kind is TagKind.Text or TagKind.Reserved ? ValueOf(name) : throw WrongAccessor(name, kind);
    }

    /// <summary>The member name of an enum tag; <see langword="null"/> if the line doesn't have it.</summary>
    public string? GetEnum(string name)
    {
        TagKind kind = Require(name);
        return kind is TagKind.Enum ? ValueOf(name) : throw WrongAccessor(name, kind);
    }

    /// <summary>The member of an enum tag as the host's own enum with the same member names; <see langword="null"/> if the line doesn't have it.</summary>
    /// <exception cref="InvalidOperationException">The host's enum has no member with that name.</exception>
    public TEnum? GetEnum<TEnum>(string name)
        where TEnum : struct, Enum
    {
        string? member = GetEnum(name);
        if (member is null)
            return null;

        return Enum.TryParse(member, ignoreCase: false, out TEnum result)
            ? result
            : throw new InvalidOperationException($"`{typeof(TEnum).Name}` has no member called `{member}`, which `#{name}` can be.");
    }

    /// <inheritdoc/>
    public IEnumerator<Tag> GetEnumerator() => tags.GetEnumerator();

    /// <inheritdoc/>
    public bool Equals(TagCollection? other) => other is not null && tags.SequenceEqual(other.tags);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TagCollection);

    /// <inheritdoc/>
    public override int GetHashCode() => tags.Count;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private string? ValueOf(string name) => tags.FirstOrDefault(tag => tag.Name == name)?.Value;

    private TagKind Require(string name)
    {
        if (ReservedNames.Contains(name))
            return TagKind.Reserved;

        return declared.TryGetValue(name, out TagSymbol? symbol)
            ? symbol.ValueType is null ? TagKind.Flag : symbol.ValueType is EnumSymbol ? TagKind.Enum : TagKind.Text
            : throw new ArgumentException($"The story doesn't declare a tag called `#{name}`.", nameof(name));
    }

    private static InvalidOperationException WrongAccessor(string name, TagKind kind) =>
        new($"`#{name}` is a {kind.ToString().ToLowerInvariant()} tag, so it can't be read this way.");
}
