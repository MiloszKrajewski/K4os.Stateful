namespace K4os.Stateful;

public static class StateMachineExtenders
{
    public static StateMachine<TContext, TState, TEvent>.IExecutor NewExecutor<TContext, TState, TEvent>(
        this StateMachine<TContext, TState, TEvent>.IConfigurationProvider configuration,
        TContext context, TState state)
        where TState: notnull where TEvent: notnull =>
        StateMachine<TContext, TState, TEvent>.NewExecutor(configuration, context, state);
}
