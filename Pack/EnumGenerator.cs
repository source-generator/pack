using System.Collections.Immutable;
using DJ.SourceGenerators.Emitters;
using DJ.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DJ.SourceGenerators;

[Generator]
public class EnumGenerator : IIncrementalGenerator
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
            spc.AddSource("EnumAttributes.g.cs", EnumAttributeEmitter.Emit(rootNamespace));
        });

        var enumProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax eds && eds.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetEnumInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var collectedEnums = enumProvider.Collect();

        context.RegisterSourceOutput(collectedEnums, static (spc, enums) =>
        {
            if (enums.Length == 0) return;
            spc.AddSource("EnumExtensions.g.cs", EnumExtensionsEmitter.Emit(enums));
        });
    }

    private static EnumInfo? GetEnumInfo(GeneratorSyntaxContext context)
    {
        var enumDecl = (EnumDeclarationSyntax)context.Node;

        bool hasAttr = false;
        foreach (var attrList in enumDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "GenerateEnumExtensionsAttribute" or "GenerateEnumExtensions")
                {
                    hasAttr = true;
                    break;
                }
            }
            if (hasAttr) break;
        }
        if (!hasAttr) return null;

        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;
        if (symbol is null || symbol.TypeKind != TypeKind.Enum) return null;

        var underlyingType = symbol.EnumUnderlyingType?.ToDisplayString() ?? "int";
        var isFlags = symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");

        var members = ImmutableArray.CreateBuilder<EnumMemberInfo>();

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IFieldSymbol field || !field.HasConstantValue) continue;

            string? displayName = null;
            string? description = null;

            foreach (var attr in field.GetAttributes())
            {
                if (attr.AttributeClass?.Name is "DisplayAttribute")
                {
                    if (attr.ConstructorArguments.Length > 0)
                        displayName = attr.ConstructorArguments[0].Value?.ToString();

                    foreach (var namedArg in attr.NamedArguments)
                    {
                        if (namedArg.Key == "Description")
                            description = namedArg.Value.Value?.ToString();
                    }
                }
            }

            members.Add(new EnumMemberInfo(field.Name, field.ConstantValue, displayName, description));
        }

        if (members.Count == 0) return null;

        return new EnumInfo(
            symbol.ToDisplayString(), symbol.Name,
            symbol.ContainingNamespace.ToDisplayString(),
            underlyingType, isFlags, members.ToImmutable());
    }
}
