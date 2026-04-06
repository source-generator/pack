using System.Collections.Immutable;

namespace DJ.SourceGenerators.Models;

public sealed record ViewModelInfo(
    string FullTypeName,
    string TypeName,
    string Namespace,
    ImmutableArray<ObservableFieldInfo> ObservableFields,
    ImmutableArray<RelayCommandInfo> Commands);

public sealed record ObservableFieldInfo(
    string FieldName,
    string PropertyName,
    string TypeName,
    bool IsNullable,
    ImmutableArray<string> NotifyCanExecuteChangedFor,
    ImmutableArray<string> AlsoNotifyFor);

public sealed record RelayCommandInfo(
    string MethodName,
    string CommandName,
    string? CanExecuteMethodName,
    bool IsAsync,
    bool HasParameter,
    string? ParameterTypeName,
    bool HasCancellationToken);
