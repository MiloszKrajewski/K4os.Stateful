namespace K4os.Stateful.Internal;

// Mutable accumulator for one In<TCS>().On<TCE>()...GoTo/Stay() chain.
// Populated across multiple DSL calls; converted to a frozen EventHandler at GoTo/Stay time.
internal sealed class EventHandlerConfig<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    public Type? StateType { get; set; }
    public Type? EventType { get; set; }
    public int DeclarationOrder { get; set; }

    // Multiple When() calls accumulate here; all must pass (AND semantics).
    private List<Func<Activation<TContext, TState, TEvent>, ValueTask<bool>>>? _guards;

    public void AddGuard(Func<Activation<TContext, TState, TEvent>, ValueTask<bool>> guard)
    {
        _guards ??= [];
        _guards.Add(guard);
    }

    public bool HasGuard => _guards is { Count: > 0 };

    public Func<Activation<TContext, TState, TEvent>, ValueTask<TState>>? Action { get; set; }

    public EventHandler<TContext, TState, TEvent> ToFrozen()
    {
        var combinedGuard = BuildCombinedGuard();
        return new EventHandler<TContext, TState, TEvent>(
            StateType.ThrowIfNull(), EventType.ThrowIfNull(), 
            HasGuard, DeclarationOrder,
            combinedGuard, Action.ThrowIfNull());
    }

    private Func<Activation<TContext, TState, TEvent>, ValueTask<bool>>? BuildCombinedGuard()
    {
        if (_guards is not { Count: > 0 }) return null;
        if (_guards.Count == 1) return _guards[0];

        var guards = _guards.ToArray();
        return x => ExecuteGuards(guards, x);
    }

    private static async ValueTask<bool> ExecuteGuards(
        Func<Activation<TContext, TState, TEvent>, ValueTask<bool>>[] guards, 
        Activation<TContext, TState, TEvent> activation)
    {
        foreach (var guard in guards)
            if (!await guard(activation))
                return false;

        return true;
    }
}
