using K4os.Stateful.Configuration;

namespace K4os.Stateful;

public static class StateMachine
{
    public static StateMachineConfig<TContext, TState, TEvent>.IMachineConfig Define<TContext, TState, TEvent>()
        where TState: class
        where TEvent: class =>
        StateMachineConfig<TContext, TState, TEvent>.CreateBuilder();
}
