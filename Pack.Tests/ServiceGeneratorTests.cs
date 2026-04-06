using DJ.SourceGenerators;
using Pack.Tests.Helpers;
using Xunit;

namespace Pack.Tests;

public class ServiceGeneratorTests
{
    // Stub ServiceAttribute so the semantic model can resolve it and extract named arguments.
    private const string ServiceAttributeStub = """
        namespace StubAttrs
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class ServiceAttribute : System.Attribute
            {
                public bool Logging { get; set; } = true;
                public bool Timing { get; set; } = true;
                public bool Retry { get; set; } = false;
                public bool Cache { get; set; } = false;
                public bool CircuitBreaker { get; set; } = false;
                public bool Validation { get; set; } = false;
                public int RetryMaxAttempts { get; set; } = 3;
                public int RetryInitialDelayMs { get; set; } = 100;
                public int CacheDefaultDurationSeconds { get; set; } = 300;
                public int CircuitBreakerFailureThreshold { get; set; } = 5;
                public int CircuitBreakerResetSeconds { get; set; } = 30;
            }
        }
        """;

    [Fact]
    public void AlwaysEmitsServiceAttributes_EvenWithNoServices()
    {
        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>("// empty", rootNamespace: "MyApp");

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ServiceAttributes.g.cs", hintNames);
    }

    [Fact]
    public void ServiceAttributes_ContainsExpectedContent()
    {
        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>("// empty", rootNamespace: "MyApp");

        var source = GeneratorTestHelper.GetGeneratedSource(result, "ServiceAttributes.g.cs");
        Assert.NotNull(source);
        Assert.Contains("Service", source);
        Assert.Contains("ICacheProvider", source);
    }

    [Fact]
    public void EmitsServiceInterface_ForMarkedServiceClass()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service]
                public class ProductService
                {
                    public Task<string> GetNameAsync(int id) => Task.FromResult("");
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ServiceInterfaces.g.cs", hintNames);
    }

    [Fact]
    public void GeneratedInterface_ContainsServiceMethods()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service]
                public class OrderService
                {
                    public Task PlaceOrderAsync(int orderId) => Task.CompletedTask;
                    public Task CancelOrderAsync(int orderId) => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var interfaceSource = GeneratorTestHelper.GetGeneratedSource(result, "ServiceInterfaces.g.cs");
        Assert.NotNull(interfaceSource);
        Assert.Contains("IOrderService", interfaceSource);
        Assert.Contains("PlaceOrderAsync", interfaceSource);
        Assert.Contains("CancelOrderAsync", interfaceSource);
    }

    [Fact]
    public void EmitsLoggingDecorator_ByDefault()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service]
                public class UserService
                {
                    public Task<string> GetUserAsync(int id) => Task.FromResult("");
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("LoggingDecorators.g.cs", hintNames);
    }

    [Fact]
    public void EmitsTimingDecorator_ByDefault()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service]
                public class ReportService
                {
                    public Task GenerateAsync() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("TimingDecorators.g.cs", hintNames);
    }

    [Fact]
    public void EmitsRetryDecorator_WhenRetryIsEnabled()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service(Retry = true)]
                public class PaymentService
                {
                    public Task<bool> ChargeAsync(decimal amount) => Task.FromResult(true);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("RetryDecorators.g.cs", hintNames);
    }

    [Fact]
    public void EmitsCacheDecorators_WhenCacheIsEnabled()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service(Cache = true)]
                public class CatalogService
                {
                    public Task<string[]> GetCategoriesAsync() => Task.FromResult(System.Array.Empty<string>());
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("CacheDecorators.g.cs", hintNames);
        Assert.Contains("CacheKeys.g.cs", hintNames);
        Assert.Contains("CacheEviction.g.cs", hintNames);
    }

    [Fact]
    public void EmitsCircuitBreakerDecorator_WhenCircuitBreakerIsEnabled()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service(CircuitBreaker = true)]
                public class ExternalApiService
                {
                    public Task<string> FetchDataAsync() => Task.FromResult("");
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("CircuitBreakerDecorators.g.cs", hintNames);
    }

    [Fact]
    public void EmitsServiceRegistration_ForServiceClass()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service]
                public class InventoryService
                {
                    public Task UpdateStockAsync(int productId, int qty) => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ServiceRegistration.g.cs", hintNames);
    }

    [Fact]
    public void ServiceRegistration_ContainsAddGeneratedServicesMethod()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service]
                public class NotificationService
                {
                    public Task SendAsync(string message) => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var registrationSource = GeneratorTestHelper.GetGeneratedSource(result, "ServiceRegistration.g.cs");
        Assert.NotNull(registrationSource);
        Assert.Contains("AddGeneratedServices", registrationSource);
    }

    [Fact]
    public void DoesNotEmitServiceCode_ForUnmarkedClass()
    {
        const string source = """
            using System.Threading.Tasks;
            namespace TestApp
            {
                public class PlainService
                {
                    public Task DoSomethingAsync() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ServiceInterfaces.g.cs", hintNames);
        Assert.DoesNotContain("ServiceRegistration.g.cs", hintNames);
    }

    [Fact]
    public void ServiceWithNoPublicMethods_NoServiceCodeGenerated()
    {
        // Service with no public methods should not produce service-specific files
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [Service]
                public class EmptyService
                {
                    private void InternalHelper() { }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ServiceInterfaces.g.cs", hintNames);
    }

    [Fact]
    public void NoGeneratorException_OnValidInput()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [Service]
                public class SimpleService
                {
                    public Task RunAsync() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceGenerator>(
            new[] { ServiceAttributeStub, source });

        Assert.Null(result.Exception);
    }
}
