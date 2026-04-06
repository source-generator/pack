using DJ.SourceGenerators;
using Pack.Tests.Helpers;
using Xunit;

namespace Pack.Tests;

public class ValueObjectGeneratorTests
{
    [Fact]
    public void AlwaysEmitsValueObjectAttributes_EvenWithNoValueObjects()
    {
        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>("// empty", rootNamespace: "MyApp");

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ValueObjectAttributes.g.cs", hintNames);
    }

    [Fact]
    public void ValueObjectAttributes_ContainsExpectedNamespace()
    {
        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>("// empty", rootNamespace: "MyApp");

        var source = GeneratorTestHelper.GetGeneratedSource(result, "ValueObjectAttributes.g.cs");
        Assert.NotNull(source);
        Assert.Contains("namespace MyApp.ValueObjects", source);
        Assert.Contains("ValueObject", source);
    }

    [Fact]
    public void EmitsValueObjects_ForStructWithGenericAttribute()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<string>]
                public partial struct Name { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ValueObjects.g.cs", hintNames);
    }

    [Fact]
    public void EmitsValueObjects_ForClassWithTypeofArgument()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject(typeof(int))]
                public partial class Age { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ValueObjects.g.cs", hintNames);
    }

    [Fact]
    public void EmitsValueObjects_ForRecordWithGenericAttribute()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<string>]
                public partial record Email { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ValueObjects.g.cs", hintNames);
    }

    [Fact]
    public void DoesNotEmitValueObjects_ForUnmarkedType()
    {
        const string source = """
            namespace TestApp
            {
                public partial struct Name { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ValueObjects.g.cs", hintNames);
    }

    [Fact]
    public void GeneratedValueObject_ContainsCreateFactory()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<string>]
                public partial struct Name { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var voSource = GeneratorTestHelper.GetGeneratedSource(result, "ValueObjects.g.cs");
        Assert.NotNull(voSource);
        Assert.Contains("Create(", voSource);
        Assert.Contains("TryCreate(", voSource);
    }

    [Fact]
    public void GeneratedValueObject_ContainsValueProperty()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<int>]
                public partial struct Quantity { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var voSource = GeneratorTestHelper.GetGeneratedSource(result, "ValueObjects.g.cs");
        Assert.NotNull(voSource);
        Assert.Contains("Value", voSource);
    }

    [Fact]
    public void GeneratedValueObject_ContainsEqualityAndComparisonOperators()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<int>]
                public partial struct Temperature { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var voSource = GeneratorTestHelper.GetGeneratedSource(result, "ValueObjects.g.cs");
        Assert.NotNull(voSource);
        Assert.Contains("IEquatable<", voSource);
        Assert.Contains("IComparable<", voSource);
    }

    [Fact]
    public void GeneratedValueObject_ContainsIsValidMethod()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<string>]
                public partial struct Email { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var voSource = GeneratorTestHelper.GetGeneratedSource(result, "ValueObjects.g.cs");
        Assert.NotNull(voSource);
        Assert.Contains("IsValid(", voSource);
    }

    [Fact]
    public void GeneratedStructValueObject_UsesStructKeyword()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<int>]
                public partial struct Identifier { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var voSource = GeneratorTestHelper.GetGeneratedSource(result, "ValueObjects.g.cs");
        Assert.NotNull(voSource);
        Assert.Contains("partial struct Identifier", voSource);
    }

    [Fact]
    public void GeneratedClassValueObject_UsesClassKeyword()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject(typeof(string))]
                public partial class Description { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        var voSource = GeneratorTestHelper.GetGeneratedSource(result, "ValueObjects.g.cs");
        Assert.NotNull(voSource);
        Assert.Contains("partial class Description", voSource);
    }

    [Fact]
    public void NoGeneratorException_OnValidInput()
    {
        const string source = """
            namespace TestApp
            {
                [ValueObject<string>]
                public partial struct Name { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ValueObjectGenerator>(source);

        Assert.Null(result.Exception);
    }
}
