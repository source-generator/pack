using System.Collections.Immutable;
using DJ.SourceGenerators.Emitters;
using DJ.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DJ.SourceGenerators;

[Generator]
public class EntityGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNamespaceProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
                return ns ?? "Generated";
            });

        // Emit attributes and specification base classes (always, so the attributes are available)
        context.RegisterSourceOutput(rootNamespaceProvider, static (spc, rootNamespace) =>
        {
            spc.AddSource("EntityAttributes.g.cs", EntityAttributeEmitter.Emit(rootNamespace));
            spc.AddSource("SpecificationBase.g.cs", SpecificationBaseEmitter.Emit(rootNamespace));
        });

        // Find classes marked with [Entity]
        var entityProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => ExtractEntityInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var configurationProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.BaseList is not null,
                transform: static (ctx, _) => ExtractEntityConfigurationInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var collectedEntities = entityProvider.Collect();
        var collectedConfigurations = configurationProvider.Collect();
        var combined = collectedEntities.Combine(collectedConfigurations).Combine(rootNamespaceProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var ((entities, configurations), rootNamespace) = tuple;
            if (entities.Length == 0)
                return;

            var entityList = entities.Distinct().ToImmutableArray();
            var configurationList = configurations.Distinct().ToImmutableArray();
            if (entityList.Length == 0)
                return;

            spc.AddSource("ApplicationDbContext.g.cs", DbContextEmitter.Emit(entityList, configurationList, rootNamespace));
            spc.AddSource("EntityQueryExtensions.g.cs", EntityExtensionsEmitter.Emit(entityList));
            spc.AddSource("EntitySpecifications.g.cs", SpecificationEmitter.Emit(entityList, rootNamespace));
        });
    }

    private static EntityConfigurationInfo? ExtractEntityConfigurationInfo(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.TypeParameters.Length > 0)
            return null;

        if (!ImplementsEntityTypeConfiguration(symbol))
            return null;

        var hasParameterlessConstructor = symbol.InstanceConstructors.Any(ctor =>
            ctor.Parameters.Length == 0 &&
            ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal);

        if (!hasParameterlessConstructor)
            return null;

        return new EntityConfigurationInfo(
            symbol.ToDisplayString(),
            symbol.Name,
            symbol.ContainingNamespace.ToDisplayString());
    }

    private static EntityInfo? ExtractEntityInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        bool hasAttr = false;
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "EntityAttribute" or "Entity")
                {
                    hasAttr = true;
                    break;
                }
            }
            if (hasAttr) break;
        }
        if (!hasAttr)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol symbol)
            return null;

        var properties = ImmutableArray.CreateBuilder<EntityPropertyInfo>();
        var navigations = ImmutableArray.CreateBuilder<EntityNavigationInfo>();

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            if (property.DeclaredAccessibility != Accessibility.Public)
                continue;

            if (property.IsStatic)
                continue;

            if (property.GetMethod is null)
                continue;

            if (HasAttribute(property, "QueryIgnoreAttribute"))
                continue;

            var propertyType = property.Type;
            var typeName = GetTypeName(propertyType);
            var isNullable = propertyType.NullableAnnotation == NullableAnnotation.Annotated ||
                             propertyType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

            if (IsNavigationProperty(property))
            {
                var isCollection = IsCollectionType(propertyType);
                var elementType = GetElementType(propertyType);
                navigations.Add(new EntityNavigationInfo(
                    property.Name,
                    elementType?.ToDisplayString() ?? typeName,
                    isCollection));
            }
            else
            {
                var isKey = HasAttribute(property, "KeyAttribute") ||
                            property.Name == "Id" ||
                            property.Name == $"{symbol.Name}Id";
                var isString = propertyType.SpecialType == SpecialType.System_String;
                var isComparable = IsComparableType(propertyType);
                var isEnum = propertyType.TypeKind == TypeKind.Enum;
                var isCollection = IsCollectionType(propertyType);

                properties.Add(new EntityPropertyInfo(
                    property.Name,
                    typeName,
                    isNullable,
                    isKey,
                    isString,
                    isComparable,
                    isCollection,
                    isEnum));
            }
        }

        if (properties.Count == 0)
            return null;

        var ns = symbol.ContainingNamespace.ToDisplayString();
        var dbSetName = symbol.Name + "s";

        return new EntityInfo(
            symbol.ToDisplayString(),
            symbol.Name,
            ns,
            properties.ToImmutable(),
            navigations.ToImmutable(),
            dbSetName);
    }

    private static string GetTypeName(ITypeSymbol type)
    {
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol nullable &&
            nullable.TypeArguments.Length > 0)
        {
            return nullable.TypeArguments[0].ToDisplayString();
        }

        var displayString = type.ToDisplayString();
        if (displayString.EndsWith("?"))
        {
            displayString = displayString.Substring(0, displayString.Length - 1);
        }

        return displayString;
    }

    private static bool IsComparableType(ITypeSymbol type)
    {
        var comparableInterface = type.AllInterfaces
            .Any(i => i.OriginalDefinition.ToDisplayString().StartsWith("System.IComparable"));

        if (comparableInterface)
            return true;

        return type.SpecialType switch
        {
            SpecialType.System_Int32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_Byte => true,
            SpecialType.System_Double => true,
            SpecialType.System_Single => true,
            SpecialType.System_Decimal => true,
            SpecialType.System_DateTime => true,
            SpecialType.System_String => true,
            _ => type.ToDisplayString() is "System.Guid" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly"
        };
    }

    private static bool ImplementsEntityTypeConfiguration(INamedTypeSymbol symbol)
    {
        return symbol.AllInterfaces.Any(i =>
            i.OriginalDefinition.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<"));
    }

    private static bool IsNavigationProperty(IPropertySymbol property)
    {
        var type = property.Type;

        if (type.SpecialType != SpecialType.None)
            return false;

        var typeName = type.ToDisplayString();

        if (typeName.StartsWith("System.") && !typeName.StartsWith("System.Collections"))
            return false;

        if (IsCollectionType(type))
        {
            var elementType = GetElementType(type);
            return elementType is INamedTypeSymbol namedElement &&
                   namedElement.TypeKind == TypeKind.Class &&
                   !namedElement.ToDisplayString().StartsWith("System.");
        }

        if (type is INamedTypeSymbol namedType &&
            namedType.TypeKind == TypeKind.Class &&
            !typeName.StartsWith("System."))
        {
            return true;
        }

        return false;
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        var typeName = namedType.OriginalDefinition.ToDisplayString();

        return typeName.StartsWith("System.Collections.Generic.ICollection<") ||
               typeName.StartsWith("System.Collections.Generic.IList<") ||
               typeName.StartsWith("System.Collections.Generic.List<") ||
               typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
               typeName.StartsWith("System.Collections.Generic.HashSet<") ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyList<") ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<");
    }

    private static ITypeSymbol? GetElementType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
            return namedType.TypeArguments[0];

        return null;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.Name == attributeName);
    }
}
