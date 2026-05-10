namespace K4os.Stateful.Runtime;

internal sealed class StateHandler<TContext, TState>
    where TState: class
{
    public Type StateType { get; }
    public int DeclarationOrder { get; }
    public Func<Activation<TContext, TState>, ValueTask>? OnEnter { get; }
    public Func<Activation<TContext, TState>, ValueTask>? OnExit { get; }
    public Func<Activation<TContext, TState>, ValueTask<TState>>? Auto { get; }

    public StateHandler(
        Type stateType,
        int declarationOrder,
        Func<Activation<TContext, TState>, ValueTask>? onEnter,
        Func<Activation<TContext, TState>, ValueTask>? onExit,
        Func<Activation<TContext, TState>, ValueTask<TState>>? auto = null)
    {
        StateType = stateType;
        DeclarationOrder = declarationOrder;
        OnEnter = onEnter;
        OnExit = onExit;
        Auto = auto;
    }
}
