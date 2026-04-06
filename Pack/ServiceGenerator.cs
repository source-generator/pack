using System.Collections.Immutable;
using DJ.SourceGenerators.Emitters;
using DJ.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DJ.SourceGenerators;

[Generator]
public class ServiceGenerator : IIncrementalGenerator
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
            spc.AddSource("ServiceAttributes.g.cs", ServiceAttributeEmitter.Emit(rootNamespace));
        });

        var serviceProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => ExtractServiceInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var collectedServices = serviceProvider.Collect();
        var combined = collectedServices.Combine(rootNamespaceProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (services, rootNamespace) = tuple;
            if (services.Length == 0)
                return;

            var serviceList = services.Distinct().ToImmutableArray();

            spc.AddSource("ServiceInterfaces.g.cs", ServiceInterfaceEmitter.Emit(serviceList));

            var logging = serviceList.Where(s => s.GenerateLogging).ToImmutableArray();
            if (logging.Length > 0)
                spc.AddSource("LoggingDecorators.g.cs", LoggingDecoratorEmitter.Emit(logging));

            var timing = serviceList.Where(s => s.GenerateTiming).ToImmutableArray();
            if (timing.Length > 0)
                spc.AddSource("TimingDecorators.g.cs", TimingDecoratorEmitter.Emit(timing));

            var retry = serviceList.Where(s => s.GenerateRetry).ToImmutableArray();
            if (retry.Length > 0)
                spc.AddSource("RetryDecorators.g.cs", RetryDecoratorEmitter.Emit(retry));

            var cache = serviceList.Where(s => s.GenerateCache).ToImmutableArray();
            if (cache.Length > 0)
            {
                spc.AddSource("CacheDecorators.g.cs", CacheDecoratorEmitter.Emit(cache, rootNamespace));
                spc.AddSource("CacheKeys.g.cs", CacheKeyEmitter.Emit(cache));
                spc.AddSource("CacheEviction.g.cs", CacheEvictionEmitter.Emit(cache, rootNamespace));
            }

            var circuitBreaker = serviceList.Where(s => s.GenerateCircuitBreaker).ToImmutableArray();
            if (circuitBreaker.Length > 0)
                spc.AddSource("CircuitBreakerDecorators.g.cs", CircuitBreakerDecoratorEmitter.Emit(circuitBreaker));

            var validation = serviceList.Where(s => s.GenerateValidation).ToImmutableArray();
            if (validation.Length > 0)
                spc.AddSource("ValidationDecorators.g.cs", ValidationDecoratorEmitter.Emit(validation, rootNamespace));

            spc.AddSource("ServiceRegistration.g.cs", ServiceRegistrationEmitter.Emit(serviceList, rootNamespace));
        });
    }

    private static ServiceInfo? ExtractServiceInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        bool hasAttr = false;
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "ServiceAttribute" or "Service")
                {
                    hasAttr = true;
                    break;
                }
            }
            if (hasAttr) break;
        }
        if (!hasAttr) return null;

        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol symbol)
            return null;

        var serviceAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "ServiceAttribute" or "Service");
        if (serviceAttr is null) return null;

        // Extract attribute properties
        bool genLogging = true, genTiming = true, genRetry = false, genCache = false;
        bool genCircuitBreaker = false, genValidation = false;
        int retryMax = 3, retryDelay = 100, cacheDefault = 300;
        int cbFailureThreshold = 5, cbResetSeconds = 30;

        foreach (var namedArg in serviceAttr.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Logging": genLogging = (bool)namedArg.Value.Value!; break;
                case "Timing": genTiming = (bool)namedArg.Value.Value!; break;
                case "Retry": genRetry = (bool)namedArg.Value.Value!; break;
                case "Cache": genCache = (bool)namedArg.Value.Value!; break;
                case "CircuitBreaker": genCircuitBreaker = (bool)namedArg.Value.Value!; break;
                case "Validation": genValidation = (bool)namedArg.Value.Value!; break;
                case "RetryMaxAttempts": retryMax = (int)namedArg.Value.Value!; break;
                case "RetryInitialDelayMs": retryDelay = (int)namedArg.Value.Value!; break;
                case "CacheDefaultDurationSeconds": cacheDefault = (int)namedArg.Value.Value!; break;
                case "CircuitBreakerFailureThreshold": cbFailureThreshold = (int)namedArg.Value.Value!; break;
                case "CircuitBreakerResetSeconds": cbResetSeconds = (int)namedArg.Value.Value!; break;
            }
        }

        var methods = ExtractMethods(symbol);
        if (methods.Length == 0) return null;

        var ns = symbol.ContainingNamespace.ToDisplayString();
        var interfaceName = "I" + symbol.Name;
        var fullInterfaceName = $"{ns}.{interfaceName}";

        return new ServiceInfo(
            symbol.ToDisplayString(),
            symbol.Name,
            ns,
            interfaceName,
            fullInterfaceName,
            genLogging,
            genTiming,
            genRetry,
            retryMax,
            retryDelay,
            genCache,
            cacheDefault,
            genCircuitBreaker,
            cbFailureThreshold,
            cbResetSeconds,
            genValidation,
            methods);
    }

    private static ImmutableArray<ServiceMethodInfo> ExtractMethods(INamedTypeSymbol symbol)
    {
        var methods = ImmutableArray.CreateBuilder<ServiceMethodInfo>();

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IMethodSymbol method) continue;
            if (method.DeclaredAccessibility != Accessibility.Public) continue;
            if (method.IsStatic) continue;
            if (method.MethodKind != MethodKind.Ordinary) continue;

            var returnType = method.ReturnType.ToDisplayString();
            var isAsync = returnType.Contains("Task");
            var hasReturn = !returnType.Contains("void") &&
                           returnType != "System.Threading.Tasks.Task" &&
                           returnType != "Task";

            bool skipLogging = false, skipRetry = false, skipCache = false;
            bool cacheEnabled = false;
            int? cacheDuration = null;
            var cacheInvalidationTargets = ImmutableArray.CreateBuilder<string>();

            foreach (var attr in method.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;
                switch (attrName)
                {
                    case "NoLogAttribute" or "NoLog": skipLogging = true; break;
                    case "NoRetryAttribute" or "NoRetry": skipRetry = true; break;
                    case "NoCacheAttribute" or "NoCache": skipCache = true; break;
                    case "CacheAttribute" or "Cache":
                        cacheEnabled = true;
                        foreach (var arg in attr.ConstructorArguments)
                        {
                            if (arg.Value is int d && d > 0) cacheDuration = d;
                        }
                        foreach (var na in attr.NamedArguments)
                        {
                            if (na.Key == "DurationSeconds" && na.Value.Value is int ds) cacheDuration = ds;
                        }
                        break;
                    case "CacheInvalidateAttribute" or "CacheInvalidate":
                        foreach (var arg in attr.ConstructorArguments)
                        {
                            if (arg.Kind == TypedConstantKind.Array)
                            {
                                foreach (var item in arg.Values)
                                {
                                    if (item.Value is string mn) cacheInvalidationTargets.Add(mn);
                                }
                            }
                        }
                        break;
                }
            }

            var parameters = method.Parameters
                .Select(p =>
                {
                    var isCt = p.Type.ToDisplayString().Contains("CancellationToken");
                    var includeInKey = !isCt && !p.GetAttributes()
                        .Any(a => a.AttributeClass?.Name is "CacheIgnoreAttribute" or "CacheIgnore");
                    var auditIgnore = p.GetAttributes()
                        .Any(a => a.AttributeClass?.Name is "AuditIgnoreAttribute" or "AuditIgnore");
                    return new ServiceParameterInfo(p.Name, p.Type.ToDisplayString(), includeInKey, auditIgnore, isCt);
                })
                .ToImmutableArray();

            methods.Add(new ServiceMethodInfo(
                method.Name,
                returnType,
                isAsync,
                hasReturn,
                cacheDuration,
                cacheEnabled,
                cacheInvalidationTargets.ToImmutable(),
                skipLogging,
                skipRetry,
                skipCache,
                parameters));
        }

        return methods.ToImmutable();
    }
}
