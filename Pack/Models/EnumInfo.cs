using System.Collections.Immutable;

namespace DJ.SourceGenerators.Models;

public sealed record EnumInfo(
    string FullTypeName,
    string TypeName,
    string Namespace,
    string UnderlyingType,
    bool IsFlags,
    ImmutableArray<EnumMemberInfo> Members);

public sealed record EnumMemberInfo(
    string Name,
    object? Value,
    string? DisplayName,
    string? Description);
