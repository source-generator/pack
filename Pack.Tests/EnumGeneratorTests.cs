using DJ.SourceGenerators;
using Pack.Tests.Helpers;
using Xunit;

namespace Pack.Tests;

public class EnumGeneratorTests
{
    [Fact]
    public void AlwaysEmitsEnumAttributes_EvenWithNoEnums()
    {
        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>("// empty", rootNamespace: "MyApp");

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("EnumAttributes.g.cs", hintNames);
    }

    [Fact]
    public void EnumAttributes_ContainsExpectedNamespace()
    {
        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>("// empty", rootNamespace: "MyApp");

        var source = GeneratorTestHelper.GetGeneratedSource(result, "EnumAttributes.g.cs");
        Assert.NotNull(source);
        Assert.Contains("namespace MyApp.Enums", source);
        Assert.Contains("GenerateEnumExtensions", source);
    }

    [Fact]
    public void EmitsEnumExtensions_ForEnumWithGenerateEnumExtensionsAttribute()
    {
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum Status { Active, Inactive, Pending }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("EnumExtensions.g.cs", hintNames);
    }

    [Fact]
    public void DoesNotEmitEnumExtensions_ForEnumWithoutAttribute()
    {
        const string source = """
            namespace TestApp
            {
                public enum Status { Active, Inactive, Pending }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("EnumExtensions.g.cs", hintNames);
    }

    [Fact]
    public void EmittedExtensions_ContainToDisplayString()
    {
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum OrderStatus { New, Processing, Shipped, Delivered }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EnumExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.Contains("ToDisplayString", extensionsSource);
        Assert.Contains("OrderStatusExtensions", extensionsSource);
    }

    [Fact]
    public void EmittedExtensions_ContainTryParse_And_Parse()
    {
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum Color { Red, Green, Blue }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EnumExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.Contains("TryParse", extensionsSource);
        Assert.Contains("Parse", extensionsSource);
        Assert.Contains("IsDefined", extensionsSource);
        Assert.Contains("GetValues", extensionsSource);
    }

    [Fact]
    public void EmitsFlagsMethods_ForFlagsEnum()
    {
        const string source = """
            namespace TestApp
            {
                [System.Flags]
                [GenerateEnumExtensions]
                public enum Permissions { None = 0, Read = 1, Write = 2, Execute = 4 }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EnumExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.Contains("GetFlags", extensionsSource);
        Assert.Contains("HasFlag", extensionsSource);
    }

    [Fact]
    public void DoesNotEmitFlagsMethods_ForNonFlagsEnum()
    {
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum Priority { Low, Medium, High }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EnumExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.DoesNotContain("GetFlags", extensionsSource);
    }

    [Fact]
    public void EmitsAllEnumMembers_InExtensions()
    {
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum Month { January, February, March }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EnumExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.Contains("January", extensionsSource);
        Assert.Contains("February", extensionsSource);
        Assert.Contains("March", extensionsSource);
    }

    [Fact]
    public void EmitsMultipleEnums_InSingleExtensionsFile()
    {
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum Status { Active, Inactive }

                [GenerateEnumExtensions]
                public enum Priority { Low, High }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Single(hintNames, n => n == "EnumExtensions.g.cs");

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EnumExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.Contains("StatusExtensions", extensionsSource);
        Assert.Contains("PriorityExtensions", extensionsSource);
    }

    [Fact]
    public void HandlesEmptyEnum_NoExtensionsGenerated()
    {
        // An enum with no members should not produce extensions (members.Count == 0 guard)
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum Empty { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("EnumExtensions.g.cs", hintNames);
    }

    [Fact]
    public void NoGeneratorException_OnValidInput()
    {
        const string source = """
            namespace TestApp
            {
                [GenerateEnumExtensions]
                public enum Status { Active, Inactive }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EnumGenerator>(source);

        Assert.Null(result.Exception);
    }
}
