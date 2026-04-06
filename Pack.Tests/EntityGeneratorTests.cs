using DJ.SourceGenerators;
using Pack.Tests.Helpers;
using Xunit;

namespace Pack.Tests;

public class EntityGeneratorTests
{
    [Fact]
    public void AlwaysEmitsEntityAttributes_EvenWithNoEntities()
    {
        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>("// empty", rootNamespace: "MyApp");

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("EntityAttributes.g.cs", hintNames);
    }

    [Fact]
    public void AlwaysEmitsSpecificationBase_EvenWithNoEntities()
    {
        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>("// empty", rootNamespace: "MyApp");

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("SpecificationBase.g.cs", hintNames);
    }

    [Fact]
    public void EntityAttributes_ContainsExpectedNamespace()
    {
        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>("// empty", rootNamespace: "MyApp");

        var source = GeneratorTestHelper.GetGeneratedSource(result, "EntityAttributes.g.cs");
        Assert.NotNull(source);
        Assert.Contains("namespace MyApp.Entities", source);
        Assert.Contains("Entity", source);
    }

    [Fact]
    public void SpecificationBase_ContainsISpecificationInterface()
    {
        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>("// empty", rootNamespace: "MyApp");

        var source = GeneratorTestHelper.GetGeneratedSource(result, "SpecificationBase.g.cs");
        Assert.NotNull(source);
        Assert.Contains("ISpecification", source);
    }

    [Fact]
    public void EmitsDbContext_ForEntityClass()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Product
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ApplicationDbContext.g.cs", hintNames);
    }

    [Fact]
    public void EmitsQueryExtensions_ForEntityClass()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Order
                {
                    public int Id { get; set; }
                    public string Reference { get; set; } = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("EntityQueryExtensions.g.cs", hintNames);
    }

    [Fact]
    public void EmitsSpecifications_ForEntityClass()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Customer
                {
                    public int Id { get; set; }
                    public string Email { get; set; } = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("EntitySpecifications.g.cs", hintNames);
    }

    [Fact]
    public void DoesNotEmitEntityCode_ForUnmarkedClass()
    {
        const string source = """
            namespace TestApp
            {
                public class PlainClass
                {
                    public int Id { get; set; }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ApplicationDbContext.g.cs", hintNames);
        Assert.DoesNotContain("EntityQueryExtensions.g.cs", hintNames);
        Assert.DoesNotContain("EntitySpecifications.g.cs", hintNames);
    }

    [Fact]
    public void GeneratedDbContext_ContainsDbSetForEntity()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Invoice
                {
                    public int Id { get; set; }
                    public decimal Amount { get; set; }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var dbContextSource = GeneratorTestHelper.GetGeneratedSource(result, "ApplicationDbContext.g.cs");
        Assert.NotNull(dbContextSource);
        Assert.Contains("DbSet", dbContextSource);
        Assert.Contains("Invoice", dbContextSource);
    }

    [Fact]
    public void GeneratedExtensions_ContainsWhereId_Method()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Item
                {
                    public int Id { get; set; }
                    public string Code { get; set; } = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EntityQueryExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.Contains("WhereId", extensionsSource);
    }

    [Fact]
    public void GeneratedExtensions_ContainsStringFilterMethods_ForStringProperty()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Article
                {
                    public int Id { get; set; }
                    public string Title { get; set; } = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "EntityQueryExtensions.g.cs");
        Assert.NotNull(extensionsSource);
        Assert.Contains("TitleContains", extensionsSource);
    }

    [Fact]
    public void GeneratedSpecifications_ContainsEntitySpecificationBuilder()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Tag
                {
                    public int Id { get; set; }
                    public string Label { get; set; } = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var specsSource = GeneratorTestHelper.GetGeneratedSource(result, "EntitySpecifications.g.cs");
        Assert.NotNull(specsSource);
        Assert.Contains("TagSpecificationBuilder", specsSource);
    }

    [Fact]
    public void EntityWithNoPublicProperties_NoEntityCodeGenerated()
    {
        // Entity with only private properties should produce nothing (properties.Count == 0 guard)
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class NoProps
                {
                    private int _id;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ApplicationDbContext.g.cs", hintNames);
    }

    [Fact]
    public void NoGeneratorException_OnValidInput()
    {
        const string source = """
            namespace TestApp
            {
                [Entity]
                public class Widget
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<EntityGenerator>(source);

        Assert.Null(result.Exception);
    }
}
