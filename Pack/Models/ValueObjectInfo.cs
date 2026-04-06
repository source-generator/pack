using System.Collections.Immutable;

namespace DJ.SourceGenerators.Models;

public sealed record ValueObjectInfo(
    string FullTypeName,
    string TypeName,
    string Namespace,
    string UnderlyingType,
    bool IsStruct,
    bool IsRecord,
    ImmutableArray<ValidationRule> ValidationRules);

public sealed record ValidationRule(
    string RuleType,
    string? Parameter);
