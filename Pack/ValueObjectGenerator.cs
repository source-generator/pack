using System.Collections.Immutable;
using DJ.SourceGenerators.Emitters;
using DJ.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DJ.SourceGenerators;

[Generator]
public class ValueObjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNamespaceProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
                return ns ?? "Generated";
            });

        context.RegisterSourceOutput(rootNamespaceProvider, static (spc, rootNamespace) =>
        {
            spc.AddSource("ValueObjectAttributes.g.cs", ValueObjectAttributeEmitter.Emit(rootNamespace));
        });

        var voProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax tds &&
                    tds is (StructDeclarationSyntax or ClassDeclarationSyntax or RecordDeclarationSyntax) &&
                    tds.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetValueObjectInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var allValueObjects = voProvider.Collect();
        var combined = allValueObjects.Combine(rootNamespaceProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (valueObjects, rootNamespace) = tuple;
            if (valueObjects.Length == 0) return;
            spc.AddSource("ValueObjects.g.cs", ValueObjectEmitter.Emit(valueObjects, rootNamespace));
        });
    }

    private static ValueObjectInfo? GetValueObjectInfo(GeneratorSyntaxContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;

        // Find ValueObject attribute in the syntax
        AttributeSyntax? voAttribute = null;
        foreach (var attrList in typeDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.StartsWith("ValueObject"))
                {
                    voAttribute = attr;
                    break;
                }
            }
            if (voAttribute != null) break;
        }
        if (voAttribute is null) return null;

        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;
        if (symbol is null) return null;

        // Extract the underlying type from the attribute syntax
        // For ValueObject<T>, the Name should be GenericNameSyntax
        string? underlyingTypeStr = null;
        
        if (voAttribute.Name is GenericNameSyntax genericName)
        {
            var typeArgs = genericName.TypeArgumentList.Arguments;
            if (typeArgs.Count > 0)
            {
                underlyingTypeStr = context.SemanticModel.GetTypeInfo(typeArgs[0]).Type?.ToDisplayString();
            }
        }
        else if (voAttribute.ArgumentList?.Arguments.Count > 0)
        {
            // For ValueObject(typeof(T))
            var arg = voAttribute.ArgumentList.Arguments[0];
            if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                underlyingTypeStr = context.SemanticModel.GetTypeInfo(typeOfExpr.Type).Type?.ToDisplayString();
            }
        }

        if (underlyingTypeStr != null)
        {
            return CreateInfo(symbol, underlyingTypeStr);
        }

        return null;
    }

    private static ValueObjectInfo CreateInfo(INamedTypeSymbol symbol, string underlyingType)
    {
        var validationRules = ImmutableArray.CreateBuilder<ValidationRule>();

        foreach (var attr in symbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            switch (name)
            {
                case "NotEmptyAttribute":
                    validationRules.Add(new ValidationRule("NotEmpty", null));
                    break;
                case "MinLengthAttribute" when attr.ConstructorArguments.Length > 0:
                    validationRules.Add(new ValidationRule("MinLength", attr.ConstructorArguments[0].Value?.ToString()));
                    break;
                case "MaxLengthAttribute" when attr.ConstructorArguments.Length > 0:
                    validationRules.Add(new ValidationRule("MaxLength", attr.ConstructorArguments[0].Value?.ToString()));
                    break;
                case "PatternAttribute" when attr.ConstructorArguments.Length > 0:
                    validationRules.Add(new ValidationRule("Pattern", attr.ConstructorArguments[0].Value?.ToString()));
                    break;
                case "MinValueAttribute" when attr.ConstructorArguments.Length > 0:
                    validationRules.Add(new ValidationRule("MinValue", attr.ConstructorArguments[0].Value?.ToString()));
                    break;
                case "MaxValueAttribute" when attr.ConstructorArguments.Length > 0:
                    validationRules.Add(new ValidationRule("MaxValue", attr.ConstructorArguments[0].Value?.ToString()));
                    break;
            }
        }

        return new ValueObjectInfo(
            symbol.ToDisplayString(), symbol.Name,
            symbol.ContainingNamespace.ToDisplayString(),
            underlyingType, symbol.IsValueType, symbol.IsRecord,
            validationRules.ToImmutable());
    }
}
