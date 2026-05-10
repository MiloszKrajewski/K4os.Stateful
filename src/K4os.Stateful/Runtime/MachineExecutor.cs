using K4os.Stateful.Internal;

namespace K4os.Stateful.Runtime;

public sealed class MachineExecutor<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    private readonly MachineDefinition<TContext, TState, TEvent> _definition;
    private readonly TContext _context;
    private TState _state;
    private int _firing;

    internal MachineExecutor(
        MachineDefinition<TContext, TState, TEvent> definition,
        TContext context, TState state)
    {
        _definition = definition;
        _context = context;
        _state = state;
    }

    public TState State => _state;

    public async ValueTask FireAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _firing, 1, 0) != 0)
            throw new ConcurrentFireException();
        try
        {
            await RunAutoChainAsync(cancellationToken);
            await TryFireCoreAsync(@event, true, cancellationToken);
            await RunAutoChainAsync(cancellationToken);
        }
        finally
        {
            Volatile.Write(ref _firing, 0);
        }
    }

    public async ValueTask<bool> TryFireAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _firing, 1, 0) != 0)
            throw new ConcurrentFireException();
        try
        {
            await RunAutoChainAsync(cancellationToken);
            if (!await TryFireCoreAsync(@event, false, cancellationToken)) return false;
            await RunAutoChainAsync(cancellationToken);
            return true;
        }
        finally
        {
            Volatile.Write(ref _firing, 0);
        }
    }

    public async ValueTask IdleAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _firing, 1, 0) != 0)
            throw new ConcurrentFireException();
        try
        {
            await RunAutoChainAsync(cancellationToken);
        }
        finally
        {
            Volatile.Write(ref _firing, 0);
        }
    }

    public void Fire(TEvent @event, CancellationToken cancellationToken = default) =>
        FireAsync(@event, cancellationToken).GetAwaiter().GetResult();

    public bool TryFire(TEvent @event, CancellationToken cancellationToken = default) =>
        TryFireAsync(@event, cancellationToken).GetAwaiter().GetResult();

    public void Idle(CancellationToken cancellationToken = default) =>
        IdleAsync(cancellationToken).GetAwaiter().GetResult();

    private async ValueTask<bool> TryFireCoreAsync(
        TEvent @event, bool throwOnNoMatch, CancellationToken cancellationToken)
    {
        var thisState = _state;
        var activation = Activation.Create(_context, thisState, @event, cancellationToken);
        var handler = await FindMatchingHandler(activation, throwOnNoMatch);
        if (handler is null) return false;

        var nextState = await handler.Action(activation);
        var stateChanged = _definition.StateChanged(thisState, nextState);

        if (stateChanged) await OnExit(thisState, cancellationToken);
        _state = nextState;
        if (!stateChanged) return true;

        await OnEnter(nextState, cancellationToken);
        return true;
    }

    private async ValueTask RunAutoChainAsync(CancellationToken ct)
    {
        while (true)
        {
            var state = _state;
            var stateHandlers = _definition.GetStateHandlers(state.GetType());
            var autoHandler = stateHandlers.FirstOrDefault(h => h.Auto is not null);
            if (autoHandler?.Auto is null) break;

            var activation = Activation.Create(_context, state, ct);
            var nextState = await autoHandler.Auto(activation);

            var stateChanged = _definition.StateChanged(state, nextState);
            if (stateChanged) await OnExit(state, ct);
            _state = nextState;
            if (!stateChanged) break;

            await OnEnter(nextState, ct);
        }
    }

    private async ValueTask OnEnter(TState state, CancellationToken ct)
    {
        var enterHandlers = _definition.GetStateHandlers(state.GetType());
        var activation = Activation.Create(_context, state, ct);
        foreach (var handler in enterHandlers.InReverse())
        {
            var action = handler.OnEnter;
            if (action is null) continue;

            await action(activation);
        }
    }

    private async ValueTask OnExit(TState state, CancellationToken ct)
    {
        var exitHandlers = _definition.GetStateHandlers(state.GetType());
        var activation = Activation.Create(_context, state, ct);
        foreach (var handler in exitHandlers)
        {
            var action = handler.OnExit;
            if (action is null) continue;

            await action(activation);
        }
    }

    private async Task<EventHandler<TContext, TState, TEvent>?> FindMatchingHandler(
        Activation<TContext, TState, TEvent> activation, bool throwOnNoMatch)
    {
        var state = activation.State;
        var @event = activation.Event;
        var handlers = _definition.GetEventHandlers(state.GetType(), @event.GetType());

        foreach (var handler in handlers)
        {
            if (handler.Guard is null || await handler.Guard(activation))
                return handler;
        }

        return throwOnNoMatch
            ? throw new UnhandledEventException(@event, state.GetType())
            : null;
    }
}
