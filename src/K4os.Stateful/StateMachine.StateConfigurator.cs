using System;

namespace K4os.Stateful;

public static partial class StateMachine<TContext, TState, TEvent>
{
    public interface IStateContext<out TActualState>
        where TActualState: TState
    {
        TContext Context { get; }
        TActualState State { get; }
    }

    private class StateContext<TActualState>:
        IStateContext<TActualState>
        where TActualState: TState
    {
        public required TContext Context { get; init; }
        public required TActualState State { get; init; }
    }

    private static IStateContext<TActualState> MakeContext<TActualState>(
        TContext context, TState state)
        where TActualState: TState =>
        new StateContext<TActualState> { Context = context, State = (TActualState)state! };

    public interface IStateConfigurator<out TActualState>
        where TActualState: TState
    {
        IStateConfigurator<TActualState> Name(string name);
        IStateConfigurator<TActualState> OnEnter(Action<IStateContext<TActualState>> context);
        IStateConfigurator<TActualState> OnExit(Action<IStateContext<TActualState>> context);

        IEventConfigurator<TActualState, TActualEvent> On<TActualEvent>()
            where TActualEvent: TEvent;
    }

    public interface IStateConfiguration
    {
        Type StateType { get; }
        string? Name { get; }
        Action<TContext, TState>? OnEnter { get; }
        Action<TContext, TState>? OnExit { get; }
    }

    private class StateConfiguration: IStateConfiguration
    {
        public StateConfiguration(Type stateType)
        {
            StateType = stateType;
        }

        public StateConfiguration(IStateConfiguration other)
        {
            StateType = other.StateType;
            OnEnter = other.OnEnter;
            OnExit = other.OnExit;
        }

        public Type StateType { get; }
        public string? Name { get; set; }
        public Action<TContext, TState>? OnEnter { get; set; }
        public Action<TContext, TState>? OnExit { get; set; }

        public override string ToString() =>
            $"StateConfiguration(Type:{StateType.Name}, Name:'{Name}', OnEnter:{OnEnter != null}, OnExit:{OnExit != null}";
    }

    private class StateConfigurator<TActualState>: IStateConfigurator<TActualState>
        where TActualState: TState
    {
        private readonly Configurator _machineConfigurator;
        private readonly StateConfiguration _stateConfiguration;

        public StateConfigurator(
            Configurator machineConfigurator, StateConfiguration stateConfiguration)
        {
            _machineConfigurator = machineConfigurator;
            _stateConfiguration = stateConfiguration;
        }

        private string TextId => $"State({_stateConfiguration.StateType.Name})";

        public IStateConfigurator<TActualState> Name(string name)
        {
            if (_stateConfiguration.Name != null)
                throw new InvalidOperationException(
                    $"{TextId}.Name has been already defined.");

            _stateConfiguration.Name = name;
            return this;
        }

        public IStateConfigurator<TActualState> OnEnter(
            Action<IStateContext<TActualState>> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (_stateConfiguration.OnEnter != null)
                throw new InvalidOperationException(
                    $"{TextId}.OnEnter has been already defined");

            _stateConfiguration.OnEnter = (c, s) => action(MakeContext<TActualState>(c, s));
            return this;
        }

        public IStateConfigurator<TActualState> OnExit(
            Action<IStateContext<TActualState>> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (_stateConfiguration.OnExit != null)
                throw new InvalidOperationException(
                    $"{TextId}.OnExit has been already defined");

            _stateConfiguration.OnExit = (c, s) => action(MakeContext<TActualState>(c, s));
            return this;
        }

        public IEventConfigurator<TActualState, TActualEvent> On<TActualEvent>()
            where TActualEvent: TEvent =>
            _machineConfigurator.On<TActualState, TActualEvent>();

        public override string ToString() => $"StateConfigurator({_stateConfiguration})";
    }
}