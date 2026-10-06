using Pibbles.Diagnostics;

namespace Pibbles.Semantics;

/// <summary>Maintenance: variables nothing uses.</summary>
internal sealed partial class StyleChecks
{
    /// <summary>
    /// PIB5040: a variable the story declares and never uses. Setting it counts as a use, since the game's code may read
    /// it. The game's vocabulary, such as commands and markup, isn't checked: it's a toolbox, and some of it going unused
    /// is normal.
    /// </summary>
    private void CheckUnusedVariables()
    {
        foreach (VariableSymbol variable in symbols.Variables.Values.Where(variable => !references.UsesOf(variable).Any()))
        {
            if (variable.Location is { } location)
                ReportAt(DiagnosticCatalog.UnusedVariable, location, variable.Name);
        }
    }
}
