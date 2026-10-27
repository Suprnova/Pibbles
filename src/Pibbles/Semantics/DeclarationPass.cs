using System.Text;
using Pibbles.Diagnostics;
using Pibbles.Syntax;

namespace Pibbles.Semantics;

/// <summary>
/// Gathers every declaration and node name in a story, and the prelude's, into a <see cref="SymbolTable"/>. It expands
/// relative node names, and reports names that clash, are reserved or aren't ASCII, and types that don't exist.
/// </summary>
/// <remarks>
/// Enums are declared first and variables last, so every type is known before anything uses one, and a variable's
/// missing type can be suggested from every other name. Channels come before actors, whose blocks change the values
/// every actor has. A name with a problem is still declared, so the places that use it don't report it again; a
/// duplicate isn't, and the first declaration wins.
/// </remarks>
internal sealed class DeclarationPass(List<Diagnostic> diagnostics, ReferenceIndex references, Bindings bindings) : AnalysisPass(diagnostics, references)
{
    private readonly SymbolTable symbols = new();
    private readonly Dictionary<string, SourceLocation> nodeNames = [];
    private SourceLocation? narrationLocation;
    private bool builtIn;

    public static SymbolTable Run(IReadOnlyList<SyntaxTree> trees, List<Diagnostic> diagnostics, ReferenceIndex references, Bindings bindings)
    {
        var pass = new DeclarationPass(diagnostics, references, bindings);
        pass.ForEachFile(trees, pass.DeclareEnums);
        pass.ForEachFile(trees, pass.DeclareChannels);
        pass.ForEachFile(trees, pass.DeclareOthers);
        pass.ForEachFile(trees, pass.DeclareVariables);
        if (pass.narrationLocation is null)
            pass.symbols.Narration = new(null, null, [.. pass.symbols.Channels.Values.Select(channel => new SpeakerChannel(channel, channel.Defaults))]);

        return pass.symbols;
    }

    private void ForEachFile(IReadOnlyList<SyntaxTree> trees, Action<FileSyntax> declare)
    {
        foreach (SyntaxTree file in (IEnumerable<SyntaxTree>)[Prelude.Tree, .. trees])
        {
            Tree = file;
            builtIn = file == Prelude.Tree;
            declare(file.Root);
        }
    }

    private void DeclareEnums(FileSyntax file)
    {
        foreach (EnumDeclarationSyntax declaration in file.Declarations.OfType<EnumDeclarationSyntax>())
        {
            List<EnumMemberSymbol> members = DeclareList(declaration.Members, member => member, SymbolKind.EnumMember,
                $"the members of `{declaration.Name.Text}`", member => new EnumMemberSymbol(member.Text, Location(member.Span)));
            Declare(symbols.Enums, new EnumSymbol(declaration.Name.Text, Location(declaration.Name.Span), members), declaration.Name, SymbolKind.Enum);
        }
    }

    private void DeclareChannels(FileSyntax file)
    {
        foreach (ChannelDeclarationSyntax declaration in file.Declarations.OfType<ChannelDeclarationSyntax>())
        {
            List<ChannelValueSymbol> defaults = DeclareList(declaration.Values, value => value, SymbolKind.ChannelValue,
                $"the values of `{declaration.Name.Text}`", value => new ChannelValueSymbol(value.Text, Location(value.Span)));
            foreach (ChannelValueSymbol value in defaults)
            {
                if (symbols.Channels.Values.FirstOrDefault(other => other.Defaults.Any(known => known.Name == value.Name)) is { } sharing)
                    ReportSharedValue(declaration.Values.First(candidate => candidate.Text == value.Name), $"`{sharing.Name}`", declaration.Name.Text);
            }

            Declare(symbols.Channels, new ChannelSymbol(declaration.Name.Text, Location(declaration.Name.Span), defaults), declaration.Name, SymbolKind.Channel);
        }
    }

    private void ReportSharedValue(NameSyntax value, string holder, string channel) =>
        Report(DiagnosticCatalog.ValueInTwoChannels, value.Span, value.Text, holder, channel);

    private void DeclareOthers(FileSyntax file)
    {
        if (file.Prefix is { Name: { Text: ['.', ..] } name })
            Report(DiagnosticCatalog.RedundantPrefixDot, name.Span, name.Text, name.Text[1..]);

        foreach (DeclarationSyntax declaration in file.Declarations)
        {
            switch (declaration)
            {
                case ActorDeclarationSyntax actor:
                    DeclareActor(actor);
                    break;

                case NarrationDeclarationSyntax narration:
                    DeclareNarration(narration);
                    break;

                case CommandDeclarationSyntax command:
                    List<ParameterSymbol> commandParameters = DeclareParameters(command.Parameters, $"the parameters of `@{command.Name.Text}`");
                    Declare(symbols.Commands, new CommandSymbol(command.Name.Text, Location(command.Name.Span), commandParameters, command.IsInline, command.Waits), command.Name, SymbolKind.Command);
                    break;

                case MarkupDeclarationSyntax markup:
                    List<ParameterSymbol> markupParameters = DeclareParameters(markup.Parameters, $"the parameters of `[{markup.Name.Text}]`");
                    Declare(symbols.Markup, new MarkupSymbol(markup.Name.Text, Location(markup.Name.Span), markupParameters), markup.Name, SymbolKind.Markup);
                    break;

                case IconDeclarationSyntax icons:
                    foreach (NameSyntax icon in icons.Names)
                        Declare(symbols.Icons, new IconSymbol(icon.Text, Location(icon.Span)), icon, SymbolKind.Icon);
                    break;

                case TagDeclarationSyntax tags:
                    foreach (TagEntrySyntax tag in tags.Entries)
                        DeclareTag(tag);
                    break;

                case FunctionDeclarationSyntax function:
                    List<ParameterSymbol> functionParameters = DeclareParameters(function.Parameters, $"the parameters of `{function.Name.Text}()`");
                    var symbol = new FunctionSymbol(function.Name.Text, Location(function.Name.Span), functionParameters, ResolveType(function.ReturnType));
                    Declare(symbols.Functions, symbol, function.Name, SymbolKind.Function);
                    break;
            }
        }

        foreach (NodeSyntax node in file.Nodes)
            DeclareNode(node);
    }

    private void DeclareVariables(FileSyntax file)
    {
        foreach (VariableDeclarationSyntax declaration in file.Declarations.OfType<VariableDeclarationSyntax>())
        {
            var name = new NameSyntax(declaration.Variable.Name) { Span = declaration.Variable.Span };
            TypeSymbol type = declaration.Type is { } written ? ResolveType(written) : TypeOfValue(declaration);
            if (FullName(name.Text, name.Span, $"${name.Text}") is { } fullName)
                Declare(symbols.Variables, new VariableSymbol(fullName, Location(name.Span), type, declaration.Value), name, SymbolKind.Variable, $"${fullName}");
        }
    }

    private void DeclareActor(ActorDeclarationSyntax actor)
    {
        string displayName = actor.DisplayName ?? actor.Name.Text;
        string owner = actor.Name.IsMissing ? "this actor" : displayName;
        List<SpeakerChannel> channels = ResolveChannels(actor.Channels, owner);
        Declare(symbols.Actors, new ActorSymbol(actor.Name.Text, Location(actor.Name.Span), displayName, channels), actor.Name, SymbolKind.Actor);
    }

    private void DeclareNarration(NarrationDeclarationSyntax narration)
    {
        var keyword = new TextSpan(narration.Span.Start, "@narration".Length);
        if (narrationLocation is { } first)
        {
            Report(DiagnosticCatalog.SecondNarration, keyword, Where(first));
            return;
        }

        narrationLocation = Tree.Source.GetLocation(keyword);
        symbols.Narration = new NarrationSymbol(Location(keyword), narration.DisplayName, ResolveChannels(narration.Channels, "Narration"));
        DeclaresIfWritten(keyword, symbols.Narration);
    }

    /// <summary>
    /// Works out the channels an actor or narration has: every channel with its defaults, then the block applied. A channel
    /// the block drops is left out, one it replaces has the block's values, and one it adds to has the defaults and the
    /// block's values. A channel is mentioned once in a block, and the values of an actor's channels all differ.
    /// </summary>
    private List<SpeakerChannel> ResolveChannels(IReadOnlyList<ChannelEditSyntax> edits, string owner)
    {
        Dictionary<ChannelSymbol, List<ChannelValueSymbol>?> lists = symbols.Channels.Values.ToDictionary(channel => channel, channel => (List<ChannelValueSymbol>?)[.. channel.Defaults]);
        HashSet<string> written = [];
        List<(NameSyntax Syntax, ChannelValueSymbol Value, ChannelSymbol Channel)> introduced = [];

        foreach (ChannelEditSyntax edit in edits)
        {
            if (FindChannel(edit.Channel, written, owner) is not { } channel)
                continue;

            if (edit.Edit is ChannelEdit.Drop)
            {
                lists[channel] = null;
                continue;
            }

            List<ChannelValueSymbol> values = DeclareList(edit.Values, value => value, SymbolKind.ChannelValue,
                $"{owner}'s `{channel.Name}` values", value => new ChannelValueSymbol(value.Text, Location(value.Span)));
            List<ChannelValueSymbol> kept = edit.Edit is ChannelEdit.Add ? lists[channel]! : [];
            foreach (ChannelValueSymbol value in values)
            {
                NameSyntax syntax = edit.Values.First(candidate => candidate.Text == value.Name);
                if (kept.Any(known => known.Name == value.Name))
                    Report(DiagnosticCatalog.RepeatedName, syntax.Span, value.Name, $"{owner}'s `{channel.Name}` values");
                else
                    kept.Add(value);

                introduced.Add((syntax, value, channel));
            }

            lists[channel] = kept;
        }

        List<SpeakerChannel> channels = [.. lists.Where(list => list.Value is not null).Select(list => new SpeakerChannel(list.Key, list.Value!))];
        foreach ((NameSyntax syntax, ChannelValueSymbol value, ChannelSymbol channel) in introduced)
        {
            int order = channels.FindIndex(has => has.Channel == channel);
            bool IntroducedLater(SpeakerChannel other) =>
                channels.IndexOf(other) > order && introduced.Any(later => later.Channel == other.Channel && later.Value.Name == value.Name);

            if (channels.FirstOrDefault(other => other.Channel != channel && other.Values.Any(known => known.Name == value.Name) && !IntroducedLater(other)) is { } sharing)
                ReportSharedValue(syntax, $"{owner}'s `{sharing.Channel.Name}`", channel.Name);
        }

        return channels;
    }

    /// <summary>Finds the channel a block line names, reporting it if it isn't declared or the block already mentioned it.</summary>
    private ChannelSymbol? FindChannel(NameSyntax name, HashSet<string> written, string owner)
    {
        if (name.IsMissing)
            return null;

        if (!symbols.Channels.TryGetValue(name.Text, out ChannelSymbol? channel))
        {
            ReportWithOptionalHelp(DiagnosticCatalog.UnknownChannel, name.Span, name.Text, Suggestions.Closest(name.Text, symbols.Channels.Keys));
            return null;
        }

        Refers(name.Span, channel);
        if (written.Add(name.Text))
            return channel;

        Report(DiagnosticCatalog.RepeatedName, name.Span, name.Text, $"the channels in {owner}'s block");
        return null;
    }

    private void DeclareTag(TagEntrySyntax tag)
    {
        TypeSymbol? type = tag.Value is { } value ? ResolveType(value.Type) : null;
        if (type is not (null or EnumSymbol) && type != TypeSymbol.String && type != TypeSymbol.Error)
            Report(DiagnosticCatalog.TagValueType, tag.Value!.Type.Span, type.Name);

        Declare(symbols.Tags, new TagSymbol(tag.Name.Text, Location(tag.Name.Span), type, tag.Value?.AllowsEmpty ?? false), tag.Name, SymbolKind.Tag);
    }

    private List<ParameterSymbol> DeclareParameters(IReadOnlyList<ParameterSyntax> parameters, string listName)
    {
        foreach (ParameterSyntax parameter in parameters.SkipWhile(parameter => parameter.Default is null).Where(parameter => parameter.Default is null && !parameter.Name.IsMissing))
            Report(DiagnosticCatalog.RequiredAfterOptional, parameter.Span, parameter.Name.Text);

        return DeclareList(parameters, parameter => parameter.Name, SymbolKind.Parameter, listName,
            parameter => new ParameterSymbol(parameter.Name.Text, Location(parameter.Name.Span), ResolveType(parameter.Type), parameter.Default));
    }

    /// <summary>Declares the names in one declaration's list, such as the values of a channel, which have to differ from each other.</summary>
    private List<T> DeclareList<TSyntax, T>(IEnumerable<TSyntax> items, Func<TSyntax, NameSyntax> nameOf, SymbolKind kind, string listName, Func<TSyntax, T> create)
        where T : Symbol
    {
        List<T> declared = [];
        foreach (TSyntax item in items)
        {
            NameSyntax name = nameOf(item);
            if (name.IsMissing)
                continue;

            bool valid = CheckName(name, kind);
            if (declared.All(symbol => symbol.Name != name.Text))
            {
                T symbol = create(item);
                declared.Add(symbol);
                DeclaresIfWritten(name.Span, symbol);
            }
            else if (valid)
                Report(DiagnosticCatalog.RepeatedName, name.Span, name.Text, listName);
        }

        return declared;
    }

    private void DeclareNode(NodeSyntax node)
    {
        if (FullName(node.Name.Text, node.Name.Span) is not { } name || !DeclareNodeName(name, node.Name))
            return;

        List<string> aliases = [];
        var symbol = new NodeSymbol(name, Location(node.Name.Span), aliases);
        symbols.Nodes.Add(name, symbol);
        Declares(node.Name.Span, symbol);
        bindings.BindName(node.Name, symbol);

        foreach (NameSyntax alias in node.Aliases)
        {
            if (FullName(alias.Text, alias.Span) is { } old && DeclareNodeName(old, alias))
            {
                aliases.Add(old);
                symbols.Aliases.Add(old, symbol);
                Declares(alias.Span, symbol);
            }
        }
    }

    /// <summary>Claims a node name or alias, which share one namespace, reporting it if it's taken.</summary>
    private bool DeclareNodeName(string fullName, NameSyntax written)
    {
        bool valid = CheckName(written, SymbolKind.Node);
        if (nodeNames.TryGetValue(fullName, out SourceLocation first))
        {
            if (valid)
                Report(DiagnosticCatalog.DuplicateNode, written.Span, fullName, Where(first));

            return false;
        }

        nodeNames.Add(fullName, Tree.Source.GetLocation(written.Span));
        return true;
    }

    private void Declare<T>(Dictionary<string, T> table, T symbol, NameSyntax name, SymbolKind kind, string? shown = null)
        where T : Symbol
    {
        if (name.IsMissing)
            return;

        bool valid = CheckName(name, kind, shown);
        if (!table.TryGetValue(symbol.Name, out T? first))
        {
            table.Add(symbol.Name, symbol);
            DeclaresIfWritten(name.Span, symbol);
        }
        else if (valid)
            Report(DiagnosticCatalog.DuplicateDeclaration, name.Span, kind.Describe(), shown ?? name.Text, first.Location is { } location ? Where(location) : "built into Pibbles");
    }

    /// <summary>Reports a declared name that's reserved for its kind, or isn't ASCII. Returns whether the name is valid.</summary>
    private bool CheckName(NameSyntax name, SymbolKind kind, string? shown = null)
    {
        if (builtIn)
            return true;

        if (ReservedWords.UseOf(name.Text, kind) is { } use)
        {
            Report(DiagnosticCatalog.ReservedName, name.Span, shown ?? name.Text, kind.Describe(), use);
            return false;
        }

        int index = name.Text.AsSpan().IndexOfAnyExceptInRange('\0', '\x7F');
        if (index < 0)
            return true;

        Rune.DecodeFromUtf16(name.Text.AsSpan(index), out Rune character, out _);
        Report(DiagnosticCatalog.NonAsciiName, name.Span, shown ?? name.Text, character.ToString());
        return false;
    }

    private TypeSymbol ResolveType(NameSyntax name)
    {
        if (name.IsMissing)
            return TypeSymbol.Error;

        if (symbols.FindType(name.Text) is { } found)
        {
            if (found is EnumSymbol && !builtIn)
                Refers(name.Span, found);

            return found;
        }

        string? suggestion = Suggestions.Closest(name.Text, [.. TypeSymbol.BuiltIn.Select(type => type.Name), .. symbols.Enums.Keys]);
        ReportWithOptionalHelp(DiagnosticCatalog.UnknownType, name.Span, name.Text, suggestion);
        return TypeSymbol.Error;
    }

    /// <summary>The type of a variable with no type written, which its initial value gives, unless that value is a name.</summary>
    private TypeSymbol TypeOfValue(VariableDeclarationSyntax declaration)
    {
        switch (declaration.Value)
        {
            case NameExpressionSyntax name:
                string[] types = [.. TypesOfName(name.Name).Distinct()];
                string? written = types is [var type] ? $"@var ${declaration.Variable.Name}: {type} = {name.Name}" : null;
                ReportWithOptionalHelp(DiagnosticCatalog.UntypedVariable, name.Span, $"${declaration.Variable.Name}", name.Name, written);
                return TypeSymbol.Error;

            default:
                return TypeOfStartingValue(declaration.Value);
        }
    }

    /// <summary>The type a variable's starting value gives it when no type is written: a literal's, or <see cref="TypeSymbol.Error"/> for anything else.</summary>
    public static TypeSymbol TypeOfStartingValue(ExpressionSyntax value) => value switch
    {
        UnaryExpressionSyntax { Operand: var operand } => TypeOfLiteral(operand),
        _ => TypeOfLiteral(value),
    };

    private static TypeSymbol TypeOfLiteral(ExpressionSyntax literal) => literal switch
    {
        NumberLiteralSyntax => TypeSymbol.Number,
        DurationLiteralSyntax => TypeSymbol.Duration,
        StringLiteralSyntax => TypeSymbol.String,
        BooleanLiteralSyntax => TypeSymbol.Bool,
        _ => TypeSymbol.Error,
    };

    /// <summary>The types a bare name could be a value of: the enums it's a member of, <c>actor</c> and <c>node</c>.</summary>
    private IEnumerable<string> TypesOfName(string name)
    {
        if (name.Contains('.', StringComparison.Ordinal))
            return ["node"];

        IEnumerable<string> enums = symbols.Enums.Values.Where(@enum => @enum.Members.Any(member => member.Name == name)).Select(@enum => @enum.Name);
        return [.. enums, .. symbols.Actors.ContainsKey(name) ? ["actor"] : Array.Empty<string>(), .. nodeNames.ContainsKey(name) ? ["node"] : Array.Empty<string>()];
    }

    private SourceLocation? Location(TextSpan span) => builtIn ? null : Tree.Source.GetLocation(span);

    /// <summary>Records a declaration, unless it's built in, since nothing in the story writes those.</summary>
    private void DeclaresIfWritten(TextSpan span, Symbol symbol)
    {
        if (!builtIn)
            Declares(span, symbol);
    }
}
