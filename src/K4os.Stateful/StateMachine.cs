namespace K4os.Stateful;

public static class StateMachine
{
    public static StateMachine<TContext, TState, TEvent>.IMachineConfig Define<TContext, TState, TEvent>()
        where TState: class
        where TEvent: class =>
        StateMachine<TContext, TState, TEvent>.CreateBuilder();
}

// Generic outer class — TContext, TState, TEvent are implicitly available to all nested types.
// Each nested interface and builder class lives in its own file via partial class.
public partial class StateMachine<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    internal static IMachineConfig CreateBuilder() => new MachineConfigBuilder();
}
