using Pibbles.Compiler;
using Pibbles.Syntax;
using static VerifyXunit.Verifier;

namespace Pibbles.Tests.Compiler;

/// <summary>
/// IR snapshots: each <c>.pib</c> input in <c>Snapshots/</c>, compiled with the shared <c>_defs.pib</c>, written as a
/// listing of its nodes' instructions and templates and compared with its <c>.verified.txt</c>. The sample story has one too.
/// </summary>
public class IrSnapshotTests
{
    private static readonly string Directory = Path.Combine(RepositoryRoot.Path, "tests", "Pibbles.Tests", "Compiler", "Snapshots");

    public static TheoryData<string> Inputs { get; } = [.. FindInputs().Select(path => Path.GetFileName(path)!)];

    [Theory]
    [MemberData(nameof(Inputs))]
    public Task Compile_SnapshotInput_MatchesVerifiedListing(string input)
    {
        string path = Path.Combine(Directory, input);

        return Verify(IrDump.Write(Compile([Source(path), Source(Path.Combine(Directory, "_defs.pib"))])))
            .UseDirectory(Directory)
            .UseFileName(Path.GetFileNameWithoutExtension(path));
    }

    [Fact]
    public Task Compile_KitchenStory_MatchesVerifiedListing()
    {
        string folder = Path.Combine(RepositoryRoot.Path, "samples", "kitchen");
        SourceText[] sources =
        [
            .. System.IO.Directory.EnumerateFiles(folder, "*.pib", SearchOption.AllDirectories)
                .Select(path => new SourceText(Path.GetRelativePath(RepositoryRoot.Path, path).Replace('\\', '/'), File.ReadAllText(path))),
        ];

        return Verify(IrDump.Write(Compile(sources))).UseDirectory(Directory).UseFileName("kitchen");
    }

    [Fact]
    public void Inputs_CoverEveryInstructionTemplateElementAndExpression()
    {
        HashSet<Type> seen = [];
        SourceText defs = Source(Path.Combine(Directory, "_defs.pib"));
        foreach (string path in FindInputs())
            IrDump.Write(Compile([Source(path), defs]), seen);

        string[] missing = [.. typeof(Story).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && (type.IsSubclassOf(typeof(Instruction)) || type.IsSubclassOf(typeof(TemplateElement)) || type.IsSubclassOf(typeof(Expr))) && !seen.Contains(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)];

        Assert.Empty(missing);
    }

    private static Story Compile(IEnumerable<SourceText> sources)
    {
        CompileResult result = StoryCompiler.Compile(sources);
        return result.Story ?? throw new Xunit.Sdk.XunitException(string.Join("\n", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}")));
    }

    private static SourceText Source(string path) => new(Path.GetFileName(path), File.ReadAllText(path));

    private static IEnumerable<string> FindInputs() =>
        System.IO.Directory.EnumerateFiles(Directory, "*.pib").Where(path => !Path.GetFileName(path).StartsWith('_'));
}
