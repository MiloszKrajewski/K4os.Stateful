using K4os.Stateful.Runtime;

namespace K4os.Stateful.Configuration;

// Mutable accumulator for one In<TCurrentState>() context.
// Delegates are stored already wrapped to base types; wrapping happens in StateConfigBuilder<TCS>.
internal sealed class StateHandlerConfig<TContext, TState>
    where TState: class
{
    public Func<Activation<TContext, TState>, ValueTask>? OnEnter { get; set; }
    public Func<Activation<TContext, TState>, ValueTask>? OnExit { get; set; }

    public StateHandler<TContext, TState> ToFrozen(Type stateType, int declarationOrder) =>
        new(stateType, declarationOrder, OnEnter, OnExit);
}
