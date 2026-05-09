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
        private readonly StateHandlerConfig<TContext, TState> _config = new();

        internal StateConfigBuilder(MachineConfigBuilder machine)
        {
            _machine = machine;
        }

        private void Flush()
        {
            if (_config.OnEnter is null && _config.OnExit is null)
                return;

            _machine.AddStateHandler(_config.ToFrozen(typeof(TCurrentState), _machine.NextOrder()));
            _config.OnEnter = null;
            _config.OnExit = null;
        }

        public IStateConfig<TCurrentState> OnEnter(Func<Activation<TContext, TCurrentState>, ValueTask> callback)
        {
            _config.OnEnter = Combine(_config.OnEnter, callback);
            return this;
        }

        public IStateConfig<TCurrentState> OnExit(Func<Activation<TContext, TCurrentState>, ValueTask> callback)
        {
            _config.OnExit = Combine(_config.OnExit, callback);
            return this;
        }
        
        private static Func<Activation<TContext, TState>, ValueTask> Combine(
            Func<Activation<TContext, TState>, ValueTask>? prev,
            Func<Activation<TContext, TCurrentState>, ValueTask> callback) =>
            prev is null
                ? x => callback(x.Convert<TCurrentState>())
                : async x => {
                    await prev(x);
                    await callback(x.Convert<TCurrentState>());
                };

        public IEventConfig<TCurrentState, TCurrentEvent> On<TCurrentEvent>()
            where TCurrentEvent: class, TEvent =>
            new EventConfigBuilder<TCurrentState, TCurrentEvent>(this, _machine);

        public IStateConfig<TNextState> In<TNextState>()
            where TNextState: class, TState
        {
            Flush();
            return new StateConfigBuilder<TNextState>(_machine);
        }

        public MachineDefinition<TContext, TState, TEvent> Build()
        {
            Flush();
            return _machine.Build();
        }
    }
}
