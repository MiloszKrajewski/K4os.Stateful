namespace K4os.Stateful.Runtime;

public static class Activation
{
    public static Activation<TContext, TState> Create<TContext, TState>(
        TContext context, TState state, CancellationToken cancellationToken)
        where TState: class =>
        new(context, state, cancellationToken);
    
    public static Activation<TContext, TState, TEvent> Create<TContext, TState, TEvent>(
        TContext context, TState state, TEvent @event, CancellationToken cancellationToken)
        where TState: class where TEvent: class =>
        new(context, state, @event, cancellationToken);
}

public sealed class Activation<TContext, TState>
    where TState: class
{
    public TContext Context { get; }
    public TState State { get; }
    public CancellationToken CancellationToken { get; }

    public Activation(TContext context, TState state, CancellationToken cancellationToken)
    {
        Context = context;
        State = state;
        CancellationToken = cancellationToken;
    }
    
    public Activation<TContext, TActualState> Convert<TActualState>() 
        where TActualState: class, TState =>
        new(Context, (TActualState)State, CancellationToken);
}

public sealed class Activation<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    public TContext Context { get; }
    public TState State { get; }
    public TEvent Event { get; }
    public CancellationToken CancellationToken { get; }

    public Activation(TContext context, TState state, TEvent @event, CancellationToken cancellationToken)
    {
        Context = context;
        State = state;
        Event = @event;
        CancellationToken = cancellationToken;
    }
    
    public Activation<TContext, TState, TActualEvent> Convert<TActualEvent>()
        where TActualEvent: class, TEvent =>
        new(Context, State, (TActualEvent)Event, CancellationToken);
    
    public Activation<TContext, TActualState, TActualEvent> Convert<TActualState, TActualEvent>()
        where TActualState: class, TState
        where TActualEvent: class, TEvent =>
        new(Context, (TActualState)State, (TActualEvent)Event, CancellationToken);
}
