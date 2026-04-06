using System.Collections.Immutable;

namespace DJ.SourceGenerators.Models;

public sealed record EntityInfo(
    string FullTypeName,
    string TypeName,
    string Namespace,
    ImmutableArray<EntityPropertyInfo> Properties,
    ImmutableArray<EntityNavigationInfo> Navigations,
    string DbSetName);

public sealed record EntityPropertyInfo(
    string Name,
    string TypeName,
    bool IsNullable,
    bool IsKey,
    bool IsString,
    bool IsComparable,
    bool IsCollection,
    bool IsEnum);

public sealed record EntityNavigationInfo(
    string Name,
    string TypeName,
    bool IsCollection);
