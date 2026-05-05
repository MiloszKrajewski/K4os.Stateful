using System.Collections.Concurrent;
using K4os.Stateful.Internal;

namespace K4os.Stateful;

// Frozen, immutable snapshot of a state machine configuration.
// Shared safely across concurrent Executor instances — fully read-only after construction.
//
// Both caches start empty and grow lazily: the first executor to encounter a given
// actual-type pair pays the ranking cost; all subsequent ones get O(1) lookup.
public sealed class MachineDefinition<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    private readonly EventHandler<TContext, TState, TEvent>[] _eventHandlers;
    private readonly StateHandler<TContext, TState>[] _stateHandlers;

    private readonly ConcurrentDictionary<(Type State, Type Event), EventHandler<TContext, TState, TEvent>[]>
        _eventCache = new();

    private readonly ConcurrentDictionary<Type, StateHandler<TContext, TState>[]>
        _stateCache = new();

    internal MachineDefinition(
        IReadOnlyList<EventHandler<TContext, TState, TEvent>> eventHandlers,
        IReadOnlyList<StateHandler<TContext, TState>> stateHandlers)
    {
        _eventHandlers = eventHandlers.ToArray();
        _stateHandlers = stateHandlers.ToArray();
    }

    internal EventHandler<TContext, TState, TEvent>[] GetRankedHandlers(
        Type actualStateType, Type actualEventType) =>
        _eventCache.GetOrAdd(
            (actualStateType, actualEventType),
            static (key, handlers) => EventHandlerRanker.RankedCandidates(handlers, key.State, key.Event),
            _eventHandlers);

    // Returns state handlers assignable from actualStateType, sorted ascending by distance
    // (most derived first = index 0). OnExit iterates forward; OnEnter iterates in reverse.
    internal StateHandler<TContext, TState>[] GetSortedStateHandlers(Type actualStateType) =>
        _stateCache.GetOrAdd(
            actualStateType,
            static (stateType, handlers) =>
                (from h in handlers
                    let d = stateType.DistanceFrom(h.StateType)
                    where d is not null
                    orderby d.Value, h.DeclarationOrder
                    select h).ToArray(),
            _stateHandlers);
}
