using K4os.Stateful.Runtime;

namespace K4os.Stateful.Configuration;

public partial class StateMachineConfig<TContext, TState, TEvent>
    where TState: class
    where TEvent: class
{
    private sealed class StateConfigBuilder<TCurrentState>: IStateConfig<TCurrentState>
        where TCurrentState: class, TState
    {
        private readonly MachineConfigBuilder _machine;
        private readonly StateHandlerConfig<TContext, TState, TEvent> _config;

        internal StateConfigBuilder(MachineConfigBuilder machine)
        {
            _machine = machine;
            _config = machine.GetOrCreateStateConfig(typeof(TCurrentState));
        }

        public IStateConfig<TCurrentState> OnEnter(Func<Activation<TContext, TCurrentState>, ValueTask> callback)
        {
            _config.AddOnEnter(x => callback(x.Convert<TCurrentState>()));
            return this;
        }

        public IStateConfig<TCurrentState> OnExit(Func<Activation<TContext, TCurrentState>, ValueTask> callback)
        {
            _config.AddOnExit(x => callback(x.Convert<TCurrentState>()));
            return this;
        }

        public IMachineConfig Auto(Func<Activation<TContext, TCurrentState>, ValueTask<TState>> handler)
        {
            _config.SetAutoHandler(async x => await handler(x.Convert<TCurrentState>()));
            return _machine;
        }

        public IEventConfig<TCurrentState, TCurrentEvent> On<TCurrentEvent>()
            where TCurrentEvent: class, TEvent =>
            new EventConfigBuilder<TCurrentState, TCurrentEvent>(this, _config, _machine);

        public IStateConfig<TNextState> In<TNextState>()
            where TNextState: class, TState =>
            new StateConfigBuilder<TNextState>(_machine);

        public MachineDefinition<TContext, TState, TEvent> Build() =>
            _machine.Build();
    }
}
