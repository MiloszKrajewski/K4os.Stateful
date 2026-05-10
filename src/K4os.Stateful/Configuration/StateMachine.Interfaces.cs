using K4os.Stateful.Runtime;

namespace K4os.Stateful.Configuration;

public partial class StateMachineConfig<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    internal static IMachineConfig CreateBuilder() => new MachineConfigBuilder();
    
    public interface IMachineConfig
    {
        IMachineConfig WithStateChangeIf(Func<TState, TState, bool> predicate);
        IStateConfig<TCurrentState> In<TCurrentState>() where TCurrentState: class, TState;
        MachineDefinition<TContext, TState, TEvent> Build();
    }

    public interface IStateConfig<TCurrentState> where TCurrentState: class, TState
    {
        IStateConfig<TCurrentState> OnEnter(Func<Activation<TContext, TCurrentState>, ValueTask> callback);

        IStateConfig<TCurrentState> OnEnter(Func<Activation<TContext, TCurrentState>, Task> callback) =>
            OnEnter(x => new ValueTask(callback(x)));

        IStateConfig<TCurrentState> OnEnter(Action<Activation<TContext, TCurrentState>> callback) =>
            OnEnter(x => {
                callback(x);
                return ValueTask.CompletedTask;
            });

        IStateConfig<TCurrentState> OnExit(Func<Activation<TContext, TCurrentState>, ValueTask> callback);

        IStateConfig<TCurrentState> OnExit(Func<Activation<TContext, TCurrentState>, Task> callback) =>
            OnExit(x => new ValueTask(callback(x)));

        IStateConfig<TCurrentState> OnExit(Action<Activation<TContext, TCurrentState>> callback) =>
            OnExit(x => {
                callback(x);
                return ValueTask.CompletedTask;
            });

        IEventConfig<TCurrentState, TCurrentEvent> On<TCurrentEvent>() where TCurrentEvent: class, TEvent;
        IStateConfig<TNextState> In<TNextState>() where TNextState: class, TState;
        MachineDefinition<TContext, TState, TEvent> Build();
    }

    public interface IEventConfig<TCurrentState, TCurrentEvent>
        where TCurrentState: class, TState
        where TCurrentEvent: class, TEvent
    {
        IEventConfig<TCurrentState, TCurrentEvent> When(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask<bool>> guard);

        IEventConfig<TCurrentState, TCurrentEvent> When(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, Task<bool>> guard) =>
            When(x => new ValueTask<bool>(guard(x)));

        IEventConfig<TCurrentState, TCurrentEvent> When(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, bool> guard) =>
            When(x => new ValueTask<bool>(guard(x)));

        IStateConfig<TCurrentState> GoTo(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask<TState>> next);

        IStateConfig<TCurrentState> GoTo(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, Task<TState>> next) =>
            GoTo(x => new ValueTask<TState>(next(x)));

        IStateConfig<TCurrentState> GoTo(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, TState> next) =>
            GoTo(x => new ValueTask<TState>(next(x)));

        IStateConfig<TCurrentState> Stay() =>
            Stay(_ => ValueTask.CompletedTask);

        IStateConfig<TCurrentState> Stay(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask> action);

        IStateConfig<TCurrentState> Stay(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, Task> action) =>
            Stay(x => new ValueTask(action(x)));

        IStateConfig<TCurrentState> Stay(
            Action<Activation<TContext, TCurrentState, TCurrentEvent>> action) =>
            Stay(x => {
                action(x);
                return ValueTask.CompletedTask;
            });
    }
}
