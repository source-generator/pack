using DJ.SourceGenerators;
using Pack.Tests.Helpers;
using Xunit;

namespace Pack.Tests;

public class StateMachineGeneratorTests
{
    // Minimal stubs for the state machine attributes so the semantic model can resolve them.
    private const string StateMachineAttributeStubs = """
        namespace StubAttrs
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class StateMachineAttribute : System.Attribute
            {
                public System.Type? StateEnum { get; set; }
                public bool TrackHistory { get; set; } = true;
            }

            [System.AttributeUsage(System.AttributeTargets.Field)]
            public class InitialStateAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Field)]
            public class FinalStateAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class TransitionAttribute : System.Attribute
            {
                public TransitionAttribute(object from, object to) { }
                public string? Guard { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class OnEnterAttribute : System.Attribute
            {
                public OnEnterAttribute(object state) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class OnExitAttribute : System.Attribute
            {
                public OnExitAttribute(object state) { }
            }
        }
        """;

    [Fact]
    public void AlwaysEmitsStateMachineAttributes_EvenWithNoMachines()
    {
        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>("// empty", rootNamespace: "MyApp");

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("StateMachineAttributes.g.cs", hintNames);
    }

    [Fact]
    public void StateMachineAttributes_ContainsExpectedContent()
    {
        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>("// empty", rootNamespace: "MyApp");

        var source = GeneratorTestHelper.GetGeneratedSource(result, "StateMachineAttributes.g.cs");
        Assert.NotNull(source);
        Assert.Contains("StateMachine", source);
        Assert.Contains("Transition", source);
        Assert.Contains("InitialState", source);
        Assert.Contains("FinalState", source);
    }

    [Fact]
    public void EmitsStateMachine_ForClassWithStateMachineAttributeAndNestedEnum()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [StateMachine]
                public partial class OrderStateMachine
                {
                    public enum State { Pending, Processing, Shipped, Delivered }

                    [Transition(State.Pending, State.Processing)]
                    public Task OnProcess() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(
            new[] { StateMachineAttributeStubs, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.Contains("StateMachines.g.cs", hintNames);
    }

    [Fact]
    public void DoesNotEmitStateMachine_ForUnmarkedClass()
    {
        const string source = """
            namespace TestApp
            {
                public class PlainClass
                {
                    public enum State { A, B }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(source);

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("StateMachines.g.cs", hintNames);
    }

    [Fact]
    public void GeneratedStateMachine_ContainsCurrentStateProperty()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [StateMachine]
                public partial class TrafficLight
                {
                    public enum State { Red, Yellow, Green }

                    [Transition(State.Red, State.Green)]
                    public Task OnGo() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(
            new[] { StateMachineAttributeStubs, source });

        var smSource = GeneratorTestHelper.GetGeneratedSource(result, "StateMachines.g.cs");
        Assert.NotNull(smSource);
        Assert.Contains("CurrentState", smSource);
    }

    [Fact]
    public void GeneratedStateMachine_ContainsTransitionMethods()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [StateMachine]
                public partial class DocumentFlow
                {
                    public enum State { Draft, Review, Approved, Rejected }

                    [Transition(State.Draft, State.Review)]
                    public Task OnSubmit() => Task.CompletedTask;

                    [Transition(State.Review, State.Approved)]
                    public Task OnApprove() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(
            new[] { StateMachineAttributeStubs, source });

        var smSource = GeneratorTestHelper.GetGeneratedSource(result, "StateMachines.g.cs");
        Assert.NotNull(smSource);
        Assert.Contains("SubmitAsync", smSource);
        Assert.Contains("ApproveAsync", smSource);
    }

    [Fact]
    public void GeneratedStateMachine_ContainsStateChangeEvents()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [StateMachine]
                public partial class Workflow
                {
                    public enum State { Idle, Running, Completed }

                    [Transition(State.Idle, State.Running)]
                    public Task OnStart() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(
            new[] { StateMachineAttributeStubs, source });

        var smSource = GeneratorTestHelper.GetGeneratedSource(result, "StateMachines.g.cs");
        Assert.NotNull(smSource);
        Assert.Contains("StateChanging", smSource);
        Assert.Contains("StateChanged", smSource);
    }

    [Fact]
    public void GeneratedStateMachine_ContainsHistoryTracking_WhenEnabled()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [StateMachine(TrackHistory = true)]
                public partial class AuditedMachine
                {
                    public enum State { Open, Closed }

                    [Transition(State.Open, State.Closed)]
                    public Task OnClose() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(
            new[] { StateMachineAttributeStubs, source });

        var smSource = GeneratorTestHelper.GetGeneratedSource(result, "StateMachines.g.cs");
        Assert.NotNull(smSource);
        Assert.Contains("History", smSource);
    }

    [Fact]
    public void StateMachineWithNoTransitions_NoStateMachineCodeGenerated()
    {
        // A class with [StateMachine] but no [Transition] methods → no output (transitions.Count == 0 guard)
        const string source = """
            using StubAttrs;
            namespace TestApp
            {
                [StateMachine]
                public partial class NoTransitionMachine
                {
                    public enum State { A, B }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(
            new[] { StateMachineAttributeStubs, source });

        var hintNames = GeneratorTestHelper.GetGeneratedHintNames(result);
        Assert.DoesNotContain("StateMachines.g.cs", hintNames);
    }

    [Fact]
    public void NoGeneratorException_OnValidInput()
    {
        const string source = """
            using StubAttrs;
            using System.Threading.Tasks;
            namespace TestApp
            {
                [StateMachine]
                public partial class SimpleMachine
                {
                    public enum State { Idle, Active }

                    [Transition(State.Idle, State.Active)]
                    public Task OnActivate() => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<StateMachineGenerator>(
            new[] { StateMachineAttributeStubs, source });

        Assert.Null(result.Exception);
    }
}
