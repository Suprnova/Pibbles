using Pibbles.Compiler;
using Pibbles.Diagnostics;
using Pibbles.Semantics;

namespace Pibbles.Runtime;

/// <summary>
/// The functions a game provides for a story's <c>@function</c> declarations, such as
/// <c>@function has_item(id: string) -> bool</c>.
/// </summary>
/// <remarks>
/// <para>A function is a typed delegate of up to four parameters. Parameter and return types are strict:</para>
/// <list type="table">
/// <listheader><term>Story type</term><description>CLR type</description></listheader>
/// <item><term><c>bool</c></term><description><see cref="bool"/></description></item>
/// <item><term><c>number</c></term><description><see cref="decimal"/></description></item>
/// <item><term><c>string</c></term><description><see cref="string"/></description></item>
/// <item><term><c>duration</c></term><description><see cref="TimeSpan"/>, rounded to the nearest tick, and clamped to its range</description></item>
/// <item><term>an enum, <c>actor</c> or <c>node</c></term><description><see cref="string"/>: the member's name, the actor's ID or the node's current name</description></item>
/// </list>
/// <para>
/// Functions must be free of side effects: the story may call one more or fewer times than it reads, since
/// <c>and</c> and <c>or</c> short-circuit and text is rendered again after a load.
/// </para>
/// </remarks>
public sealed class HostFunctions
{
    private readonly Dictionary<string, Registration> functions = [];

    /// <summary>Registers a function with no parameters.</summary>
    /// <param name="name">The name the story declares.</param>
    /// <param name="function">The function.</param>
    /// <exception cref="ArgumentException">The name is already registered, is <c>visits</c>, or the function is null or uses a type that isn't supported.</exception>
    public HostFunctions Add<TResult>(string name, Func<TResult> function) =>
        Register(name, function, [], typeof(TResult), _ => function());

    /// <summary>Registers a function with one parameter.</summary>
    /// <inheritdoc cref="Add{TResult}"/>
    public HostFunctions Add<T1, TResult>(string name, Func<T1, TResult> function) =>
        Register(name, function, [typeof(T1)], typeof(TResult), a => function((T1)a[0]!));

    /// <summary>Registers a function with two parameters.</summary>
    /// <inheritdoc cref="Add{TResult}"/>
    public HostFunctions Add<T1, T2, TResult>(string name, Func<T1, T2, TResult> function) =>
        Register(name, function, [typeof(T1), typeof(T2)], typeof(TResult), a => function((T1)a[0]!, (T2)a[1]!));

    /// <summary>Registers a function with three parameters.</summary>
    /// <inheritdoc cref="Add{TResult}"/>
    public HostFunctions Add<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> function) =>
        Register(name, function, [typeof(T1), typeof(T2), typeof(T3)], typeof(TResult), a => function((T1)a[0]!, (T2)a[1]!, (T3)a[2]!));

    /// <summary>Registers a function with four parameters.</summary>
    /// <inheritdoc cref="Add{TResult}"/>
    public HostFunctions Add<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> function) =>
        Register(name, function, [typeof(T1), typeof(T2), typeof(T3), typeof(T4)], typeof(TResult), a => function((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, (T4)a[3]!));

    /// <summary>
    /// Finds the mismatches between the story's declared functions and the registered ones: a declared function that's
    /// missing, a registered one whose parameters or return type don't match, and a registered name the story doesn't
    /// declare, which is probably a typo. Call it at startup. It never throws.
    /// </summary>
    /// <param name="story">The compiled story.</param>
    /// <returns>The problems, or an empty list if everything matches.</returns>
    public IReadOnlyList<HostFunctionProblem> Validate(Story story)
    {
        List<HostFunctionProblem> problems = [];
        foreach ((string name, FunctionSymbol declared) in story.Functions)
        {
            if (!functions.TryGetValue(name, out Registration? registered))
            {
                problems.Add(new(HostFunctionProblemKind.Missing, name, $"The story declares `{name}`, which {Signature(declared)}, but no function with that name is registered."));
            }
            else if (!registered.Parameters.SequenceEqual(declared.Parameters.Select(parameter => ClrTypeOf(parameter.Type))) || registered.Returns != ClrTypeOf(declared.ReturnType))
            {
                problems.Add(new(HostFunctionProblemKind.WrongSignature, name, $"In the story, `{name}` {Signature(declared)}, but the registered function {Signature(registered)}."));
            }
        }

        problems.AddRange(functions.Keys
            .Where(name => !story.Functions.ContainsKey(name))
            .Select(name => new HostFunctionProblem(HostFunctionProblemKind.NotDeclared, name, $"`{name}` is registered, but the story doesn't declare it. Check the spelling.")));
        return problems;
    }

    /// <summary>Calls a function for the story: converts the arguments, runs the function and checks what it returns.</summary>
    /// <exception cref="InvalidOperationException">The function was never registered.</exception>
    /// <exception cref="HostFunctionException">The function threw, or returned a string that names nothing of its declared type.</exception>
    internal Value Invoke(Story story, CallExpr call, IReadOnlyList<Value> arguments, Action<RuntimeWarning> warn)
    {
        string name = call.Function.Name;
        if (!functions.TryGetValue(name, out Registration? registered))
            throw new InvalidOperationException($"The story calls `{name}`, but no function with that name is registered. Call HostFunctions.Validate when the game starts to find these.");

        object?[] hostArguments = [.. arguments.Select(argument => ToHost(argument, call.Location, warn))];
        try
        {
            return FromHost(registered.Invoke(hostArguments), call.Function, story);
        }
        catch (Exception exception)
        {
            throw new HostFunctionException(name, call.Location, exception);
        }
    }

    private HostFunctions Register(string name, Delegate function, Type[] parameters, Type returns, Func<object?[], object?> invoke)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(function);
        if (name is "visits")
            throw new ArgumentException("`visits` is answered by Pibbles itself and can't be registered.", nameof(name));

        foreach (Type type in (Type[])[.. parameters, returns])
        {
            if (type != typeof(bool) && type != typeof(decimal) && type != typeof(string) && type != typeof(TimeSpan))
                throw new ArgumentException($"`{name}` uses `{type.Name}`, which a story can't pass. Use bool, decimal, string or TimeSpan.", nameof(function));
        }

        return functions.TryAdd(name, new(parameters, returns, invoke)) ? this : throw new ArgumentException($"`{name}` is already registered.", nameof(name));
    }

    private static Type ClrTypeOf(TypeSymbol type) =>
        type == TypeSymbol.Bool ? typeof(bool)
        : type == TypeSymbol.Number ? typeof(decimal)
        : type == TypeSymbol.Duration ? typeof(TimeSpan)
        : typeof(string);

    private static string Signature(FunctionSymbol function) =>
        Signature(function.Parameters.Select(parameter => ClrTypeOf(parameter.Type)), ClrTypeOf(function.ReturnType));

    private static string Signature(Registration registration) => Signature(registration.Parameters, registration.Returns);

    private static string Signature(IEnumerable<Type> parameters, Type returns)
    {
        string[] names = [.. parameters.Select(type => $"a `{ClrName(type)}`")];
        string takes = names.Length switch { 0 => "takes nothing", 1 => $"takes {names[0]}", _ => $"takes {string.Join(", ", names[..^1])} and {names[^1]}" };
        return $"{takes} and returns a `{ClrName(returns)}`";
    }

    private static string ClrName(Type type) => type == typeof(bool) ? "bool" : type == typeof(decimal) ? "decimal" : type == typeof(string) ? "string" : "TimeSpan";

    private static object ToHost(Value value, SourceLocation location, Action<RuntimeWarning> warn)
    {
        if (value.Type == TypeSymbol.Bool)
            return value.AsBool;

        if (value.Type == TypeSymbol.Number)
            return value.AsDecimal;

        if (value.Type == TypeSymbol.String || value.Type == TypeSymbol.Node)
            return value.AsString;

        return value.Type == TypeSymbol.Duration ? Durations.ToTimeSpan(value.AsDecimal, location, warn) : value.AsSymbol.Name;
    }

    private static Value FromHost(object? result, FunctionSymbol function, Story story)
    {
        TypeSymbol type = function.ReturnType;
        if (type == TypeSymbol.Bool)
            return Value.Bool((bool)result!);

        if (type == TypeSymbol.Number)
            return Value.Number((decimal)result!);

        if (type == TypeSymbol.Duration)
            return Value.Duration(Durations.ToSeconds((TimeSpan)result!));

        string text = (string?)result ?? throw new InvalidOperationException($"`{function.Name}` returned null, but the story expects {type.Describe()}.");
        if (type == TypeSymbol.String)
            return Value.String(text);

        if (type is EnumSymbol @enum)
            return @enum.Members.FirstOrDefault(member => member.Name == text) is { } member ? Value.Member(member, @enum) : throw NotA(function, text, $"a member of `{@enum.Name}`");

        if (type == TypeSymbol.Actor)
            return story.Actors.TryGetValue(text, out ActorSymbol? actor) ? Value.Actor(actor) : throw NotA(function, text, "a declared actor");

        string node = story.Nodes.ContainsKey(text) ? text : story.Aliases.GetValueOrDefault(text) ?? throw NotA(function, text, "a node or an old name of one");
        return Value.Node(node);
    }

    private static InvalidOperationException NotA(FunctionSymbol function, string text, string expected) =>
        new($"`{function.Name}` returned \"{text}\", which isn't {expected}.");

    private sealed record Registration(Type[] Parameters, Type Returns, Func<object?[], object?> Invoke);
}

/// <summary>A mismatch between the story's declared functions and the registered ones, found by <see cref="HostFunctions.Validate"/>.</summary>
/// <param name="Kind">What kind of mismatch.</param>
/// <param name="Function">The function's name.</param>
/// <param name="Message">What's wrong and what to do about it, written for the game's developers.</param>
public sealed record HostFunctionProblem(HostFunctionProblemKind Kind, string Function, string Message);

/// <summary>The kinds of mismatch <see cref="HostFunctions.Validate"/> finds. New kinds can be added, so a caller that switches on it keeps a default arm.</summary>
public enum HostFunctionProblemKind
{
    /// <summary>The story declares the function, but none is registered.</summary>
    Missing,

    /// <summary>The registered function's parameters or return type don't match the declaration.</summary>
    WrongSignature,

    /// <summary>A function is registered that the story doesn't declare.</summary>
    NotDeclared,
}

/// <summary>A host function threw, or returned something the story can't use.</summary>
public sealed class HostFunctionException : Exception
{
    internal HostFunctionException(string function, SourceLocation location, Exception inner)
        : base($"The host function `{function}` failed when called at {location.Path} line {location.Start.Line + 1}: {inner.Message}", inner)
    {
        Function = function;
        Location = location;
    }

    /// <summary>The function's name.</summary>
    public string Function { get; }

    /// <summary>Where in the story the function was called.</summary>
    public SourceLocation Location { get; }
}
