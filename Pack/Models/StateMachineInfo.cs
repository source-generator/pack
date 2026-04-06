using System.Collections.Immutable;

namespace DJ.SourceGenerators.Models;

public sealed record StateMachineInfo(
    string FullTypeName,
    string TypeName,
    string Namespace,
    string StateEnumFullName,
    string StateEnumName,
    string InitialState,
    ImmutableArray<StateInfo> States,
    ImmutableArray<TransitionInfo> Transitions,
    ImmutableArray<StateHookInfo> OnEnterHooks,
    ImmutableArray<StateHookInfo> OnExitHooks,
    bool TrackHistory);

public sealed record StateInfo(
    string Name,
    string? DisplayName,
    bool IsInitial,
    bool IsFinal);

public sealed record TransitionInfo(
    string Name,
    string MethodName,
    string From,
    string To,
    string? GuardMethod,
    string? EffectMethod,
    bool IsAsync,
    ImmutableArray<TransitionParameterInfo> Parameters);

public sealed record TransitionParameterInfo(
    string Name,
    string TypeName);

public sealed record StateHookInfo(
    string StateName,
    string MethodName,
    bool IsAsync);
