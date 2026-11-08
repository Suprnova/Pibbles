using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>What an expression's type is, and the type its context converts it to.</summary>
/// <param name="Type">The type the expression has. <see cref="TypeSymbol.Error"/> if it can't be known.</param>
/// <param name="ConvertedType">
/// The type the context uses it as: <see cref="TypeSymbol.Duration"/> for a number where a duration is expected, such as
/// <c>@pause 1</c> or <c>0.5s + 1</c>; otherwise the same as <paramref name="Type"/>.
/// </param>
internal readonly record struct ExpressionType(TypeSymbol Type, TypeSymbol ConvertedType);

/// <summary>
/// What the binder worked out about the syntax, for the compiler: which symbol each name refers to, and each
/// expression's type. Entries are keyed by the syntax node's identity, never its value, since two identical nodes in
/// different places are different nodes.
/// </summary>
/// <remarks>
/// A name is keyed by the node that spells it: the <see cref="NameSyntax"/> of a speaker, channel value, command, markup, icon,
/// called function, named argument, node header, or <c>@jump</c> and <c>@call</c> target; the <see cref="VariableExpressionSyntax"/>
/// of a variable; the <see cref="NameExpressionSyntax"/> of a bare name (an enum member, an actor or a node); and the
/// <see cref="TagSyntax"/> of a tag, whose enum value is in <see cref="TagValueOf"/>. A name that couldn't be resolved has
/// no entry. Every bound expression has a type, which is <see cref="TypeSymbol.Error"/> where it couldn't be known.
/// </remarks>
internal sealed class Bindings
{
    private readonly Dictionary<SyntaxNode, Symbol> symbols = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TagSyntax, EnumMemberSymbol> tagValues = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ExpressionSyntax, ParameterSymbol> parameters = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ExpressionSyntax, ExpressionType> types = new(ReferenceEqualityComparer.Instance);

    /// <summary>Finds the symbol a name refers to.</summary>
    /// <param name="name">The node that spells the name.</param>
    /// <returns>The symbol, or <see langword="null"/> if the name wasn't resolved.</returns>
    public Symbol? SymbolOf(SyntaxNode name) => symbols.GetValueOrDefault(name);

    /// <summary>Finds the enum member a tag's value names.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The member, or <see langword="null"/> if the tag has no enum value or it isn't a member.</returns>
    public EnumMemberSymbol? TagValueOf(TagSyntax tag) => tagValues.GetValueOrDefault(tag);

    /// <summary>Finds the type of an expression, and what its context converts it to.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>Its types, or <see langword="null"/> if it wasn't bound.</returns>
    public ExpressionType? TypeOf(ExpressionSyntax expression) => types.TryGetValue(expression, out ExpressionType type) ? type : null;

    /// <summary>Finds the parameter an argument fills.</summary>
    /// <param name="argument">The argument's value expression, which is how an argument is keyed whether it's positional or named.</param>
    /// <returns>The parameter, or <see langword="null"/> if it fills none (it's extra, or repeated, or its owner doesn't exist).</returns>
    public ParameterSymbol? ParameterOf(ExpressionSyntax argument) => parameters.GetValueOrDefault(argument);

    /// <summary>Records the parameter an argument fills.</summary>
    /// <param name="argument">The argument's value expression.</param>
    /// <param name="parameter">The parameter it fills.</param>
    public void BindArgument(ExpressionSyntax argument, ParameterSymbol parameter) => parameters[argument] = parameter;

    /// <summary>Records the symbol a name refers to.</summary>
    /// <param name="name">The node that spells the name.</param>
    /// <param name="symbol">The symbol it refers to.</param>
    public void BindName(SyntaxNode name, Symbol symbol) => symbols[name] = symbol;

    /// <summary>Records the enum member a tag's value names.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="member">The member its value names.</param>
    public void BindTagValue(TagSyntax tag, EnumMemberSymbol member) => tagValues[tag] = member;

    /// <summary>Records the type of an expression, before its context converts it.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="type">Its type.</param>
    public void BindType(ExpressionSyntax expression, TypeSymbol type) => types[expression] = new(type, type);

    /// <summary>Records that the context uses a number as a duration. Any other type is left as it is.</summary>
    /// <param name="expression">The expression the context uses.</param>
    /// <param name="target">The type the context expects.</param>
    public void ConvertTo(ExpressionSyntax expression, TypeSymbol target)
    {
        if (types.TryGetValue(expression, out ExpressionType type) && type.Type == TypeSymbol.Number && target == TypeSymbol.Duration)
            types[expression] = type with { ConvertedType = target };
    }
}
