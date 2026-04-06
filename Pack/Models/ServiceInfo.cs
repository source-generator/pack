using System.Collections.Immutable;

namespace DJ.SourceGenerators.Models;

public sealed record ServiceInfo(
    string FullTypeName,
    string TypeName,
    string Namespace,
    string InterfaceName,
    string FullInterfaceName,
    bool GenerateLogging,
    bool GenerateTiming,
    bool GenerateRetry,
    int RetryMaxAttempts,
    int RetryInitialDelayMs,
    bool GenerateCache,
    int CacheDefaultDurationSeconds,
    bool GenerateCircuitBreaker,
    int CircuitBreakerFailureThreshold,
    int CircuitBreakerResetSeconds,
    bool GenerateValidation,
    ImmutableArray<ServiceMethodInfo> Methods);

public sealed record ServiceMethodInfo(
    string Name,
    string ReturnType,
    bool IsAsync,
    bool HasReturn,
    int? CacheDurationSeconds,
    bool CacheEnabled,
    ImmutableArray<string> CacheInvalidationTargets,
    bool SkipLogging,
    bool SkipRetry,
    bool SkipCache,
    ImmutableArray<ServiceParameterInfo> Parameters);

public sealed record ServiceParameterInfo(
    string Name,
    string TypeName,
    bool IncludeInCacheKey,
    bool IsAuditIgnored,
    bool IsCancellationToken);
