using Pibbles.Semantics;
using Pibbles.Syntax;

namespace Pibbles.Compiler;

/// <summary>
/// An expression, lowered: a small tree that needs neither the syntax tree nor the bindings to be evaluated. Every
/// expression knows its <see cref="Type"/>, so the evaluator can tell a number sum from a duration sum and from joining text.
/// </summary>
/// <param name="Type">The type the expression has where it's used. A number that the context uses as a duration is wrapped in a <see cref="ToDurationExpr"/>, which has type <c>duration</c>.</param>
internal abstract record Expr(TypeSymbol Type);

/// <summary>A number literal.</summary>
internal sealed record NumberExpr(decimal Value) : Expr(TypeSymbol.Number);

/// <summary>A duration literal, in seconds.</summary>
internal sealed record DurationExpr(decimal Seconds) : Expr(TypeSymbol.Duration);

/// <summary>A string literal.</summary>
internal sealed record StringExpr(string Value) : Expr(TypeSymbol.String);

/// <summary><c>true</c> or <c>false</c>.</summary>
internal sealed record BoolExpr(bool Value) : Expr(TypeSymbol.Bool);

/// <summary>A member of an enum, written as a bare name.</summary>
/// <param name="Member">The member.</param>
/// <param name="EnumType">The enum it belongs to, as the type the name was read against.</param>
internal sealed record EnumMemberExpr(EnumMemberSymbol Member, TypeSymbol EnumType) : Expr(EnumType);

/// <summary>An actor, written as a bare name.</summary>
internal sealed record ActorExpr(ActorSymbol Actor) : Expr(TypeSymbol.Actor);

/// <summary>A node, written as a bare name.</summary>
/// <param name="Node">The node's current name, even if the story wrote an old one.</param>
internal sealed record NodeExpr(string Node) : Expr(TypeSymbol.Node);

/// <summary>A variable's current value.</summary>
internal sealed record VariableExpr(VariableSymbol Variable) : Expr(Variable.Type);

/// <summary>A call to a host function.</summary>
/// <param name="Function">The function.</param>
/// <param name="Arguments">The arguments, in the order of the function's parameters.</param>
internal sealed record CallExpr(FunctionSymbol Function, IReadOnlyList<Expr> Arguments) : Expr(Function.ReturnType);

/// <summary><c>visits(node)</c>, which the runtime answers itself.</summary>
/// <param name="Target">The node, an expression of type <c>node</c>.</param>
internal sealed record VisitsExpr(Expr Target) : Expr(TypeSymbol.Number);

/// <summary>A unary operator. The operand's type is <c>Operand.Type</c>.</summary>
internal sealed record UnaryExpr(UnaryOperator Operator, Expr Operand, TypeSymbol ResultType) : Expr(ResultType);

/// <summary>A binary operator. The operands' types are <c>Left.Type</c> and <c>Right.Type</c>, which say what kind of sum or comparison it is.</summary>
internal sealed record BinaryExpr(BinaryOperator Operator, Expr Left, Expr Right, TypeSymbol ResultType) : Expr(ResultType);

/// <summary>A number used as a duration in seconds, where its context expects a duration.</summary>
internal sealed record ToDurationExpr(Expr Operand) : Expr(TypeSymbol.Duration);
