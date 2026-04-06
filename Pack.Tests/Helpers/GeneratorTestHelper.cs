using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pack.Tests.Helpers;

public static class GeneratorTestHelper
{
    private static readonly MetadataReference[] DefaultReferences = BuildDefaultReferences();

    private static MetadataReference[] BuildDefaultReferences()
    {
        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator)
            .Where(s => !string.IsNullOrEmpty(s) && File.Exists(s));

        return trustedAssemblies
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }

    /// <summary>
    /// Runs an incremental source generator against a single source string and returns the run result.
    /// </summary>
    public static GeneratorRunResult RunGenerator<TGenerator>(
        string source,
        string rootNamespace = "TestApp")
        where TGenerator : IIncrementalGenerator, new()
    {
        return RunGenerator<TGenerator>(new[] { source }, rootNamespace);
    }

    /// <summary>
    /// Runs an incremental source generator against multiple source strings and returns the run result.
    /// </summary>
    public static GeneratorRunResult RunGenerator<TGenerator>(
        IEnumerable<string> sources,
        string rootNamespace = "TestApp")
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

        var syntaxTrees = sources
            .Select(s => CSharpSyntaxTree.ParseText(s, parseOptions))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: DefaultReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            new Dictionary<string, string>
            {
                ["build_property.RootNamespace"] = rootNamespace
            });

        var generator = new TGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Enumerable.Empty<AdditionalText>(),
            parseOptions: parseOptions,
            optionsProvider: optionsProvider);

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        return driver.GetRunResult().Results[0];
    }

    /// <summary>
    /// Returns the content of a generated source by hint name, or null if not found.
    /// </summary>
    public static string? GetGeneratedSource(GeneratorRunResult result, string hintName)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.Equals(hintName, StringComparison.OrdinalIgnoreCase));

        return source.SourceText?.ToString();
    }

    /// <summary>
    /// Returns all generated hint names in the run result.
    /// </summary>
    public static IReadOnlyList<string> GetGeneratedHintNames(GeneratorRunResult result)
    {
        return result.GeneratedSources.Select(s => s.HintName).ToList();
    }
}
