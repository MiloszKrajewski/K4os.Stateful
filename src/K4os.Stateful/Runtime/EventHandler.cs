namespace K4os.Stateful.Runtime;

internal class EventHandler
{
    public Type StateType { get; }
    public Type EventType { get; }
    public bool HasGuard { get; }
    public int DeclarationOrder { get; }

    public EventHandler(Type stateType, Type eventType, bool hasGuard, int declarationOrder)
    {
        StateType = stateType;
        EventType = eventType;
        HasGuard = hasGuard;
        DeclarationOrder = declarationOrder;
    }
}

// Generic sealed subclass — carries the typed delegate payload for execution.
// MachineDefinition stores this; EventHandlerRanker operates on the base class.
internal sealed class EventHandler<TContext, TState, TEvent>: EventHandler
    where TState: class
    where TEvent: class
{
    public Func<Activation<TContext, TState, TEvent>, ValueTask<bool>>? Guard { get; }
    public Func<Activation<TContext, TState, TEvent>, ValueTask<TState>> Action { get; }

    public EventHandler(
        Type stateType, Type eventType, bool hasGuard, int declarationOrder,
        Func<Activation<TContext, TState, TEvent>, ValueTask<bool>>? guard,
        Func<Activation<TContext, TState, TEvent>, ValueTask<TState>> action):
        base(stateType, eventType, hasGuard, declarationOrder)
    {
        Guard = guard;
        Action = action;
    }
}

