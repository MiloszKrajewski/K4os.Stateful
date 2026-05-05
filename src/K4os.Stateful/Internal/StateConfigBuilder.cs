using K4os.Stateful.Internal;

namespace K4os.Stateful;

public partial class StateMachine<TContext, TState, TEvent>
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
            var prev = _config.OnEnter;
            if (prev is null)
                _config.OnEnter = x => callback(x.Convert<TCurrentState>());
            else
                _config.OnEnter = async x => { await prev(x); await callback(x.Convert<TCurrentState>()); };
            return this;
        }

        public IStateConfig<TCurrentState> OnExit(Func<Activation<TContext, TCurrentState>, ValueTask> callback)
        {
            var prev = _config.OnExit;
            if (prev is null)
                _config.OnExit = x => callback(x.Convert<TCurrentState>());
            else
                _config.OnExit = async x => { await prev(x); await callback(x.Convert<TCurrentState>()); };
            return this;
        }

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
