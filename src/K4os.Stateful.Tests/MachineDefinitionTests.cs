using K4os.Stateful.Internal;

namespace K4os.Stateful.Tests;

public class MachineDefinitionTests
{
    private class Ctx;
    private class State;
    private class ConcreteState : State;
    private class Event;
    private class ConcreteEvent : Event;

    private static readonly Func<Activation<Ctx, State, Event>, ValueTask<State>> DummyAction =
        _ => ValueTask.FromResult<State>(null!);

    private static EventHandler<Ctx, State, Event> Handler(
        Type state, Type evt, bool hasGuard = false, int order = 0) =>
        new(state, evt, hasGuard, order, guard: null, action: DummyAction);

    private static MachineDefinition<Ctx, State, Event> Definition(
        params EventHandler<Ctx, State, Event>[] handlers) =>
        new(handlers, []);

    [Fact]
    public void GetRankedHandlers_ReturnsCorrectRankedOrder()
    {
        var closer = Handler(typeof(ConcreteState), typeof(ConcreteEvent), order: 0);
        var farther = Handler(typeof(State), typeof(ConcreteEvent), order: 1);
        var definition = Definition(closer, farther);

        var result = definition.GetRankedHandlers(typeof(ConcreteState), typeof(ConcreteEvent));

        Assert.Equal(2, result.Length);
        Assert.Equal(closer, result[0]);
        Assert.Equal(farther, result[1]);
    }

    [Fact]
    public void GetRankedHandlers_ReturnsCachedArrayReferenceOnSubsequentCalls()
    {
        var handler = Handler(typeof(ConcreteState), typeof(ConcreteEvent));
        var definition = Definition(handler);

        var first = definition.GetRankedHandlers(typeof(ConcreteState), typeof(ConcreteEvent));
        var second = definition.GetRankedHandlers(typeof(ConcreteState), typeof(ConcreteEvent));

        Assert.Same(first, second);
    }

    [Fact]
    public void GetRankedHandlers_IsThreadSafe()
    {
        var handlers = Enumerable.Range(0, 10)
            .Select(i => Handler(typeof(ConcreteState), typeof(ConcreteEvent), order: i))
            .ToArray();
        var definition = Definition(handlers);

        var results = new EventHandler<Ctx, State, Event>[64][];
        Parallel.For(0, 64, i =>
            results[i] = definition.GetRankedHandlers(typeof(ConcreteState), typeof(ConcreteEvent)));

        Assert.All(results, r => Assert.Equal(10, r.Length));
    }
}
