# DJ Source Generators

Roslyn incremental source generators that reduce boilerplate through attribute-driven code generation.

## Generators

### Entity Generator (`[Entity]`)

Marks a class as a database entity. Generates:

- **`ApplicationDbContext`** with `DbSet<T>` properties for all entities
- **IQueryable extension methods** — `WhereId`, `Where{Prop}`, `Contains`/`StartsWith`/`EndsWith` for strings, `WhereHas{Prop}`/`WhereHasNo{Prop}` for nullables, `OrderBy{Prop}`, `Include{Nav}`
- **Specification pattern** — `ISpecification<T>`, `Specification<T>` base class, per-entity specs (`By{Prop}Spec`, `ContainsSpec`, `GreaterThanSpec`, `BetweenSpec`, `InSpec`, etc.), `And`/`Or`/`Not` combinators, and fluent `{Entity}SpecificationBuilder`

```csharp
using DJ.Entities;

namespace DJ.Domain;

[Entity]
public class Video
{
    public string Id { get; set; }        // Auto-detected as key (named "Id")
    public string Title { get; set; }
    public string? Description { get; set; }
    public long Duration { get; set; }
    public ICollection<PlaylistVideo> PlaylistVideos { get; set; }
}
```

**Attributes:**
| Attribute | Target | Description |
|-----------|--------|-------------|
| `[Entity]` | Class | Marks class as a database entity |
| `[Key]` | Property | Explicit primary key (auto-detected if named `Id` or `{Type}Id`) |
| `[QueryIgnore]` | Property | Exclude from generated extensions and specs |

**Generated usage:**
```csharp
// IQueryable extensions
var videos = await db.Videos
    .WhereTitleContains("hello")
    .WhereHasDescription()
    .OrderByDurationDescending()
    .IncludePlaylistVideos()
    .ToListAsync();

// Specification pattern
var spec = VideoSpecificationBuilder.Create()
    .WhereTitleContains("hello")
    .WhereDurationGreaterThan(60)
    .Build();

var filtered = db.Videos.WithSpecification(spec);
```

---

### Service Generator (`[Service]`)

Marks a class as a service. Generates:

- **Interface** — `I{ClassName}` extracted from public methods
- **Logging decorator** — entry/exit/error logging via `ILogger`
- **Timing decorator** — `Stopwatch`-based performance monitoring with slow threshold warnings
- **Retry decorator** — exponential backoff for async methods
- **Cache decorator** — `ICacheProvider`-based caching with key generation
- **Cache eviction interface** — `I{ClassName}CacheEviction` for targeted eviction by key or prefix
- **Circuit breaker decorator** — failure counting with auto-reset
- **Validation decorator** — resolves `IServiceValidator<T>` from DI for complex parameters
- **Composite DI registration** — `AddGeneratedServices()` registers all services with decorators in correct order

```csharp
using DJ.Services;

namespace DJ.Application;

[Service(Logging = true, Timing = true, Retry = true, Cache = true,
         RetryMaxAttempts = 3, CacheDefaultDurationSeconds = 600)]
public class VideoService
{
    [Cache(120)]
    public async Task<Video?> GetByIdAsync(string id, CancellationToken ct) { ... }

    [Cache(300)]
    public async Task<List<Video>> SearchAsync(string query, CancellationToken ct) { ... }

    [CacheInvalidate("GetByIdAsync", "SearchAsync")]
    public async Task UpdateAsync(Video video, CancellationToken ct) { ... }

    [NoLog, NoRetry]
    public async Task<int> CountAsync(CancellationToken ct) { ... }
}
```

**`[Service]` properties:**
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Logging` | bool | `true` | Enable logging decorator |
| `Timing` | bool | `true` | Enable timing decorator |
| `Retry` | bool | `false` | Enable retry decorator |
| `Cache` | bool | `false` | Enable cache decorator |
| `CircuitBreaker` | bool | `false` | Enable circuit breaker |
| `Validation` | bool | `false` | Enable validation decorator |
| `RetryMaxAttempts` | int | `3` | Max retry attempts |
| `RetryInitialDelayMs` | int | `100` | Initial retry delay (doubles each attempt) |
| `CacheDefaultDurationSeconds` | int | `300` | Default cache TTL |
| `CircuitBreakerFailureThreshold` | int | `5` | Failures before circuit opens |
| `CircuitBreakerResetSeconds` | int | `30` | Time before circuit resets |

**Method-level attributes:**
| Attribute | Description |
|-----------|-------------|
| `[Cache(seconds)]` | Enable caching for this method with optional custom duration |
| `[CacheInvalidate("Method1", "Method2")]` | Invalidate other methods' caches after this method runs |
| `[CacheIgnore]` | Exclude parameter from cache key (on parameter) |
| `[NoLog]` | Skip logging for this method |
| `[NoRetry]` | Skip retry for this method |
| `[NoCache]` | Skip caching for this method |

**Decorator application order** (outermost to innermost):
```
Validation → CircuitBreaker → Retry → Cache → Timing → Logging → Implementation
```

**Cache eviction:**
```csharp
// Generated interface for targeted cache eviction
public interface IVideoServiceCacheEviction
{
    Task EvictGetByIdAsyncAsync(string id, CancellationToken ct = default);
    Task EvictSearchAsyncAsync(string query, CancellationToken ct = default);
    Task EvictByMethodAsync(string methodName, CancellationToken ct = default);
    Task EvictAllAsync(CancellationToken ct = default);
}
```

**DI registration:**
```csharp
// Register all services with their decorators
services.AddGeneratedServices();

// Or register individually
services.AddVideoService();
```

**Required interfaces to implement:**
| Interface | Required when | Purpose |
|-----------|---------------|---------|
| `ICacheProvider` | `Cache = true` | Get/Set/Remove/RemoveByPrefix cache entries |
| `IServiceValidator<T>` | `Validation = true` | Validate method parameters |

---

### Enum Generator (`[GenerateEnumExtensions]`)

Generates high-performance extension methods for enums — display names, descriptions, parsing, and `[Flags]` helpers.

```csharp
using DJ.Enums;

namespace DJ.Domain;

[GenerateEnumExtensions]
public enum VideoStatus
{
    [Display("Pending Review", Description = "Awaiting moderation")]
    Pending,

    [Display("Published")]
    Published,

    [Display("Archived")]
    Archived
}
```

**Generates:**
- `ToDisplayString()` — returns display name or member name
- `GetDescription()` — returns description (if any members have one)
- `GetValues()` — cached `IReadOnlyList<T>` of all members
- `IsDefined(T)` / `IsDefined(int)` — fast membership check
- `TryParse` / `Parse` — match by name or display name, with `ignoreCase` option
- `[Flags]` enums also get: `HasFlag()`, `GetFlags()` (individual flags iterator)

**Attributes:**
| Attribute | Target | Description |
|-----------|--------|-------------|
| `[GenerateEnumExtensions]` | Enum | Marks enum for extension generation |
| `[Display("Name")]` | Field | Display name + optional `Description` |

---

### ValueObject Generator (`[ValueObject]`)

Generates strongly-typed value objects with validation, equality, comparison, implicit conversions, and EF Core value converters.

```csharp
using DJ.ValueObjects;

namespace DJ.Domain;

[ValueObject<string>]
[NotEmpty]
[MinLength(3)]
[MaxLength(100)]
public readonly partial record struct VideoTitle;

[ValueObject(typeof(int))]
[MinValue(0)]
[MaxValue(100)]
public readonly partial record struct Percentage;

[ValueObject<string>]
[NotEmpty]
[Pattern(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
public readonly partial record struct Email;
```

**Generates:**
- `Value` property (underlying type)
- `Create(T)` — factory with validation, throws `ValueObjectValidationException`
- `TryCreate(T, out result)` — non-throwing factory
- `IsValid(T)` — static validation check
- `IEquatable<T>`, `IComparable<T>` implementations
- `==`, `!=`, `<`, `<=`, `>`, `>=` operators
- Implicit conversion operators (both directions)
- `ToString()` override
- Struct-specific: `Empty`, `IsEmpty`
- **EF Core `ValueConverter<T, TUnderlying>`** for database mapping

**Attributes:**
| Attribute | Target | Description |
|-----------|--------|-------------|
| `[ValueObject<T>]` | Struct/Class | Marks type as value object wrapping `T` |
| `[ValueObject(typeof(T))]` | Struct/Class | Alternative non-generic form |
| `[NotEmpty]` | Type | Value cannot be null/empty/default |
| `[MinLength(n)]` | Type | Minimum string length |
| `[MaxLength(n)]` | Type | Maximum string length |
| `[Pattern("regex")]` | Type | Regex pattern for strings |
| `[MinValue(n)]` | Type | Minimum numeric value |
| `[MaxValue(n)]` | Type | Maximum numeric value |

---

### ViewModel Generator (`[ViewModel]`)

Marks a partial class as a ViewModel. Generates `INotifyPropertyChanged`, observable properties, relay commands with IsBusy/error tracking. Works with both MAUI and Blazor WASM.

```csharp
using DJ.ViewModels;

namespace DJ.App.ViewModels;

[ViewModel]
public partial class VideoDetailViewModel
{
    private readonly IVideoService _videoService;

    public VideoDetailViewModel(IVideoService videoService)
    {
        _videoService = videoService;
    }

    [Observable]
    [NotifyCanExecuteChanged(nameof(SaveCommand))]
    private string _title;

    [Observable]
    [AlsoNotify(nameof(FullName))]
    private string _firstName;

    [Observable]
    private bool _isLoading;

    public string FullName => $"{FirstName} {_title}";

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        await _videoService.SaveAsync(_title, ct);
    }

    private bool CanSave() => !string.IsNullOrEmpty(Title);

    [RelayCommand]
    private void Reset()
    {
        Title = string.Empty;
    }
}
```

**Generates:**
- `INotifyPropertyChanged` implementation with `SetProperty<T>` and `OnPropertyChanged`
- `IsBusy` property — auto-set `true` during async command execution
- `HasErrors` / `ErrorMessage` — auto-set on async command failure
- `ClearErrors()` helper method
- Observable properties from `[Observable]` fields (`_title` → `Title`)
- `RelayCommand` / `AsyncRelayCommand` from `[RelayCommand]` methods
- Parameterized commands: `RelayCommand<T>` / `AsyncRelayCommand<T>`

**Attributes:**
| Attribute | Target | Description |
|-----------|--------|-------------|
| `[ViewModel]` | Class | Marks partial class as a ViewModel |
| `[Observable]` | Field | Generates property with change notification |
| `[RelayCommand]` | Method | Generates ICommand property |
| `[NotifyCanExecuteChanged]` | Field | Re-evaluate command CanExecute when property changes |
| `[AlsoNotify]` | Field | Also raise PropertyChanged for other properties |

**Relay command naming:** `SaveAsync` → `SaveCommand`, `Delete` → `DeleteCommand`

**Async commands** automatically:
- Set `IsBusy = true` during execution
- Prevent concurrent execution
- Catch exceptions and set `ErrorMessage`
- Call `NotifyCanExecuteChanged` on start/finish

**Generated types** (in `{RootNamespace}.ViewModels`):
- `RelayCommand` / `RelayCommand<T>` — synchronous ICommand
- `AsyncRelayCommand` / `AsyncRelayCommand<T>` — async ICommand with `ExecuteAsync()`
- Both support `NotifyCanExecuteChanged()` for manual re-evaluation

---

## Project Setup

The generator targets `netstandard2.0` and is referenced as an analyzer:

```xml
<ProjectReference Include="..\..\tools\SourceGenerators\SourceGenerators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Set `RootNamespace` on consuming projects to control generated namespaces:
- Attributes: `{RootNamespace}.Entities`, `{RootNamespace}.Services`, `{RootNamespace}.Specifications`
- DbContext: `{RootNamespace}.Infrastructure`
- Decorators/Interfaces: same namespace as the source class

## File Structure

```
tools/SourceGenerators/
├── EntityGenerator.cs              # [Entity] generator
├── ServiceGenerator.cs             # [Service] generator
├── ViewModelGenerator.cs           # [ViewModel] generator
├── EnumGenerator.cs                # [GenerateEnumExtensions] generator
├── ValueObjectGenerator.cs         # [ValueObject] generator
├── Models/
│   ├── EntityInfo.cs               # Entity metadata records
│   ├── ServiceInfo.cs              # Service metadata records
│   ├── ViewModelInfo.cs            # ViewModel metadata records
│   ├── EnumInfo.cs                 # Enum metadata records
│   └── ValueObjectInfo.cs          # ValueObject metadata records
├── Emitters/
│   ├── EntityAttributeEmitter.cs   # [Entity], [Key], [QueryIgnore]
│   ├── DbContextEmitter.cs         # ApplicationDbContext with DbSets
│   ├── EntityExtensionsEmitter.cs  # IQueryable Where/OrderBy/Include
│   ├── SpecificationBaseEmitter.cs # ISpecification<T>, And/Or/Not
│   ├── SpecificationEmitter.cs     # Per-entity specs + builder
│   ├── ServiceAttributeEmitter.cs  # [Service], [Cache], etc.
│   ├── ServiceInterfaceEmitter.cs  # I{ClassName} extraction
│   ├── LoggingDecoratorEmitter.cs  # Logging decorator
│   ├── TimingDecoratorEmitter.cs   # Timing decorator
│   ├── RetryDecoratorEmitter.cs    # Retry decorator
│   ├── CacheDecoratorEmitter.cs    # Cache decorator
│   ├── CacheKeyEmitter.cs          # Cache key builders
│   ├── CacheEvictionEmitter.cs     # I{Class}CacheEviction
│   ├── CircuitBreakerDecoratorEmitter.cs
│   ├── ValidationDecoratorEmitter.cs
│   ├── ServiceRegistrationEmitter.cs # DI extensions
│   ├── ViewModelAttributeEmitter.cs  # [ViewModel], relay command types
│   ├── ViewModelEmitter.cs          # INPC, properties, commands
│   ├── EnumAttributeEmitter.cs      # [GenerateEnumExtensions], [Display]
│   ├── EnumExtensionsEmitter.cs     # Enum extension methods
│   ├── ValueObjectAttributeEmitter.cs # [ValueObject], validation attrs
│   └── ValueObjectEmitter.cs        # VO implementation + EF converter
└── IsExternalInit.cs               # netstandard2.0 polyfill
```
