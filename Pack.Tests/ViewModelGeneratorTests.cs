using DJ.SourceGenerators;
using Pack.Tests.Helpers;
using Xunit;

namespace Pack.Tests;

public class ViewModelGeneratorTests
{
    // Stub attribute definitions included in sources so the semantic model can resolve them.
    private const string AttributeStubs = """
        namespace StubAttrs
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class ViewModelAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Field)]
            public class ObservableAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class RelayCommandAttribute : System.Attribute
            {
                public string? CanExecute { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
            public class NotifyCanExecuteChangedAttribute : System.Attribute
            {
                public NotifyCanExecuteChangedAttribute(params string[] commands) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
            public class AlsoNotifyAttribute : System.Attribute
            {
                public AlsoNotifyAttribute(params string[] properties) { }
            }
        }
        """;

    [Fact]
    public void AlwaysEmitsViewModelAttributes_EvenWithNoViewModels()
    {
        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>("// empty", rootNamespace: "MyApp");

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ViewModelAttributes.g.cs", hintNames);
    }

    [Fact]
    public void ViewModelAttributes_ContainsExpectedNamespace()
    {
        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>("// empty", rootNamespace: "MyApp");

        var source = GeneratorTestHelper.GetGeneratedSource(result, "ViewModelAttributes.g.cs");
        Assert.NotNull(source);
        Assert.Contains("namespace MyApp.ViewModels", source);
        Assert.Contains("ViewModel", source);
    }

    [Fact]
    public void ReportsDiagnostic_VM001_WhenViewModelClassIsNotPartial()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public class NonPartialViewModel
                {
                    [Observable]
                    private string _name = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var diagnostics = result.Diagnostics;
        Assert.Contains(diagnostics, d => d.Id == "VM001");
    }

    [Fact]
    public void DoesNotEmitViewModels_WhenVM001DiagnosticIsTriggered()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public class NonPartialViewModel
                {
                    [Observable]
                    private string _name = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ViewModels.g.cs", hintNames);
    }

    [Fact]
    public void EmitsViewModels_ForPartialClassWithObservableFields()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class PersonViewModel
                {
                    [Observable]
                    private string _name = "";

                    [Observable]
                    private int _age;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("ViewModels.g.cs", hintNames);
    }

    [Fact]
    public void GeneratedViewModel_ContainsObservableProperties()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class PersonViewModel
                {
                    [Observable]
                    private string _firstName = "";

                    [Observable]
                    private int _age;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var vmSource = GeneratorTestHelper.GetGeneratedSource(result, "ViewModels.g.cs");
        Assert.NotNull(vmSource);
        Assert.Contains("FirstName", vmSource);
        Assert.Contains("Age", vmSource);
    }

    [Fact]
    public void GeneratedViewModel_ImplementsINotifyPropertyChanged()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class DashboardViewModel
                {
                    [Observable]
                    private string _title = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var vmSource = GeneratorTestHelper.GetGeneratedSource(result, "ViewModels.g.cs");
        Assert.NotNull(vmSource);
        Assert.Contains("INotifyPropertyChanged", vmSource);
    }

    [Fact]
    public void GeneratedViewModel_ContainsIsBusyProperty()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class MainViewModel
                {
                    [Observable]
                    private string _status = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var vmSource = GeneratorTestHelper.GetGeneratedSource(result, "ViewModels.g.cs");
        Assert.NotNull(vmSource);
        Assert.Contains("IsBusy", vmSource);
    }

    [Fact]
    public void EmitsRelayCommands_ForMethodsWithRelayCommandAttribute()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class ItemViewModel
                {
                    [Observable]
                    private string _title = "";

                    [RelayCommand]
                    public void Save() { }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var vmSource = GeneratorTestHelper.GetGeneratedSource(result, "ViewModels.g.cs");
        Assert.NotNull(vmSource);
        Assert.Contains("SaveCommand", vmSource);
    }

    [Fact]
    public void EmitsAsyncRelayCommand_ForAsyncMethod()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [ViewModel]
                public partial class DataViewModel
                {
                    [Observable]
                    private bool _isLoaded;

                    [RelayCommand]
                    public async Task LoadAsync() { }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var vmSource = GeneratorTestHelper.GetGeneratedSource(result, "ViewModels.g.cs");
        Assert.NotNull(vmSource);
        Assert.Contains("LoadCommand", vmSource);
    }

    [Fact]
    public void ReportsDiagnostic_VM004_WhenCanExecuteMethodIsMissing()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class ActionViewModel
                {
                    [Observable]
                    private string _label = "";

                    [RelayCommand(CanExecute = "CanDoAction")]
                    public void DoAction() { }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var diagnostics = result.Diagnostics;
        Assert.Contains(diagnostics, d => d.Id == "VM004");
    }

    [Fact]
    public void DoesNotEmitViewModels_ForClassWithoutViewModelAttribute()
    {
        const string source = """
            namespace TestApp
            {
                public partial class PlainClass
                {
                    private string _name = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ViewModels.g.cs", hintNames);
    }

    [Fact]
    public void DoesNotEmitViewModels_WhenNoObservablesOrCommands()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class EmptyViewModel { }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("ViewModels.g.cs", hintNames);
    }

    [Fact]
    public void FieldToPropertyName_UnderscoredField_BecomesCapitalizedProperty()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class NamingViewModel
                {
                    [Observable]
                    private string _firstName = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        var vmSource = GeneratorTestHelper.GetGeneratedSource(result, "ViewModels.g.cs");
        Assert.NotNull(vmSource);
        // The field '_firstName' should be exposed as a capitalized property 'FirstName'
        Assert.Contains("FirstName", vmSource);
        // No property getter/setter should be generated with the raw field name
        Assert.DoesNotContain("string _firstName { get;", vmSource);
    }

    [Fact]
    public void NoGeneratorException_OnValidInput()
    {
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [ViewModel]
                public partial class SimpleViewModel
                {
                    [Observable]
                    private string _title = "";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ViewModelGenerator>(
            new[] { AttributeStubs, source });

        Assert.Null(result.Exception);
    }
}
