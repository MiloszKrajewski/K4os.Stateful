using System.Collections.Concurrent;

namespace K4os.Stateful.Runtime;

// Frozen, immutable snapshot of a state machine configuration.
// Shared safely across concurrent Executor instances — fully read-only after construction.
//
// Both caches start empty and populate lazily.
public sealed class MachineDefinition<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    private readonly EventHandler<TContext, TState, TEvent>[] _eventHandlers;
    private readonly StateHandler<TContext, TState>[] _stateHandlers;
    private readonly Func<TState, TState, bool> _stateChangePredicate;

    private readonly ConcurrentDictionary<(Type State, Type Event), EventHandler<TContext, TState, TEvent>[]>
        _eventCache = new();

    // Sorted ascending by (distance, isInterface, declarationOrder).
    // Exit iterates forward (derived→base); Enter iterates in reverse (base→derived).
    private readonly ConcurrentDictionary<Type, StateHandler<TContext, TState>[]>
        _stateCache = new();

    internal MachineDefinition(
        IEnumerable<EventHandler<TContext, TState, TEvent>> eventHandlers,
        IEnumerable<StateHandler<TContext, TState>> stateHandlers,
        Func<TState, TState, bool> stateChangePredicate)
    {
        _eventHandlers = eventHandlers.ToArray();
        _stateHandlers = stateHandlers.ToArray();
        _stateChangePredicate = stateChangePredicate;
    }

    internal bool StateChanged(TState previous, TState next) => _stateChangePredicate(previous, next);

    public MachineExecutor<TContext, TState, TEvent> Create(TContext context, TState state) =>
        new(this, context, state);

    internal EventHandler<TContext, TState, TEvent>[] GetEventHandlers(
        Type actualStateType, Type actualEventType) =>
        _eventCache.GetOrAdd((actualStateType, actualEventType), GetSortedEventHandlers);

    internal StateHandler<TContext, TState>[] GetStateHandlers(Type actualStateType) =>
        _stateCache.GetOrAdd(actualStateType, GetSortedStateHandlers);

    private EventHandler<TContext, TState, TEvent>[] GetSortedEventHandlers((Type State, Type Event) key) =>
        EventHandlerRanker.RankedCandidates(_eventHandlers, key.State, key.Event);

    private StateHandler<TContext, TState>[] GetSortedStateHandlers(Type stateType) => (
        from h in _stateHandlers
        let d = stateType.DistanceFrom(h.StateType)
        where d is not null
        orderby d.Value, h.StateType.IsInterface ? 1 : 0, h.DeclarationOrder
        select h
    ).ToArray();
}
