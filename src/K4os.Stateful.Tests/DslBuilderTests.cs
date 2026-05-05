using K4os.Stateful.Internal;
using EventHandler = K4os.Stateful.Internal.EventHandler;

namespace K4os.Stateful.Tests;

public class DslBuilderTests
{
    private class Ctx;

    private interface IState;
    private record StateA(bool Flag = false) : IState;
    private record StateB : IState;

    private interface IEvent;
    private record EventX : IEvent;
    private record EventY : IEvent;

    private static StateMachine<Ctx, IState, IEvent>.IMachineConfig Define() =>
        StateMachine.Define<Ctx, IState, IEvent>();

    // ── Helper to inspect EventHandlers from a MachineDefinition ─────────────

    private static EventHandler<Ctx, IState, IEvent>[] AllEventHandlers(
        MachineDefinition<Ctx, IState, IEvent> def) =>
        def.GetRankedHandlers(typeof(StateA), typeof(EventX))
            .Concat(def.GetRankedHandlers(typeof(StateA), typeof(EventY)))
            .Concat(def.GetRankedHandlers(typeof(StateB), typeof(EventX)))
            .Distinct()
            .ToArray();

    // ── EventHandler registration ─────────────────────────────────────────────

    [Fact]
    public void GoTo_RegistersEventHandler_WithCorrectTypes()
    {
        var def = Define()
            .In<StateA>().On<EventX>().GoTo(x => ValueTask.FromResult<IState>(new StateB()))
            .Build();

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));

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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));

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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));

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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));
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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));
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

        var handlersX = def.GetRankedHandlers(typeof(StateA), typeof(EventX));
        var handlersY = def.GetRankedHandlers(typeof(StateA), typeof(EventY));

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

        var chainedA = chained.GetRankedHandlers(typeof(StateA), typeof(EventX));
        var disconnectedA = disconnectedDef.GetRankedHandlers(typeof(StateA), typeof(EventX));
        var chainedB = chained.GetRankedHandlers(typeof(StateB), typeof(EventX));
        var disconnectedB = disconnectedDef.GetRankedHandlers(typeof(StateB), typeof(EventX));

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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));
        var activation = new Activation<Ctx, IState, IEvent>(new Ctx(), new StateA(), new EventX(), CancellationToken.None);
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

        var handlers = def.GetSortedStateHandlers(typeof(StateA));

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

        var handlers = def.GetSortedStateHandlers(typeof(StateA));

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

        var handlers = def.GetSortedStateHandlers(typeof(StateA));

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

        var handlers = def.GetSortedStateHandlers(typeof(StateA));
        Assert.Single(handlers);

        await handlers[0].OnEnter!(new Activation<Ctx, IState>(new Ctx(), new StateA(), CancellationToken.None));

        Assert.Equal([1, 2], calls);
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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));
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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));
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

        var handlers = def.GetRankedHandlers(typeof(StateA), typeof(EventX));
        var activation = new Activation<Ctx, IState, IEvent>(
            new Ctx(), new StateA(), new EventX(), CancellationToken.None);

        Assert.False(await handlers[0].Guard!(activation));
    }

    // ── Build immutability ────────────────────────────────────────────────────

    [Fact]
    public void Build_ReturnsImmutableDefinition_CacheIsIndependent()
    {
        var config = Define().In<StateA>().On<EventX>().GoTo(x => ValueTask.FromResult<IState>(new StateB()));
        var def1 = config.Build();
        var def2 = config.Build();

        var h1 = def1.GetRankedHandlers(typeof(StateA), typeof(EventX));
        var h2 = def2.GetRankedHandlers(typeof(StateA), typeof(EventX));

        Assert.NotSame(def1, def2);
        Assert.NotSame(h1, h2);
    }
}
