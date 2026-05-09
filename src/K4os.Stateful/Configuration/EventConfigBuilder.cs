using K4os.Stateful.Runtime;

namespace K4os.Stateful.Configuration;

public partial class StateMachineConfig<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    private sealed class EventConfigBuilder<TCurrentState, TCurrentEvent>:
        IEventConfig<TCurrentState, TCurrentEvent>
        where TCurrentState: class, TState
        where TCurrentEvent: class, TEvent
    {
        private readonly StateConfigBuilder<TCurrentState> _state;
        private readonly MachineConfigBuilder _machine;
        private readonly EventHandlerConfig<TContext, TState, TEvent> _config;

        internal EventConfigBuilder(
            StateConfigBuilder<TCurrentState> state,
            MachineConfigBuilder machine)
        {
            _state = state;
            _machine = machine;
            _config = new EventHandlerConfig<TContext, TState, TEvent> {
                StateType = typeof(TCurrentState),
                EventType = typeof(TCurrentEvent),
                DeclarationOrder = machine.NextOrder(),
            };
        }

        public IEventConfig<TCurrentState, TCurrentEvent> When(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask<bool>> guard)
        {
            _config.AddGuard(x => guard(x.Convert<TCurrentState, TCurrentEvent>()));
            return this;
        }

        public IStateConfig<TCurrentState> GoTo(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask<TState>> next)
        {
            _config.Action = x => next(x.Convert<TCurrentState, TCurrentEvent>());
            return Commit();
        }

        public IStateConfig<TCurrentState> Stay(
            Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask>? action = null)
        {
            _config.Action = action is null
                ? x => ValueTask.FromResult(x.State)
                : async x => {
                    await action(x.Convert<TCurrentState, TCurrentEvent>());
                    return x.State;
                };

            return Commit();
        }

        private IStateConfig<TCurrentState> Commit()
        {
            _machine.AddEventHandler(_config.ToFrozen());
            return _state;
        }
    }
}
