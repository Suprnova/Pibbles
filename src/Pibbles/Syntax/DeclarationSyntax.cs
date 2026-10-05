namespace Pibbles.Syntax;

/// <summary>A declaration: part of the contract between the story and the host.</summary>
public abstract record DeclarationSyntax : SyntaxNode;

/// <summary><c>@actor id</c> and its properties: a character.</summary>
/// <param name="Name">The actor's ID, which lines use as their speaker.</param>
/// <param name="DisplayName">The <c>name:</c> property: the name the player sees, or <see langword="null"/> to show the ID.</param>
/// <param name="DisplayNameSpan">Where the display name is written, or <see langword="null"/>.</param>
/// <param name="Poses">The <c>poses:</c> property. The first pose is the default.</param>
public sealed record ActorDeclarationSyntax(NameSyntax Name, string? DisplayName, TextSpan? DisplayNameSpan, IReadOnlyList<NameSyntax> Poses) : DeclarationSyntax;

/// <summary><c>@enum name: a, b, c</c>: a closed set of values.</summary>
/// <param name="Name">The enum's name, which parameters and variables use as their type.</param>
/// <param name="Members">The enum's members, in order.</param>
public sealed record EnumDeclarationSyntax(NameSyntax Name, IReadOnlyList<NameSyntax> Members) : DeclarationSyntax;

/// <summary><c>@var $name [: type] = value</c>: a variable, saved with the game.</summary>
/// <param name="Variable">The variable.</param>
/// <param name="Type">The type written after the <c>:</c>, or <see langword="null"/> when it comes from the initial value.</param>
/// <param name="Value">The initial value: a literal, a negative number or duration, or a bare name.</param>
public sealed record VariableDeclarationSyntax(VariableExpressionSyntax Variable, NameSyntax? Type, ExpressionSyntax Value) : DeclarationSyntax;

/// <summary><c>@command name(params) [inline] [waits]</c>: an instruction the host carries out.</summary>
/// <param name="Name">The command's name.</param>
/// <param name="Parameters">The command's parameters.</param>
/// <param name="IsInline">Whether the command can appear inside text as <c>{@name …}</c>.</param>
/// <param name="Waits">Whether the story waits for the command to finish by default.</param>
public sealed record CommandDeclarationSyntax(NameSyntax Name, IReadOnlyList<ParameterSyntax> Parameters, bool IsInline, bool Waits) : DeclarationSyntax;

/// <summary><c>@markup name[(params)]</c>: a span tag.</summary>
/// <param name="Name">The markup's name.</param>
/// <param name="Parameters">The markup's parameters, which may be none.</param>
public sealed record MarkupDeclarationSyntax(NameSyntax Name, IReadOnlyList<ParameterSyntax> Parameters) : DeclarationSyntax;

/// <summary><c>@icon a, b, …</c>: icons that text can show with <c>{icon name}</c>.</summary>
/// <param name="Names">The icons' names.</param>
public sealed record IconDeclarationSyntax(IReadOnlyList<NameSyntax> Names) : DeclarationSyntax;

/// <summary><c>@tag a, b: type, …</c>: tags the host understands.</summary>
/// <param name="Entries">The declared tags.</param>
public sealed record TagDeclarationSyntax(IReadOnlyList<TagEntrySyntax> Entries) : DeclarationSyntax;

/// <summary>One tag in a <c>@tag</c> declaration: a flag, or a tag that takes a value of a type.</summary>
/// <param name="Name">The tag's name, without the <c>#</c>.</param>
/// <param name="Type">The type of the tag's value, or <see langword="null"/> for a flag that takes no value.</param>
/// <param name="AllowsEmpty">Whether the type is marked <c>?</c>, which allows an empty value (<c>#box:</c>).</param>
public sealed record TagEntrySyntax(NameSyntax Name, NameSyntax? Type, bool AllowsEmpty) : SyntaxNode;

/// <summary><c>@function name(params) -> type</c>: a host function usable in expressions.</summary>
/// <param name="Name">The function's name.</param>
/// <param name="Parameters">The function's parameters.</param>
/// <param name="ReturnType">The type the function returns.</param>
public sealed record FunctionDeclarationSyntax(NameSyntax Name, IReadOnlyList<ParameterSyntax> Parameters, NameSyntax ReturnType) : DeclarationSyntax;

/// <summary>A parameter of a command, markup or function: <c>name: type [= default]</c>.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="Type">The parameter's type.</param>
/// <param name="Default">The default value, or <see langword="null"/> for a required parameter.</param>
public sealed record ParameterSyntax(NameSyntax Name, NameSyntax Type, ExpressionSyntax? Default) : SyntaxNode;
