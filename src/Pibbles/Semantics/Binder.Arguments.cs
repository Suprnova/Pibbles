using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>Arguments: matching a command's, markup's or function's arguments to its parameters.</summary>
internal sealed partial class Binder
{
    /// <summary>
    /// Matches arguments to parameters, binding each against its parameter's type: positional ones in order, then named
    /// ones by name. Every parameter without a default needs one, unless an argument's name is unknown, since that
    /// argument was most likely meant for it.
    /// </summary>
    /// <param name="owner">What takes the arguments: a command, markup or function.</param>
    /// <param name="parameters">The owner's parameters.</param>
    /// <param name="arguments">The arguments, positional ones first.</param>
    /// <param name="span">Where a missing argument is reported.</param>
    /// <param name="named">Whether arguments can be named, which says how a missing one can be added.</param>
    private void BindArguments(Symbol owner, IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<ArgumentSyntax> arguments, TextSpan span, bool named)
    {
        bool[] given = new bool[parameters.Count];
        TextSpan? extra = null;
        bool misnamed = false;
        int next = 0;
        foreach (ArgumentSyntax argument in arguments)
        {
            int index = -1;
            if (argument.Name is not { } name)
            {
                if (next < parameters.Count)
                    index = next++;
                else
                    extra = extra is { } first ? new(first.Start, argument.Span.End - first.Start) : argument.Span;
            }
            else
            {
                index = IndexOf(parameters, name.Text);
                if (index < 0)
                {
                    misnamed = true;
                    ReportWithOptionalHelp(DiagnosticCatalog.UnknownParameter, name.Span, Spelled(owner), name.Text, Suggestions.Closest(name.Text, parameters.Select(candidate => candidate.Name)));
                }
                else
                {
                    Refers(name, parameters[index]);
                    if (given[index])
                    {
                        Report(DiagnosticCatalog.RepeatedArgument, argument.Span, Spelled(owner), parameters[index].Name);
                        index = -1;
                    }
                }
            }

            if (index < 0)
            {
                Bind(argument.Value, TypeSymbol.Error);
                continue;
            }

            given[index] = true;
            ParameterSymbol parameter = parameters[index];
            TypeSymbol type = Bind(argument.Value, parameter.Type);
            bindings.BindArgument(argument.Value, parameter);
            if (Converts(type, parameter.Type))
                bindings.ConvertTo(argument.Value, parameter.Type);
            else
                ReportWithOptionalHelp(DiagnosticCatalog.ArgumentType, argument.Value.Span, Spelled(owner), parameter.Type.Describe(), parameter.Name, type.Describe(), ValueHelp(parameter.Type));
        }

        if (extra is { } extraSpan)
        {
            string takes = parameters.Count switch { 0 => "no arguments", 1 => "only 1 argument", var count => $"only {count} arguments" };
            Report(DiagnosticCatalog.TooManyArguments, extraSpan, Spelled(owner), takes);
        }

        string[] missing = [.. Missing(parameters, given)];
        if (missing.Length > 0 && !misnamed)
        {
            string needs = missing.Length == 1 ? $"a value for {Phrase.And(missing)}" : $"values for {Phrase.And(missing)}";
            string help = (named, missing.Length) switch
            {
                (true, 1) => $"Add it after the others, or by name: `{missing[0]}=…`.",
                (true, _) => $"Add them after the others, or by name, like `{missing[0]}=…`.",
                (false, 1) => "Add it in the brackets, in order.",
                (false, _) => "Add them in the brackets, in order.",
            };
            Report(DiagnosticCatalog.MissingArgument, span, Spelled(owner), needs, help);
        }
    }

    private static int IndexOf(IReadOnlyList<ParameterSymbol> parameters, string name)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Name == name)
                return i;
        }

        return -1;
    }

    /// <summary>The names of the parameters without a default that no argument filled, in order.</summary>
    private static IEnumerable<string> Missing(IReadOnlyList<ParameterSymbol> parameters, bool[] given)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (!parameters[i].IsOptional && !given[i])
                yield return parameters[i].Name;
        }
    }

    /// <summary>What takes arguments, as messages write it: <c>@show</c>, <c>[wave]</c> or <c>has_item()</c>.</summary>
    private static string Spelled(Symbol owner) => owner switch
    {
        CommandSymbol => $"@{owner.Name}",
        MarkupSymbol => $"[{owner.Name}]",
        _ => $"{owner.Name}()",
    };

    /// <summary>Binds the arguments of something that doesn't exist, so problems inside them are still reported, but not their types.</summary>
    private void BindUnmatched(IEnumerable<ArgumentSyntax> arguments)
    {
        foreach (ArgumentSyntax argument in arguments)
            Bind(argument.Value, TypeSymbol.Error);
    }
}
