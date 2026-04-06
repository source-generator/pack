using System.Collections.Immutable;
using DJ.SourceGenerators.Emitters;
using DJ.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DJ.SourceGenerators;

[Generator]
public class StateMachineGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNamespaceProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
                return ns ?? "Generated";
            });

        // Always emit attributes so they're available for use
        context.RegisterSourceOutput(rootNamespaceProvider, static (spc, rootNamespace) =>
        {
            spc.AddSource("StateMachineAttributes.g.cs", StateMachineAttributeEmitter.Emit(rootNamespace));
        });

        // Find classes marked with [StateMachine]
        var machineProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => ExtractStateMachineInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var collectedMachines = machineProvider.Collect();
        var combined = collectedMachines.Combine(rootNamespaceProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (machines, rootNamespace) = tuple;
            if (machines.Length == 0) return;

            var machineList = machines.Distinct().ToImmutableArray();
            if (machineList.Length == 0) return;

            spc.AddSource("StateMachines.g.cs", StateMachineEmitter.Emit(machineList, rootNamespace));
        });
    }

    private static StateMachineInfo? ExtractStateMachineInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        // Check for [StateMachine] attribute
        bool hasAttr = false;
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "StateMachineAttribute" or "StateMachine")
                {
                    hasAttr = true;
                    break;
                }
            }
            if (hasAttr) break;
        }
        if (!hasAttr) return null;

        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol classSymbol)
            return null;

        // Get attribute data
        var attrData = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "StateMachineAttribute");
        if (attrData is null) return null;

        bool trackHistory = true;
        INamedTypeSymbol? stateEnumSymbol = null;

        // Check for StateEnum named argument
        foreach (var namedArg in attrData.NamedArguments)
        {
            if (namedArg.Key == "StateEnum" && namedArg.Value.Value is INamedTypeSymbol enumType)
                stateEnumSymbol = enumType;
            else if (namedArg.Key == "TrackHistory" && namedArg.Value.Value is bool th)
                trackHistory = th;
        }

        // If no explicit StateEnum, look for a nested enum
        if (stateEnumSymbol is null)
        {
            foreach (var member in classSymbol.GetTypeMembers())
            {
                if (member.TypeKind == TypeKind.Enum)
                {
                    stateEnumSymbol = member;
                    break;
                }
            }
        }

        if (stateEnumSymbol is null)
            return null;

        // Extract states from enum
        var states = ExtractStates(stateEnumSymbol);
        if (states.Length == 0) return null;

        // Determine initial state
        var initialState = states.FirstOrDefault(s => s.IsInitial);
        if (initialState is null)
            initialState = states[0]; // Default to first member

        // Extract transitions, OnEnter, OnExit from class methods
        var transitions = ImmutableArray.CreateBuilder<TransitionInfo>();
        var onEnterHooks = ImmutableArray.CreateBuilder<StateHookInfo>();
        var onExitHooks = ImmutableArray.CreateBuilder<StateHookInfo>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method) continue;

            foreach (var attr in method.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "TransitionAttribute")
                {
                    var transitionInfo = ExtractTransition(method, attr, stateEnumSymbol);
                    if (transitionInfo != null)
                        transitions.Add(transitionInfo);
                }
                else if (attr.AttributeClass?.Name == "OnEnterAttribute")
                {
                    var hookInfo = ExtractHook(method, attr, stateEnumSymbol);
                    if (hookInfo != null)
                        onEnterHooks.Add(hookInfo);
                }
                else if (attr.AttributeClass?.Name == "OnExitAttribute")
                {
                    var hookInfo = ExtractHook(method, attr, stateEnumSymbol);
                    if (hookInfo != null)
                        onExitHooks.Add(hookInfo);
                }
            }
        }

        if (transitions.Count == 0) return null;

        var ns = classSymbol.ContainingNamespace.ToDisplayString();

        return new StateMachineInfo(
            classSymbol.ToDisplayString(),
            classSymbol.Name,
            ns,
            stateEnumSymbol.ToDisplayString(),
            stateEnumSymbol.Name,
            initialState.Name,
            states,
            transitions.ToImmutable(),
            onEnterHooks.ToImmutable(),
            onExitHooks.ToImmutable(),
            trackHistory);
    }

    private static ImmutableArray<StateInfo> ExtractStates(INamedTypeSymbol enumSymbol)
    {
        var states = ImmutableArray.CreateBuilder<StateInfo>();

        foreach (var member in enumSymbol.GetMembers())
        {
            if (member is not IFieldSymbol field || !field.HasConstantValue) continue;

            bool isInitial = false;
            bool isFinal = false;
            string? displayName = null;

            foreach (var attr in field.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "InitialStateAttribute")
                    isInitial = true;
                else if (attr.AttributeClass?.Name == "FinalStateAttribute")
                    isFinal = true;
                else if (attr.AttributeClass?.Name == "DisplayAttribute" && attr.ConstructorArguments.Length > 0)
                    displayName = attr.ConstructorArguments[0].Value?.ToString();
            }

            states.Add(new StateInfo(field.Name, displayName, isInitial, isFinal));
        }

        return states.ToImmutable();
    }

    private static TransitionInfo? ExtractTransition(IMethodSymbol method, AttributeData attr, INamedTypeSymbol stateEnum)
    {
        if (attr.ConstructorArguments.Length < 2) return null;

        var fromValue = attr.ConstructorArguments[0];
        var toValue = attr.ConstructorArguments[1];

        var fromName = GetEnumMemberName(fromValue, stateEnum);
        var toName = GetEnumMemberName(toValue, stateEnum);

        if (fromName is null || toName is null) return null;

        string? guardMethod = null;
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key == "Guard" && namedArg.Value.Value is string g)
                guardMethod = g;
        }

        var isAsync = method.ReturnType.ToDisplayString().StartsWith("System.Threading.Tasks.Task");

        // The method itself is the effect — use its name
        var effectMethod = method.Name;

        // Derive transition name from method name (strip On prefix if present, or use as-is)
        var transitionName = method.Name;
        if (transitionName.StartsWith("On"))
            transitionName = transitionName.Substring(2);
        if (transitionName.EndsWith("Async"))
            transitionName = transitionName.Substring(0, transitionName.Length - 5);

        // Extract parameters (skip CancellationToken)
        var parameters = ImmutableArray.CreateBuilder<TransitionParameterInfo>();
        foreach (var param in method.Parameters)
        {
            if (param.Type.ToDisplayString() == "System.Threading.CancellationToken")
                continue;
            parameters.Add(new TransitionParameterInfo(param.Name, param.Type.ToDisplayString()));
        }

        return new TransitionInfo(
            transitionName,
            method.Name,
            fromName,
            toName,
            guardMethod,
            effectMethod,
            isAsync,
            parameters.ToImmutable());
    }

    private static StateHookInfo? ExtractHook(IMethodSymbol method, AttributeData attr, INamedTypeSymbol stateEnum)
    {
        if (attr.ConstructorArguments.Length < 1) return null;

        var stateName = GetEnumMemberName(attr.ConstructorArguments[0], stateEnum);
        if (stateName is null) return null;

        var stateFullName = $"{stateEnum.ToDisplayString()}.{stateName}";
        var isAsync = method.ReturnType.ToDisplayString().StartsWith("System.Threading.Tasks.Task");

        return new StateHookInfo(stateFullName, method.Name, isAsync);
    }

    private static string? GetEnumMemberName(TypedConstant constant, INamedTypeSymbol stateEnum)
    {
        // The value will be the underlying integer — find the matching field
        if (constant.Value is null) return null;

        foreach (var member in stateEnum.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue)
            {
                if (Equals(field.ConstantValue, constant.Value))
                    return field.Name;
            }
        }

        return null;
    }
}
