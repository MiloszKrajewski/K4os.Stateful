using K4os.Stateful.Runtime;

namespace K4os.Stateful.Configuration;

public partial class StateMachineConfig<TContext, TState, TEvent>
{
    private sealed class MachineConfigBuilder: IMachineConfig
    {
        private readonly Dictionary<Type, StateHandlerConfig<TContext, TState, TEvent>> _stateConfigs = [];
        private int _order;
        private Func<TState, TState, bool>? _stateChangePredicate;

        internal int NextOrder() => _order++;

        internal StateHandlerConfig<TContext, TState, TEvent> GetOrCreateStateConfig(Type stateType)
        {
            if (!_stateConfigs.TryGetValue(stateType, out var config))
                _stateConfigs[stateType] = config = new StateHandlerConfig<TContext, TState, TEvent>(_order++);
            return config;
        }

        public IMachineConfig WithStateChangeIf(Func<TState, TState, bool> predicate)
        {
            _stateChangePredicate = predicate;
            return this;
        }

        public IStateConfig<TCurrentState> In<TCurrentState>()
            where TCurrentState: class, TState =>
            new StateConfigBuilder<TCurrentState>(this);

        public MachineDefinition<TContext, TState, TEvent> Build()
        {
            foreach (var (stateType, stateConfig) in _stateConfigs)
                foreach (var eventConfig in stateConfig.EventHandlers)
                    if (!eventConfig.IsComplete)
                        throw new IncompleteEventHandlerException(stateType, eventConfig.EventType!);

            var stateHandlers = new List<StateHandler<TContext, TState>>();
            var eventHandlers = new List<EventHandler<TContext, TState, TEvent>>();

            foreach (var (stateType, stateConfig) in _stateConfigs)
            {
                if (stateConfig.OnEnter is not null || stateConfig.OnExit is not null || stateConfig.AutoHandler is not null)
                    stateHandlers.Add(new StateHandler<TContext, TState>(
                        stateType, stateConfig.DeclarationOrder, stateConfig.OnEnter, stateConfig.OnExit, stateConfig.AutoHandler));

                foreach (var eventConfig in stateConfig.EventHandlers)
                    eventHandlers.Add(eventConfig.ToFrozen());
            }

            return new MachineDefinition<TContext, TState, TEvent>(
                eventHandlers, stateHandlers, _stateChangePredicate ?? DefaultPredicate);
        }

        private static bool DefaultPredicate(TState s1, TState s2) => !ReferenceEquals(s1, s2);
    }
}
