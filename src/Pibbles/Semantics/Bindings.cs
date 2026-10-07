using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>What an expression's type is, and the type its context converts it to.</summary>
/// <param name="Type">The type the expression has. <see cref="TypeSymbol.Error"/> if it can't be known.</param>
/// <param name="ConvertedType">
/// The type the context uses it as: <see cref="TypeSymbol.Duration"/> for a number where a duration is expected, such as
/// <c>@wait 1</c> or <c>0.5s + 1</c>; otherwise the same as <paramref name="Type"/>.
/// </param>
internal readonly record struct ExpressionType(TypeSymbol Type, TypeSymbol ConvertedType);

/// <summary>
/// What the binder worked out about the syntax, for the compiler: which symbol each name refers to, and each
/// expression's type. Entries are keyed by the syntax node's identity, never its value, since two identical nodes in
/// different places are different nodes.
/// </summary>
/// <remarks>
/// A name is keyed by the node that spells it: the <see cref="NameSyntax"/> of a speaker, pose, command, markup, icon,
/// called function, named argument, or <c>@jump</c> and <c>@call</c> target; the <see cref="VariableExpressionSyntax"/>
/// of a variable; the <see cref="NameExpressionSyntax"/> of a bare name (an enum member, an actor or a node); and the
/// <see cref="TagSyntax"/> of a tag, whose enum value is in <see cref="TagValueOf"/>. A name that couldn't be resolved has
/// no entry. Every bound expression has a type, which is <see cref="TypeSymbol.Error"/> where it couldn't be known.
/// </remarks>
internal sealed class Bindings
{
    private readonly Dictionary<SyntaxNode, Symbol> symbols = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SyntaxNode, Symbol> tagValues = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ExpressionSyntax, ParameterSymbol> parameters = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ExpressionSyntax, ExpressionType> types = new(ReferenceEqualityComparer.Instance);

    /// <summary>The symbol a name refers to, or <see langword="null"/> if the name wasn't resolved.</summary>
    public Symbol? SymbolOf(SyntaxNode name) => symbols.GetValueOrDefault(name);

    /// <summary>The enum member a tag's value names, or <see langword="null"/> if the tag has no enum value or it isn't a member.</summary>
    public Symbol? TagValueOf(TagSyntax tag) => tagValues.GetValueOrDefault(tag);

    /// <summary>The type of an expression, and what its context converts it to, or <see langword="null"/> if it wasn't bound.</summary>
    public ExpressionType? TypeOf(ExpressionSyntax expression) => types.TryGetValue(expression, out ExpressionType type) ? type : null;

    /// <summary>The parameter an argument fills, or <see langword="null"/> if it fills none (it's extra, or repeated, or its owner doesn't exist).</summary>
    /// <param name="argument">The argument's value expression, which is how an argument is keyed whether it's positional or named.</param>
    public ParameterSymbol? ParameterOf(ExpressionSyntax argument) => parameters.GetValueOrDefault(argument);

    public void BindArgument(ExpressionSyntax argument, ParameterSymbol parameter) => parameters[argument] = parameter;

    public void BindName(SyntaxNode name, Symbol symbol) => symbols[name] = symbol;

    public void BindTagValue(TagSyntax tag, Symbol member) => tagValues[tag] = member;

    public void BindType(ExpressionSyntax expression, TypeSymbol type) => types[expression] = new(type, type);

    /// <summary>Records that the context uses a number as a duration. Any other type is left as it is.</summary>
    public void ConvertTo(ExpressionSyntax expression, TypeSymbol target)
    {
        if (types.TryGetValue(expression, out ExpressionType type) && type.Type == TypeSymbol.Number && target == TypeSymbol.Duration)
            types[expression] = type with { ConvertedType = target };
    }
}
