using System;
using Xunit;

namespace K4os.Stateful.Test
{
	public class ConfigurationTests
	{
		private class TContext { }

		private class TState { }

		private class TEvent { }

		[Fact]
		public void CanConfigureStateUsingInKeyword()
		{
			var configurator = StateMachine<TContext, TState, TEvent>.NewConfigurator();
			var stateConfigurator = configurator.In<TState>();
		}

		[Fact]
		public void CanConfigureEventUsingOnKeyword()
		{
			var configurator = StateMachine<TContext, TState, TEvent>.NewConfigurator();
			var event1Configurator = configurator.On<TState, TEvent>();
			var event2Configurator = configurator.In<TState>().On<TEvent>();
		}

		[Fact]
		public void CannotConfigureOnEnterTwice()
		{
			var configurator = StateMachine<TContext, TState, TEvent>.NewConfigurator();
			configurator.In<TState>().OnEnter(c => { });
			Assert.Throws<InvalidOperationException>(() => configurator.In<TState>().OnEnter(c => { }));
		}

		[Fact]
		public void CannotConfigureOnExitTwice()
		{
			var configurator = StateMachine<TContext, TState, TEvent>.NewConfigurator();
			configurator.In<TState>().OnExit(c => { });
			Assert.Throws<InvalidOperationException>(() => configurator.In<TState>().OnExit(c => { }));
		}
	}
}
