using K4os.Stateful.Runtime;

namespace K4os.Stateful.Configuration;

// Mutable, shared source-of-truth for one state type's lifecycle and event handlers.
// Owned by MachineConfigBuilder; StateConfigBuilder holds a reference, never a copy.
internal sealed class StateHandlerConfig<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    public int DeclarationOrder { get; }
    public Func<Activation<TContext, TState>, ValueTask>? OnEnter { get; private set; }
    public Func<Activation<TContext, TState>, ValueTask>? OnExit { get; private set; }
    public List<EventHandlerConfig<TContext, TState, TEvent>> EventHandlers { get; } = [];

    public StateHandlerConfig(int declarationOrder)
    {
        DeclarationOrder = declarationOrder;
    }

    public void AddOnEnter(Func<Activation<TContext, TState>, ValueTask> wrapped)
    {
        var prev = OnEnter;
        OnEnter = prev is null
            ? wrapped
            : async x => { await prev(x); await wrapped(x); };
    }

    public void AddOnExit(Func<Activation<TContext, TState>, ValueTask> wrapped)
    {
        var prev = OnExit;
        OnExit = prev is null
            ? wrapped
            : async x => { await prev(x); await wrapped(x); };
    }
}
