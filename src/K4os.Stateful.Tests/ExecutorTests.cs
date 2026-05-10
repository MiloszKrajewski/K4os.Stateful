#pragma warning disable xUnit1051

using K4os.Stateful.Runtime;

namespace K4os.Stateful.Tests;

public class ExecutorTests
{
    // ── Domain types ────────────────────────────────────────────────────────────

    private class Ctx;

    private interface IState;
    private class StateA : IState;
    private class StateB : IState;
    private class StateC : IState;
    private class StateDerived : StateA;

    private interface IEvent;
    private class EventA : IEvent;
    private class EventB : IEvent;

    // ── Helpers ──────────────────────────────────────────────────────────────────

    // Resolves ambiguity between sync/Task/ValueTask GoTo overloads for throw expressions.
    private static IState Throw(Exception ex) => throw ex;

    private static Configuration.StateMachineConfig<Ctx, IState, IEvent>.IMachineConfig Config() =>
        StateMachine.Configure<Ctx, IState, IEvent>();

    private static MachineExecutor<Ctx, IState, IEvent> Build(
        Action<Configuration.StateMachineConfig<Ctx, IState, IEvent>.IMachineConfig> configure,
        IState? initialState = null)
    {
        var config = Config();
        configure(config);
        var executor = config.Build().Create(new Ctx(), initialState ?? new StateA());
        return executor;
    }

    // ── 5.x Executor Lifecycle ─────────────────────────────────────────────────

    [Fact]
    public void Start_SetsState_WithoutFiringOnEnter()
    {
        var entered = false;
        var executor = Config()
            .In<StateA>().OnEnter(_ => { entered = true; })
            .Build().Create(new Ctx(), new StateA());

        Assert.IsType<StateA>(executor.State);
        Assert.False(entered);
    }

    [Fact]
    public void State_ReturnsCorrectValueAfterStart()
    {
        var state = new StateA();
        var config = Config().Build().Create(new Ctx(), state);
        Assert.Same(state, config.State);
    }

    [Fact]
    public async Task State_ReturnsCorrectValueAfterFireAsync()
    {
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().GoTo(_ => new StateB()));

        await executor.FireAsync(new EventA());

        Assert.IsType<StateB>(executor.State);
    }

    [Fact]
    public async Task TwoExecutors_FromSameDefinition_AreIndependent()
    {
        var def = Config()
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .Build();

        var ex1 = def.Create(new Ctx(), new StateA());
        var ex2 = def.Create(new Ctx(), new StateA());

        await ex1.FireAsync(new EventA());

        Assert.IsType<StateB>(ex1.State);
        Assert.IsType<StateA>(ex2.State);
    }

    // ── 6.x Entry / Exit Firing Order ─────────────────────────────────────────

    [Fact]
    public async Task OnEnter_FiresBaseBeforeDerived()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateA>().OnEnter(_ => log.Add("StateA"))
            .In<StateDerived>().OnEnter(_ => log.Add("StateDerived"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateDerived()),
            initialState: new StateA());

        await executor.FireAsync(new EventA());

        Assert.Equal(["StateA", "StateDerived"], log);
    }

    [Fact]
    public async Task OnExit_FiresDerivedBeforeBase()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateA>().OnExit(_ => log.Add("StateA"))
            .In<StateDerived>().OnExit(_ => log.Add("StateDerived"))
            .In<StateDerived>().On<EventA>().GoTo(_ => new StateB()),
            initialState: new StateDerived());

        await executor.FireAsync(new EventA());

        Assert.Equal(["StateDerived", "StateA"], log);
    }

    [Fact]
    public async Task OnExit_CompletesBeforeOnEnterStarts()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateA>().OnExit(_ => log.Add("exit-A"))
            .In<StateB>().OnEnter(_ => log.Add("enter-B"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateB()));

        await executor.FireAsync(new EventA());

        Assert.Equal(["exit-A", "enter-B"], log);
    }

    [Fact]
    public async Task OnEnter_HandlerRegisteredOnInterface_FiresAtCorrectRank()
    {
        var log = new List<string>();

        // StateDerived : StateA : IState
        // Entering StateDerived → IState handler (rank 2) → StateA handler (rank 1) → StateDerived handler (rank 0)
        var executor = Build(c => c
            .In<IState>().OnEnter(_ => log.Add("IState"))
            .In<StateA>().OnEnter(_ => log.Add("StateA"))
            .In<StateDerived>().OnEnter(_ => log.Add("StateDerived"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateDerived()));

        await executor.FireAsync(new EventA());

        Assert.Equal(["IState", "StateA", "StateDerived"], log);
    }

    [Fact]
    public async Task OnEnter_LevelsWithNoHandler_AreSilentlySkipped()
    {
        var log = new List<string>();

        // Only IState and StateDerived registered — StateA has no OnEnter
        var executor = Build(c => c
            .In<IState>().OnEnter(_ => log.Add("IState"))
            .In<StateDerived>().OnEnter(_ => log.Add("StateDerived"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateDerived()));

        await executor.FireAsync(new EventA());

        Assert.Equal(["IState", "StateDerived"], log);
    }

    [Fact]
    public async Task OnEnter_MultipleRegistrationsForSameType_FireInDeclarationOrder()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateB>().OnEnter(_ => log.Add("first"))
            .In<StateB>().OnEnter(_ => log.Add("second"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateB()));

        await executor.FireAsync(new EventA());

        Assert.Equal(["first", "second"], log);
    }

    [Fact]
    public async Task OnEnter_SplitAcrossTwoInBlocks_FiresInDeclarationOrder()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateB>().OnEnter(_ => log.Add("first"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>().OnEnter(_ => log.Add("second")));

        await executor.FireAsync(new EventA());

        Assert.Equal(["first", "second"], log);
    }

    [Fact]
    public async Task OnExit_SplitAcrossTwoInBlocks_FiresInDeclarationOrder()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateA>().OnExit(_ => log.Add("first"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateA>().OnExit(_ => log.Add("second")));

        await executor.FireAsync(new EventA());

        Assert.Equal(["first", "second"], log);
    }

    // ── 7.x State-Change Predicate ─────────────────────────────────────────────

    [Fact]
    public async Task DefaultPredicate_Stay_DoesNotFireEntryExit()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateA>().OnEnter(_ => log.Add("enter")).OnExit(_ => log.Add("exit"))
            .In<StateA>().On<EventA>().Stay());

        await executor.FireAsync(new EventA());

        Assert.Empty(log);
    }

    [Fact]
    public async Task DefaultPredicate_GoToNewInstance_FiresEntryExit()
    {
        var log = new List<string>();

        var executor = Build(c => c
            .In<StateA>().OnExit(_ => log.Add("exit")).OnEnter(_ => log.Add("enter"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateA()));

        await executor.FireAsync(new EventA());

        Assert.Equal(["exit", "enter"], log);
    }

    [Fact]
    public async Task DefaultPredicate_GoToSameTypeNewInstance_FiresEntryExit()
    {
        var log = new List<string>();
        var executor = Build(c => c
            .In<StateA>().OnExit(_ => log.Add("exit"))
            .In<StateA>().On<EventA>().GoTo(_ => new StateA()));

        await executor.FireAsync(new EventA());

        Assert.Contains("exit", log);
    }

    [Fact]
    public async Task CustomAlwaysTruePredicate_FiresEntryExitEvenForStayEquivalent()
    {
        var log = new List<string>();
        var state = new StateA();

        var executor = Config()
            .WithStateChangeIf((_, _) => true)
            .In<StateA>().OnExit(_ => log.Add("exit")).On<EventA>().GoTo(_ => state)
            .Build().Create(new Ctx(), state);

        await executor.FireAsync(new EventA());

        Assert.Contains("exit", log);
    }

    [Fact]
    public async Task CustomAlwaysFalsePredicate_NeverFiresEntryExit()
    {
        var log = new List<string>();

        var executor = Config()
            .WithStateChangeIf((_, _) => false)
            .In<StateA>().OnExit(_ => log.Add("exit")).OnEnter(_ => log.Add("enter")).On<EventA>().GoTo(_ => new StateB())
            .Build().Create(new Ctx(), new StateA());

        await executor.FireAsync(new EventA());

        Assert.Empty(log);
    }

    [Fact]
    public async Task CustomTypeChangePredicate_FiresOnlyOnTypeChange()
    {
        var log = new List<string>();

        var executor = Config()
            .WithStateChangeIf((s1, s2) => s1.GetType() != s2.GetType())
            .In<StateA>()
                .OnExit(_ => log.Add("exit"))
                .On<EventA>().GoTo(_ => new StateA()) // same type — no fire
                .On<EventB>().GoTo(_ => new StateB()) // different type — fires
            .Build().Create(new Ctx(), new StateA());

        await executor.FireAsync(new EventA());
        Assert.Empty(log);

        await executor.FireAsync(new EventB());
        Assert.Contains("exit", log);
    }

    [Fact]
    public void Start_DoesNotFireOnEnter_RegardlessOfPredicate()
    {
        var entered = false;
        var config = Config().WithStateChangeIf((_, _) => true);
        config.In<StateA>().OnEnter(_ => { entered = true; });
        _ = config.Build().Create(new Ctx(), new StateA());

        Assert.False(entered);
    }

    // ── 8.x Transitions / Guard Walk ──────────────────────────────────────────

    [Fact]
    public async Task GuardedHandler_ShortCircuits_SecondHandlerDoesNotFire()
    {
        var secondFired = false;

        var config = Config();
        config.In<StateA>().On<EventA>().When(_ => true).GoTo(_ => new StateB());
        config.In<StateA>().On<EventA>().GoTo(_ => { secondFired = true; return new StateB(); });
        var executor = config.Build().Create(new Ctx(), new StateA());

        await executor.FireAsync(new EventA());

        Assert.False(secondFired);
    }

    [Fact]
    public async Task GuardFalse_FallsThroughToNextCandidate()
    {
        var executor = Build(c => c
            .In<StateA>().On<EventA>().When(_ => false).GoTo(_ => new StateA())
            .In<StateA>().On<EventA>().GoTo(_ => new StateB()));

        await executor.FireAsync(new EventA());

        Assert.IsType<StateB>(executor.State);
    }

    [Fact]
    public async Task UnguardedHandler_ActsAsFallback_WhenGuardedHandlerFails()
    {
        var executor = Build(c => c
            .In<StateA>().On<EventA>().When(_ => false).GoTo(_ => new StateA())
            .In<StateA>().On<EventA>().Stay());

        await executor.FireAsync(new EventA());

        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task SameTypeTransition_UpdatesState()
    {
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().GoTo(_ => new StateA()));

        await executor.FireAsync(new EventA());

        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task CrossTypeTransition_UpdatesState()
    {
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().GoTo(_ => new StateB()));

        await executor.FireAsync(new EventA());

        Assert.IsType<StateB>(executor.State);
    }

    [Fact]
    public async Task Stay_NoCallback_StateRefUnchanged()
    {
        var original = new StateA();
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().Stay(), initialState: original);

        await executor.FireAsync(new EventA());

        Assert.Same(original, executor.State);
    }

    [Fact]
    public async Task Stay_WithCallback_CallbackRunsAndStateRefUnchanged()
    {
        var original = new StateA();
        var called = false;

        var executor = Build(c =>
            c.In<StateA>().On<EventA>().Stay(_ => { called = true; }), initialState: original);

        await executor.FireAsync(new EventA());

        Assert.True(called);
        Assert.Same(original, executor.State);
    }

    [Fact]
    public async Task FireAsync_AwaitsSideEffects_BeforeReturning()
    {
        var completed = false;

        async ValueTask<IState> DelayThenComplete()
        {
            await Task.Delay(10);
            completed = true;
            return new StateB();
        }

        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => DelayThenComplete()));

        await executor.FireAsync(new EventA());

        Assert.True(completed);
    }

    [Fact]
    public async Task TryFireAsync_ReturnsTrue_OnMatch()
    {
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().GoTo(_ => new StateB()));

        var result = await executor.TryFireAsync(new EventA());

        Assert.True(result);
    }

    // ── 9.x Unhandled Events ──────────────────────────────────────────────────

    [Fact]
    public async Task FireAsync_Throws_WhenNoHandlerRegistered()
    {
        var executor = Build(_ => { });

        await Assert.ThrowsAsync<UnhandledEventException>(() =>
            executor.FireAsync(new EventA()).AsTask());
    }

    [Fact]
    public async Task FireAsync_Throws_WhenAllGuardsFail()
    {
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().When(_ => false).GoTo(_ => new StateB()));

        await Assert.ThrowsAsync<UnhandledEventException>(() =>
            executor.FireAsync(new EventA()).AsTask());
    }

    [Fact]
    public async Task TryFireAsync_ReturnsFalse_WhenNoHandlerRegistered()
    {
        var executor = Build(_ => { });

        var result = await executor.TryFireAsync(new EventA());

        Assert.False(result);
    }

    [Fact]
    public async Task TryFireAsync_ReturnsFalse_WhenAllGuardsFail()
    {
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().When(_ => false).GoTo(_ => new StateB()));

        var result = await executor.TryFireAsync(new EventA());

        Assert.False(result);
    }

    [Fact]
    public async Task CatchAllRule_SuppressesUnhandledEventException()
    {
        var executor = Build(c => c
            .In<IState>().On<IEvent>().Stay());

        await executor.FireAsync(new EventA());
    }

    [Fact]
    public async Task FireAsync_StateUnchanged_WhenNoHandlerMatches()
    {
        var original = new StateA();
        var executor = Build(_ => { }, initialState: original);

        await Assert.ThrowsAsync<UnhandledEventException>(() =>
            executor.FireAsync(new EventA()).AsTask());

        Assert.Same(original, executor.State);
    }

    [Fact]
    public async Task FireAsync_StateUnchanged_WhenActionThrows()
    {
        var original = new StateA();
        var executor = Build(c =>
            c.In<StateA>().On<EventA>().GoTo(_ => Throw(new InvalidOperationException("boom"))),
            initialState: original);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.FireAsync(new EventA()).AsTask());

        Assert.Same(original, executor.State);
    }

    // ── 10.x Concurrent Fire Detection ────────────────────────────────────────

    [Fact]
    public async Task ConcurrentFireAsync_Throws_ConcurrentFireException()
    {
        var barrier = new TaskCompletionSource();

        async ValueTask<IState> WaitForBarrier()
        {
            await barrier.Task;
            return new StateB();
        }

        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => WaitForBarrier()));

        var first = executor.FireAsync(new EventA()).AsTask();

        await Assert.ThrowsAsync<ConcurrentFireException>(() =>
            executor.FireAsync(new EventA()).AsTask());

        barrier.SetResult();
        await first;
    }

    [Fact]
    public async Task SequentialFireAsync_CompletesWithoutException()
    {
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>().On<EventA>().GoTo(_ => new StateA()));

        await executor.FireAsync(new EventA());
        await executor.FireAsync(new EventA());

        Assert.IsType<StateA>(executor.State);
    }

    // ── WithStateChangeIf DSL ─────────────────────────────────────────────────

    [Fact]
    public async Task WithStateChangeIf_StoresPredicateInDefinition()
    {
        var entered = false;
        var sameState = new StateA();

        // Always-true predicate — even returning the same ref triggers entry/exit
        var executor = Config()
            .WithStateChangeIf((_, _) => true)
            .In<StateA>().OnEnter(_ => { entered = true; }).On<EventA>().GoTo(_ => sameState)
            .Build().Create(new Ctx(), sameState);

        await executor.FireAsync(new EventA());

        Assert.True(entered);
    }

    [Fact]
    public async Task WithStateChangeIf_DefaultPredicateIsReferenceInequalityWhenNotCalled()
    {
        var entered = false;
        var sameState = new StateA();

        var executor = Build(c => c
            .In<StateA>().OnEnter(_ => { entered = true; })
            .In<StateA>().On<EventA>().GoTo(_ => sameState),
            initialState: sameState);

        await executor.FireAsync(new EventA());

        Assert.False(entered); // same ref → no entry/exit under default predicate
    }

    [Fact]
    public void WithStateChangeIf_ReturnsIMachineConfig_AllowingChaining()
    {
        // Compilation test — if this compiles and builds, chaining works
        var def = Config()
            .WithStateChangeIf((_, _) => true)
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .Build();

        Assert.NotNull(def);
    }

    // ── 14.x Auto Transitions ─────────────────────────────────────────────────

    [Fact]
    public void Create_DoesNotTriggerAuto_PositionsAtInitialState()
    {
        var autoCalled = false;
        var def = Config()
            .In<StateA>().Auto(_ => { autoCalled = true; return ValueTask.FromResult<IState>(new StateB()); })
            .Build();

        var executor = def.Create(new Ctx(), new StateA());

        Assert.False(autoCalled);
        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task IdleAsync_IsNoOp_WhenCurrentStateHasNoAuto()
    {
        var log = new List<string>();
        var executor = Build(c => c
            .In<StateA>().OnEnter(_ => log.Add("enter-A"))
            .In<StateA>().On<EventA>().Stay());

        await executor.FireAsync(new EventA());
        log.Clear();

        await executor.IdleAsync();

        Assert.Empty(log);
        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task Auto_FiresAfterOnEnter_BeforeFireAsyncReturns()
    {
        var log = new List<string>();
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>()
                .OnEnter(_ => { log.Add("OnEnter-B"); })
                .Auto(_ => { log.Add("Auto-B"); return ValueTask.FromResult<IState>(new StateA()); }));

        await executor.FireAsync(new EventA());

        Assert.Equal(2, log.Count);
        Assert.Equal("OnEnter-B", log[0]);
        Assert.Equal("Auto-B", log[1]);
        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task Auto_ReturningSameReference_IsNoOp()
    {
        var autoCalled = 0;
        var enterCount = 0;
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>()
                .OnEnter(_ => { enterCount++; })
                .Auto(x => { autoCalled++; return ValueTask.FromResult<IState>(x.State); }));

        await executor.FireAsync(new EventA());

        Assert.Equal(1, autoCalled);
        Assert.Equal(1, enterCount);
        Assert.IsType<StateB>(executor.State);
    }

    [Fact]
    public async Task Auto_TriggeringTransition_FiresFullLifecycle()
    {
        var log = new List<string>();
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>()
                .OnEnter(_ => { log.Add("OnEnter-B"); })
                .OnExit(_ => { log.Add("OnExit-B"); })
                .Auto(_ => ValueTask.FromResult<IState>(new StateA()))
            .In<StateA>()
                .OnEnter(_ => { log.Add("OnEnter-A"); }));

        await executor.FireAsync(new EventA());

        Assert.Equal(["OnEnter-B", "OnExit-B", "OnEnter-A"], log);
        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task Auto_Chain_AllIntermediateLifecycleFires()
    {
        var log = new List<string>();
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>()
                .OnEnter(_ => { log.Add("OnEnter-B"); })
                .Auto(_ => ValueTask.FromResult<IState>(new StateC()))
            .In<StateC>()
                .OnEnter(_ => { log.Add("OnEnter-C"); })
                .Auto(_ => ValueTask.FromResult<IState>(new StateA()))
            .In<StateA>()
                .OnEnter(_ => { log.Add("OnEnter-A"); }));

        await executor.FireAsync(new EventA());

        Assert.Equal(["OnEnter-B", "OnEnter-C", "OnEnter-A"], log);
        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task Auto_FinalState_WithNoAuto_TerminatesCleanly()
    {
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>().Auto(_ => ValueTask.FromResult<IState>(new StateC())));

        await executor.FireAsync(new EventA());

        Assert.IsType<StateC>(executor.State);
    }

    [Fact]
    public async Task Auto_Exception_LeavesStateAtLastEnteredState()
    {
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>().Auto(_ => Throw(new InvalidOperationException("boom"))));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.FireAsync(new EventA()).AsTask());

        Assert.IsType<StateB>(executor.State);
    }

    [Fact]
    public async Task Auto_IdleAsync_ResumesAfterStuckAutoState()
    {
        var attempt = 0;
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>().Auto(x => {
                if (++attempt == 1) throw new InvalidOperationException("transient");
                return ValueTask.FromResult<IState>(new StateC());
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.FireAsync(new EventA()).AsTask());
        Assert.IsType<StateB>(executor.State);

        await executor.IdleAsync();

        Assert.IsType<StateC>(executor.State);
    }

    [Fact]
    public async Task Auto_FireAsync_SettlesStuckAutoStateBeforeDispatching()
    {
        var attempt = 0;
        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>().Auto(x => {
                if (++attempt == 1) throw new InvalidOperationException("transient");
                return ValueTask.FromResult<IState>(new StateC());
            })
            .In<StateC>().On<EventB>().GoTo(_ => new StateA()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.FireAsync(new EventA()).AsTask());
        Assert.IsType<StateB>(executor.State);

        await executor.FireAsync(new EventB());

        Assert.IsType<StateA>(executor.State);
    }

    [Fact]
    public async Task Auto_CancellationToken_FlowsThrough()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;

        var executor = Build(c => c
            .In<StateA>().On<EventA>().GoTo(_ => new StateB())
            .In<StateB>().Auto(x =>
            {
                captured = x.CancellationToken;
                return ValueTask.FromResult<IState>(x.State);
            }));

        await executor.TryFireAsync(new EventA(), cts.Token);

        Assert.Equal(cts.Token, captured);
    }
}
