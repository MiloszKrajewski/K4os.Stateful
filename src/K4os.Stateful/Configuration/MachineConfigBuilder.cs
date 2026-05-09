using K4os.Stateful.Runtime;

namespace K4os.Stateful.Configuration;

public partial class StateMachineConfig<TContext, TState, TEvent>
{
    private sealed class MachineConfigBuilder: IMachineConfig
    {
        private readonly List<EventHandler<TContext, TState, TEvent>> _eventHandlers = [];
        private readonly List<StateHandler<TContext, TState>> _stateHandlers = [];
        private int _order;

        internal int NextOrder() => _order++;

        internal void AddEventHandler(EventHandler<TContext, TState, TEvent> handler) =>
            _eventHandlers.Add(handler);

        internal void AddStateHandler(StateHandler<TContext, TState> handler) =>
            _stateHandlers.Add(handler);

        public IStateConfig<TCurrentState> In<TCurrentState>()
            where TCurrentState: class, TState =>
            new StateConfigBuilder<TCurrentState>(this);

        public MachineDefinition<TContext, TState, TEvent> Build() =>
            new(_eventHandlers, _stateHandlers);
    }
}
