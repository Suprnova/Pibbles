namespace Pibbles.Diagnostics;

/// <summary>A problem found in a story, with where it is and how to fix it.</summary>
/// <param name="Code">The catalog code, such as <c>PIB1001</c>.</param>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Location">The marked text.</param>
/// <param name="Message">A plain sentence saying what's wrong.</param>
/// <param name="Label">A short note shown under the marked text, or <see langword="null"/>.</param>
/// <param name="Help">How to fix the problem, or <see langword="null"/>.</param>
public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, SourceLocation Location, string Message, string? Label, string? Help)
{
    /// <summary>The descriptor the diagnostic was written from, or <see langword="null"/> if it was made directly.</summary>
    internal DiagnosticDescriptor? Descriptor { get; init; }

    /// <summary>The arguments its templates were formatted with.</summary>
    internal IReadOnlyList<object?> Arguments { get; init; } = [];

    /// <summary>Whether two diagnostics read the same: the same code, severity, location and text.</summary>
    /// <param name="other">The diagnostic to compare with.</param>
    public bool Equals(Diagnostic? other) =>
        other is not null && (Code, Severity, Location, Message, Label, Help) == (other.Code, other.Severity, other.Location, other.Message, other.Label, other.Help);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Code, Severity, Location, Message, Label, Help);

    /// <summary>
    /// Writes the diagnostic again with each <see cref="SpeakerMention"/> in its arguments named by the speaker's display
    /// name, where <paramref name="displayName"/> knows it. Its severity, and a label or help it left out, stay as they are.
    /// </summary>
    internal Diagnostic NameSpeakers(Func<string, string?> displayName)
    {
        if (Descriptor is null || !Arguments.Any(argument => argument is SpeakerMention))
            return this;

        object?[] named = [.. Arguments.Select(argument => argument is SpeakerMention mention && displayName(mention.Speaker) is { } name ? mention with { Speaker = name } : argument)];
        Diagnostic rewritten = Descriptor.Create(Location, named);
        return rewritten with { Severity = Severity, Label = Label is null ? null : rewritten.Label, Help = Help is null ? null : rewritten.Help };
    }
}
