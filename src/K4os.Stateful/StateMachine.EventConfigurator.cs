using System;

namespace K4os.Stateful;

public static partial class StateMachine<TContext, TState, TEvent>
{
    public interface IEventContext<out TActualState, out TActualEvent>:
        IStateContext<TActualState>
        where TActualState: TState
        where TActualEvent: TEvent
    {
        TActualEvent Event { get; }
    }

    private class EventContext<TActualState, TActualEvent>:
        IEventContext<TActualState, TActualEvent>
        where TActualState: TState
        where TActualEvent: TEvent
    {
        public required TContext Context { get; init; }
        public required TActualState State { get; init; }
        public required TActualEvent Event { get; init; }
    }

    private static IEventContext<TActualState, TActualEvent> MakeContext<TActualState, TActualEvent>(
        TContext context, TState state, TEvent @event)
        where TActualState: TState
        where TActualEvent: TEvent
    {
        return new EventContext<TActualState, TActualEvent> {
            Context = context,
            State = (TActualState)state!,
            Event = (TActualEvent)@event!
        };
    }

    public interface IEventConfiguration
    {
        Type StateType { get; }
        Type EventType { get; }

        string? Name { get; }

        Func<TContext, TState, TEvent, bool>? OnValidate { get; }
        Action<TContext, TState, TEvent>? OnTrigger { get; }
        Func<TContext, TState, TEvent, TState>? OnExecute { get; }

        bool IsLoop { get; }
    }

    private class EventConfiguration: IEventConfiguration
    {
        public EventConfiguration(Type stateType, Type eventType)
        {
            StateType = stateType;
            EventType = eventType;
        }

        public EventConfiguration(IEventConfiguration other)
        {
            StateType = other.StateType;
            EventType = other.EventType;
            OnValidate = other.OnValidate;
            OnTrigger = other.OnTrigger;
            OnExecute = other.OnExecute;
            IsLoop = other.IsLoop;
        }

        public Type StateType { get; }
        public Type EventType { get; }

        public string? Name { get; set; }

        public Func<TContext, TState, TEvent, bool>? OnValidate { get; set; }
        public Action<TContext, TState, TEvent>? OnTrigger { get; set; }
        public Func<TContext, TState, TEvent, TState>? OnExecute { get; set; }

        public bool IsLoop { get; set; }

        public override string ToString() => 
            $"EventConfiguration(Type:{StateType.Name}/{EventType.Name}, Name:'{Name}', " + 
            $"OnValidate:{OnValidate != null}, OnTrigger:{OnTrigger != null}, OnExecute:{OnExecute != null}, IsLoop:{IsLoop})";
    }

    public interface IEventConfigurator<out TActualState, out TActualEvent>
        where TActualState: TState
        where TActualEvent: TEvent
    {
        IEventConfigurator<TActualState, TActualEvent> Name(string name);

        IEventConfigurator<TActualState, TActualEvent> OnTrigger(
            Action<IEventContext<TActualState, TActualEvent>> action);

        IEventConfigurator<TActualState, TActualEvent> When(
            Func<IEventContext<TActualState, TActualEvent>, bool> predicate);

        IEventConfigurator<TActualState, TActualEvent> Goto(
            Func<IEventContext<TActualState, TActualEvent>, TState> action);

        IEventConfigurator<TActualState, TActualEvent> Loop();
    }

    private class EventConfigurator<TActualState, TActualEvent>:
        IEventConfigurator<TActualState, TActualEvent>
        where TActualState: TState
        where TActualEvent: TEvent
    {
        private readonly EventConfiguration _configuration;

        public EventConfigurator(EventConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string TextId =>
            $"Event({_configuration.StateType.Name}, {_configuration.EventType.Name})";

        public IEventConfigurator<TActualState, TActualEvent> Name(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (_configuration.Name != null)
                throw new InvalidOperationException(
                    $"{TextId}.Name has been already defined.");

            _configuration.Name = name;
            return this;
        }

        public IEventConfigurator<TActualState, TActualEvent> OnTrigger(
            Action<IEventContext<TActualState, TActualEvent>> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (_configuration.OnTrigger != null)
                throw new InvalidOperationException($"{TextId}.OnTrigger has been already defined.");

            _configuration.OnTrigger = (c, s, e) =>
                action(MakeContext<TActualState, TActualEvent>(c, s, e));
            return this;
        }

        public IEventConfigurator<TActualState, TActualEvent> When(
            Func<IEventContext<TActualState, TActualEvent>, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            if (_configuration.OnValidate != null)
                throw new InvalidOperationException($"{TextId}.When has been already defined.");

            _configuration.OnValidate = (c, s, e) =>
                predicate(MakeContext<TActualState, TActualEvent>(c, s, e));
            return this;
        }

        public IEventConfigurator<TActualState, TActualEvent> Goto(
            Func<IEventContext<TActualState, TActualEvent>, TState> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (_configuration.OnExecute != null)
                throw new InvalidOperationException($"{TextId}.Goto/Loop has been already defined.");

            _configuration.OnExecute = (c, s, e) =>
                action(MakeContext<TActualState, TActualEvent>(c, s, e));
            _configuration.IsLoop = false;
            return this;
        }

        public IEventConfigurator<TActualState, TActualEvent> Loop()
        {
            if (_configuration.OnExecute != null)
                throw new InvalidOperationException($"{TextId}.Goto/Loop has been already defined.");

            _configuration.OnExecute = LoopHandler; // technically it is not needed, but it unifies Loop and Goto
            _configuration.IsLoop = true;
            return this;
        }

        private static TState LoopHandler(TContext context, TState state, TEvent @event)
        {
            return state;
        }

        public override string ToString()
        {
            return $"EventConfigurator({_configuration})";
        }
    }
}
