using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Pack.Tests.Helpers;

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly Dictionary<string, string> _globalOptions;

    public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
    {
        _globalOptions = globalOptions;
    }

    public override AnalyzerConfigOptions GlobalOptions => new TestAnalyzerConfigOptions(_globalOptions);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
        new TestAnalyzerConfigOptions(_globalOptions);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
        new TestAnalyzerConfigOptions(_globalOptions);
}

internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _options;

    public TestAnalyzerConfigOptions(Dictionary<string, string> options)
    {
        _options = options;
    }

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
        _options.TryGetValue(key, out value);
}
