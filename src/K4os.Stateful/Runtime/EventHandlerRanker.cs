namespace K4os.Stateful.Runtime;

internal static class EventHandlerRanker
{
    // Six-field composite sort key. Ascending order = highest priority first.
    // Priority hierarchy: state specificity > event specificity > guard presence > declaration order.
    // Class beats interface at the same distance on both axes (more specific registration intent).
    // Guarded handlers beat unguarded (unconditional handlers act as fallbacks).
    private readonly struct HandlerSortKey : IComparable<HandlerSortKey>
    {
        private readonly int _stateDistance; // 0 = exact state match; larger = more general
        private readonly int _stateIsInterface; // 0 = class (higher priority), 1 = interface
        private readonly int _eventDistance;
        private readonly int _eventIsInterface;
        private readonly int _hasNoGuard; // 0 = guarded (fires first), 1 = unguarded fallback
        private readonly int _declarationOrder;

        private HandlerSortKey(
            int stateDistance, int stateIsInterface,
            int eventDistance, int eventIsInterface,
            int hasNoGuard, int declarationOrder)
        {
            _stateDistance = stateDistance;
            _stateIsInterface = stateIsInterface;
            _eventDistance = eventDistance;
            _eventIsInterface = eventIsInterface;
            _hasNoGuard = hasNoGuard;
            _declarationOrder = declarationOrder;
        }

        // Returns null when the handler is not type-compatible with the given actual types.
        public static HandlerSortKey? TryCreate(EventHandler handler, Type actualStateType, Type actualEventType)
        {
            var stateDistance = actualStateType.DistanceFrom(handler.StateType);
            if (stateDistance is null) return null;

            var eventDistance = actualEventType.DistanceFrom(handler.EventType);
            if (eventDistance is null) return null;

            return new HandlerSortKey(
                stateDistance.Value, handler.StateType.IsInterface ? 1 : 0,
                eventDistance.Value, handler.EventType.IsInterface ? 1 : 0,
                handler.HasGuard ? 0 : 1,
                handler.DeclarationOrder);
        }

        public int CompareTo(HandlerSortKey other)
        {
            int c;
            return
                (c = _stateDistance.CompareTo(other._stateDistance)) != 0 ? c :
                (c = _stateIsInterface.CompareTo(other._stateIsInterface)) != 0 ? c :
                (c = _eventDistance.CompareTo(other._eventDistance)) != 0 ? c :
                (c = _eventIsInterface.CompareTo(other._eventIsInterface)) != 0 ? c :
                (c = _hasNoGuard.CompareTo(other._hasNoGuard)) != 0 ? c :
                _declarationOrder.CompareTo(other._declarationOrder);
        }
    }

    // Returns all handlers type-compatible with (actualStateType, actualEventType), sorted
    // highest-priority first. Guards are not evaluated here — the executor walks the list
    // and short-circuits on the first handler whose guard passes.
    public static T[] RankedCandidates<T>(
        IReadOnlyList<T> handlers,
        Type actualStateType, Type actualEventType)
        where T : EventHandler =>
        (from handler in handlers
            let key = HandlerSortKey.TryCreate(handler, actualStateType, actualEventType)
            where key is not null
            orderby key.Value
            select handler
        ).ToArray();
}
