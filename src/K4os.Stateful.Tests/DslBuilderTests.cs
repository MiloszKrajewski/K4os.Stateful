#pragma warning disable xUnit1051

using K4os.Stateful.Runtime;
using EventHandler = K4os.Stateful.Runtime.EventHandler;

namespace K4os.Stateful.Tests;

public class DslBuilderTests
{
    private class Ctx;

    private interface IState;

    private record StateA(bool Flag = false): IState;

    private record StateB: IState;

    private interface IEvent;

    private record EventX: IEvent;

    private record EventY: IEvent;

    private static Configuration.StateMachineConfig<Ctx, IState, IEvent>.IMachineConfig Define() =>
        StateMachine.Configure<Ctx, IState, IEvent>();

    // ── Helper to inspect EventHandlers from a MachineDefinition ─────────────

    private static EventHandler<Ctx, IState, IEvent>[] AllEventHandlers(
        MachineDefinition<Ctx, IState, IEvent> def) =>
        def.GetEventHandlers(typeof(StateA), typeof(EventX))
            .Concat(def.GetEventHandlers(typeof(StateA), typeof(EventY)))
            .Concat(def.GetEventHandlers(typeof(StateB), typeof(EventX)))
            .Distinct()
            .ToArray();

    // ── EventHandler registration ─────────────────────────────────────────────

    [Fact]
    public void GoTo_RegistersEventHandler_WithCorrectTypes()
    {
        var def = Define()
            .In<StateA>().On<EventX>().GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));

        Assert.Single(handlers);
        Assert.Equal(typeof(StateA), handlers[0].StateType);
        Assert.Equal(typeof(EventX), handlers[0].EventType);
        Assert.False(handlers[0].HasGuard);
        Assert.NotNull(handlers[0].Action);
    }

    [Fact]
    public void When_SetsHasGuard_True()
    {
        var def = Define()
            .In<StateA>().On<EventX>()
            .When(x => x.State.Flag)
            .GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));

        Assert.Single(handlers);
        Assert.True(handlers[0].HasGuard);
        Assert.NotNull(handlers[0].Guard);
    }

    [Fact]
    public void Stay_RegistersEventHandler_WithIdentityAction()
    {
        var def = Define()
            .In<StateA>().On<EventX>().Stay()
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));

        Assert.Single(handlers);
        Assert.False(handlers[0].HasGuard);
        Assert.NotNull(handlers[0].Action);
    }

    [Fact]
    public async Task Stay_WithCallback_RunsCallbackAndReturnsState()
    {
        var ran = false;
        var def = Define()
            .In<StateA>().On<EventX>().Stay(x => { ran = true; })
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));
        var state = new StateA();
        var ctx = new Ctx();
        var activation = new Activation<Ctx, IState, IEvent>(ctx, state, new EventX(), CancellationToken.None);
        var result = await handlers[0].Action(activation);

        Assert.True(ran);
        Assert.Same(state, result);
    }

    [Fact]
    public async Task Stay_NoCallback_ReturnsSameReference()
    {
        var def = Define()
            .In<StateA>().On<EventX>().Stay()
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));
        var state = new StateA();
        var activation = new Activation<Ctx, IState, IEvent>(new Ctx(), state, new EventX(), CancellationToken.None);
        var result = await handlers[0].Action(activation);

        Assert.Same(state, result);
    }

    [Fact]
    public void MultipleHandlers_ChainedStyle_RegisteredInDeclarationOrder()
    {
        var def = Define()
            .In<StateA>()
            .On<EventX>().GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .On<EventY>().Stay()
            .Build();

        var handlersX = def.GetEventHandlers(typeof(StateA), typeof(EventX));
        var handlersY = def.GetEventHandlers(typeof(StateA), typeof(EventY));

        Assert.Single(handlersX);
        Assert.Single(handlersY);
        Assert.True(handlersX[0].DeclarationOrder < handlersY[0].DeclarationOrder);
    }

    [Fact]
    public void MultipleHandlers_DisconnectedStyle_SameAsChained()
    {
        var chained = Define()
            .In<StateA>().On<EventX>().GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .In<StateB>().On<EventX>().Stay()
            .Build();

        var disconnected = Define();
        disconnected.In<StateA>().On<EventX>().GoTo(x => ValueTask.FromResult<IState>(new StateB()));
        disconnected.In<StateB>().On<EventX>().Stay();
        var disconnectedDef = disconnected.Build();

        var chainedA = chained.GetEventHandlers(typeof(StateA), typeof(EventX));
        var disconnectedA = disconnectedDef.GetEventHandlers(typeof(StateA), typeof(EventX));
        var chainedB = chained.GetEventHandlers(typeof(StateB), typeof(EventX));
        var disconnectedB = disconnectedDef.GetEventHandlers(typeof(StateB), typeof(EventX));

        Assert.Single(chainedA);
        Assert.Single(disconnectedA);
        Assert.Equal(chainedA[0].StateType, disconnectedA[0].StateType);
        Assert.Equal(chainedA[0].EventType, disconnectedA[0].EventType);
        Assert.Single(chainedB);
        Assert.Single(disconnectedB);
    }

    [Fact]
    public async Task GoTo_Action_ReturnsExpectedNewState()
    {
        var expected = new StateB();
        var def = Define()
            .In<StateA>().On<EventX>().GoTo(_ => (IState)expected)
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));
        var activation = new Activation<Ctx, IState, IEvent>(
            new Ctx(), new StateA(), new EventX(), CancellationToken.None);
        var result = await handlers[0].Action(activation);

        Assert.Same(expected, result);
    }

    // ── StateHandler registration ─────────────────────────────────────────────

    [Fact]
    public void OnEnter_RegistersStateHandler_WithEnterCallback()
    {
        var def = Define()
            .In<StateA>().OnEnter(x => { })
            .Build();

        var handlers = def.GetStateHandlers(typeof(StateA));

        Assert.Single(handlers);
        Assert.Equal(typeof(StateA), handlers[0].StateType);
        Assert.NotNull(handlers[0].OnEnter);
        Assert.Null(handlers[0].OnExit);
    }

    [Fact]
    public void OnExit_RegistersStateHandler_WithExitCallback()
    {
        var def = Define()
            .In<StateA>().OnExit(x => { })
            .Build();

        var handlers = def.GetStateHandlers(typeof(StateA));

        Assert.Single(handlers);
        Assert.Equal(typeof(StateA), handlers[0].StateType);
        Assert.Null(handlers[0].OnEnter);
        Assert.NotNull(handlers[0].OnExit);
    }

    [Fact]
    public void OnEnterAndOnExit_SameInContext_RegisteredAsSingleStateHandler()
    {
        var def = Define()
            .In<StateA>()
            .OnEnter(x => { })
            .OnExit(x => { })
            .Build();

        var handlers = def.GetStateHandlers(typeof(StateA));

        Assert.Single(handlers);
        Assert.NotNull(handlers[0].OnEnter);
        Assert.NotNull(handlers[0].OnExit);
    }

    [Fact]
    public async Task MultipleOnEnter_SameContext_AllCallbacksRun()
    {
        var calls = new List<int>();
        var def = Define()
            .In<StateA>()
            .OnEnter(x => { calls.Add(1); })
            .OnEnter(x => { calls.Add(2); })
            .Build();

        var handlers = def.GetStateHandlers(typeof(StateA));
        Assert.Single(handlers);

        await handlers[0].OnEnter!(new Activation<Ctx, IState>(new Ctx(), new StateA(), CancellationToken.None));

        Assert.Equal([1, 2], calls);
    }

    // ── Disconnected-style registration ──────────────────────────────────────

    [Fact]
    public async Task DisconnectedStyle_OnEnter_FiresOnTransitionIntoState()
    {
        var log = new List<string>();
        var config = Define();
        config.In<StateB>().OnEnter(_ => log.Add("entered-B"));
        config.In<StateA>().On<EventX>().GoTo(_ => (IState)new StateB());
        var executor = config.Build().Create(new Ctx(), new StateA());

        await executor.FireAsync(new EventX());

        Assert.Equal(["entered-B"], log);
    }

    // ── Incomplete handler detection ──────────────────────────────────────────

    [Fact]
    public void Build_Throws_WhenOnEventHasNoTerminal()
    {
        var config = Define();
        config.In<StateA>().On<EventX>();

        var ex = Assert.Throws<IncompleteEventHandlerException>(() => config.Build());
        Assert.Equal(typeof(StateA), ex.StateType);
        Assert.Equal(typeof(EventX), ex.EventType);
    }

    [Fact]
    public void Build_Throws_WhenOnEventWithGuardHasNoTerminal()
    {
        var config = Define();
        config.In<StateA>().On<EventX>().When(_ => true);

        Assert.Throws<IncompleteEventHandlerException>(() => config.Build());
    }

    [Fact]
    public void Build_Succeeds_AfterCompletingIncompleteHandler()
    {
        var config = Define();
        var h = config.In<StateA>().On<EventX>();
        Assert.Throws<IncompleteEventHandlerException>(() => config.Build());

        h.GoTo(_ => (IState)new StateB());
        var def = config.Build();

        Assert.NotNull(def);
        Assert.Single(def.GetEventHandlers(typeof(StateA), typeof(EventX)));
    }

    // ── Non-destructive Build ─────────────────────────────────────────────────

    [Fact]
    public void Build_IsNonDestructive_SecondBuildReflectsAddedHandlers()
    {
        var config = Define().In<StateA>().On<EventX>().GoTo(_ => (IState)new StateB());
        var def1 = config.Build();

        config.In<StateA>().On<EventY>().Stay();
        var def2 = config.Build();

        Assert.Empty(def1.GetEventHandlers(typeof(StateA), typeof(EventY)));
        Assert.Single(def2.GetEventHandlers(typeof(StateA), typeof(EventY)));
    }

    // ── AND guard semantics ───────────────────────────────────────────────────

    [Fact]
    public async Task MultipleWhen_AllPass_HandlerFires()
    {
        var def = Define()
            .In<StateA>().On<EventX>()
            .When(x => true)
            .When(x => true)
            .GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));
        var activation = new Activation<Ctx, IState, IEvent>(
            new Ctx(), new StateA(), new EventX(), CancellationToken.None);

        Assert.True(handlers[0].HasGuard);
        Assert.True(await handlers[0].Guard!(activation));
    }

    [Fact]
    public async Task MultipleWhen_FirstFails_GuardReturnsFalse()
    {
        var def = Define()
            .In<StateA>().On<EventX>()
            .When(x => false)
            .When(x => true)
            .GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));
        var activation = new Activation<Ctx, IState, IEvent>(
            new Ctx(), new StateA(), new EventX(), CancellationToken.None);

        Assert.False(await handlers[0].Guard!(activation));
    }

    [Fact]
    public async Task MultipleWhen_SecondFails_GuardReturnsFalse()
    {
        var def = Define()
            .In<StateA>().On<EventX>()
            .When(x => true)
            .When(x => false)
            .GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .Build();

        var handlers = def.GetEventHandlers(typeof(StateA), typeof(EventX));
        var activation = new Activation<Ctx, IState, IEvent>(
            new Ctx(), new StateA(), new EventX(), CancellationToken.None);

        Assert.False(await handlers[0].Guard!(activation));
    }

    // ── Auto handler registration ─────────────────────────────────────────────

    [Fact]
    public void Auto_RegistersStateHandler_WithAutoDelegate()
    {
        var def = Define()
            .In<StateA>().Auto(_ => ValueTask.FromResult<IState>(new StateB()))
            .Build();

        var handlers = def.GetStateHandlers(typeof(StateA));

        Assert.Single(handlers);
        Assert.NotNull(handlers[0].Auto);
    }

    [Fact]
    public async Task Auto_LastCallWins_WhenCalledTwiceOnSameState()
    {
        // .Auto() returns IMachineConfig, so two calls require disconnected style
        var config = Define();
        config.In<StateA>().Auto(_ => ValueTask.FromResult<IState>(new StateB()));   // first — should be overwritten
        config.In<StateA>().Auto(_ => ValueTask.FromResult<IState>(new StateA()));   // last — should win
        var def = config.Build();

        var handlers = def.GetStateHandlers(typeof(StateA));
        Assert.Single(handlers);

        var activation = new Activation<Ctx, IState>(new Ctx(), new StateA(), CancellationToken.None);
        var result = await handlers[0].Auto!(activation);

        Assert.IsType<StateA>(result);  // Second Auto (→StateA) won, not first (→StateB)
    }

    // ── Build immutability ────────────────────────────────────────────────────

    [Fact]
    public void Build_ReturnsImmutableDefinition_CacheIsIndependent()
    {
        var config = Define().In<StateA>().On<EventX>().GoTo(x => ValueTask.FromResult<IState>(new StateB()));
        var def1 = config.Build();
        var def2 = config.Build();

        var h1 = def1.GetEventHandlers(typeof(StateA), typeof(EventX));
        var h2 = def2.GetEventHandlers(typeof(StateA), typeof(EventX));

        Assert.NotSame(def1, def2);
        Assert.NotSame(h1, h2);
    }
}
