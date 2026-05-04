using System;
using K4os.Stateful.Internal;
using System.Linq;

namespace K4os.Stateful;

public static partial class StateMachine<TContext, TState, TEvent>
{
	public interface IExecutor
	{
		TContext Context { get; }
		TState State { get; }

		void Fire(TEvent @event);
	}

	private class Executor: IExecutor
	{
		private readonly StateConfiguration[] _stateConfigList;
		private readonly EventConfiguration[] _eventConfigList;

		private readonly IDictionary<Type, StateExecutor[]> _stateConfigCache =
			new Dictionary<Type, StateExecutor[]>();

		private readonly IDictionary<EventKey, EventExecutor[]> _eventConfigCache =
			new Dictionary<EventKey, EventExecutor[]>();

		public TContext Context { get; }
		public TState State { get; private set; }

		public Executor(
			IConfigurationProvider configuration,
			TContext context, TState state)
		{
			configuration.EnsureNotNull("configuration");
			state.EnsureNotNull("state");

			_stateConfigList = configuration.States.Select(s => new StateConfiguration(s)).ToArray();
			_eventConfigList = configuration.Events.Select(e => new EventConfiguration(e)).ToArray();
			Context = context;
			State = state;

			OnEnter();
		}

		public void Fire(TEvent @event)
		{
			@event.EnsureNotNull("event");
			OnFire(@event);
		}

		private void OnEnter()
		{
			var stateType = State.GetType();
			var configs = _stateConfigCache.GetOrCreate(stateType, CacheStateConfiguration);
			configs.Iterate(s => s.Enter(Context, State));
		}

		private void OnExit()
		{
			var stateType = State.GetType();
			var configs = _stateConfigCache.GetOrCreate(stateType, CacheStateConfiguration);
			Enumerable.Reverse(configs).Iterate(s => s.Exit(Context, State));
		}

		private void OnFire(TEvent @event)
		{
			var stateType = State.GetType();
			var eventType = @event.GetType();
			var eventKey = new EventKey(stateType, eventType);

			var configs = // all rules applicable for this state/event
				_eventConfigCache.GetOrCreate(eventKey, CacheEventConfiguration)
					.Where(e => e.Validate(Context, State, @event))
					.ToArray();
			var transitions = // only ones which claim to decide where to go next
				configs.Where(e => e.IsTransition).Reverse().ToArray();

			var firstTransition = transitions.Length <= 0 ? null : transitions[0];
			if (firstTransition == null)
				throw new InvalidOperationException(
					$"Unexpected event '{eventType.Name}' in state '{stateType.Name}'. No transitions defined.");

			var secondTransition = transitions.Length <= 1 ? null : transitions[1];
			if (secondTransition != null && secondTransition.CompareTo(firstTransition) <= 0)
				throw new InvalidOperationException(
					$"Unhandled event '{eventType.Name}' in state '{stateType.Name}'. Ambiguous transitions defined.");

			configs.Iterate(e => e.Trigger(Context, State, @event));

			if (firstTransition.IsLoop)
				// for 'loop' transition we stay in the same state without triggering OnEnter nor OnExit
				return;

			OnExit();
			State = firstTransition.Execute(Context, State, @event);
			OnEnter();
		}

		private StateExecutor[] CacheStateConfiguration(Type stateType)
		{
			var result = _stateConfigList
				.Where(s => stateType.InheritsFrom(s.StateType))
				.Select(s => new StateExecutor(stateType, s))
				.ToArray();
			Array.Sort(result);
			Array.Reverse(result);
			return result;
		}

		private EventExecutor[] CacheEventConfiguration(EventKey eventKey)
		{
			var stateType = eventKey.StateType;
			var eventType = eventKey.EventType;

			bool IsMatchingEvent(EventConfiguration eventConfiguration) => 
				stateType.InheritsFrom(eventConfiguration.StateType) && 
				eventType.InheritsFrom(eventConfiguration.EventType);

			var result = _eventConfigList
				.Where(IsMatchingEvent)
				.Select(e => new EventExecutor(stateType, eventType, e))
				.ToArray();
			Array.Sort(result);
			Array.Reverse(result);
			return result;
		}
	}

	public static IExecutor NewExecutor(
		IConfigurationProvider configuration, TContext context, TState state) =>
		new Executor(configuration, context, state);
}