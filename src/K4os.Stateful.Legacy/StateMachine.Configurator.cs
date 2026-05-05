using System;
using System.Linq;
using K4os.Stateful.Legacy.Internal;

namespace K4os.Stateful.Legacy;

public static partial class StateMachine<TContext, TState, TEvent> 
	where TState: notnull
	where TEvent: notnull
{
	/// <summary>Provides state machine configuration for IExecutor.</summary>
	public interface IConfigurationProvider
	{
		IEnumerable<IStateConfiguration> States { get; }
		IEnumerable<IEventConfiguration> Events { get; }
	}

	/// <summary>Allows to configure state machine.</summary>
	public interface IConfigurator: IConfigurationProvider
	{
		IStateConfigurator<TActualState> In<TActualState>()
			where TActualState: TState;
		
		IEventConfigurator<TActualState, TActualEvent> On<TActualState, TActualEvent>()
			where TActualState: TState
			where TActualEvent: TEvent;
	}

	/// <inheritdoc />
	/// <summary>
	/// Implementation of <see cref="IConfigurator" />
	/// </summary>
	private class Configurator: IConfigurator
	{
		private readonly IDictionary<Type, StateConfiguration> _states =
			new Dictionary<Type, StateConfiguration>();

		private readonly IDictionary<EventKey, IList<EventConfiguration>> _events =
			new Dictionary<EventKey, IList<EventConfiguration>>();

		public IStateConfigurator<TActualState> In<TActualState>()
			where TActualState: TState
		{
			var stateType = typeof(TActualState);
			var stateData = _states.GetOrCreate(stateType, t => new StateConfiguration(t));
				// .TryGet(stateType, TryGetMode.GetOrCreate, t => new StateConfiguration(t));
			return new StateConfigurator<TActualState>(this, stateData);
		}

		public IEventConfigurator<TActualState, TActualEvent> On<TActualState, TActualEvent>()
			where TActualState: TState
			where TActualEvent: TEvent
		{
			var eventMap = _events;
			var stateType = typeof(TActualState);
			var eventType = typeof(TActualEvent);
			var eventKey = new EventKey(stateType, eventType);
			var eventList = eventMap.GetOrCreate(eventKey, _ => new List<EventConfiguration>());
			var eventData = new EventConfiguration(stateType, eventType);
			eventList.Add(eventData);
			return new EventConfigurator<TActualState, TActualEvent>(eventData);
		}

		public IEnumerable<IStateConfiguration> States => _states.Values;
		public IEnumerable<IEventConfiguration> Events => _events.Values.SelectMany(x => x);
	}

	/// <summary>Create new state machine configurator.</summary>
	/// <returns>New configurator.</returns>
	public static IConfigurator NewConfigurator() => new Configurator();
}