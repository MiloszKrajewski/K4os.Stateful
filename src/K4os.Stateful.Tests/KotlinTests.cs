using System;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace K4os.Stateful.Test
{
	internal class Context { }

	internal class State { }

	internal class StateA: State { }

	internal class StateB: State { }

	internal class Event { }

	internal class EventA: Event { }

	internal class EventB: Event { }

	public class KotlinTests
	{
		private readonly ITestOutputHelper _testOutputHelper;
		private readonly StringBuilder _visited = new StringBuilder();
		private string Visited => _visited.ToString();

		private void Visit(string state) => _visited.Append(state);
		private void AssertVisited(string states) => Assert.Equal(states, Visited);

		private readonly StateMachine<Context, State, Event>.IConfigurator _config =
			StateMachine<Context, State, Event>.NewConfigurator();

		public KotlinTests(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

		private void Debug(string text) => _testOutputHelper.WriteLine(text);

		[Fact]
		public void CanConfigureState()
		{
			_config
				.In<StateA>()
				.Name("StateA")
				.OnEnter(_ => Debug("Hello from StateA"))
				.OnExit(_ => Debug("StateA says goodbye"));
		}

		[Fact]
		public void CannotReconfigureStateAlias()
		{
			Assert.Throws<InvalidOperationException>(
				() => _config.In<StateA>().Name("x").Name("y"));
		}

		[Fact]
		public void CannotReconfigureStateEnter()
		{
			Assert.Throws<InvalidOperationException>(
				() => _config.In<StateA>()
					.OnEnter(_ => Debug("once"))
					.OnEnter(_ => Debug("twice")));
		}

		[Fact]
		public void CannotReconfigureStateExit()
		{
			Assert.Throws<InvalidOperationException>(
				() => _config.In<StateA>()
					.OnExit(_ => Debug("once"))
					.OnExit(_ => Debug("twice")));
		}

		[Fact]
		public void AccessingSameStateReturnsSameState()
		{
			var x = _config.In<StateA>();
			var y = _config.In<StateA>();
			x.Name("x");
			Assert.Throws<InvalidOperationException>(() => y.Name("y"));
		}

		[Fact]
		public void AccessingSameEventCreatesNewEntry()
		{
			var x = _config.On<StateA, EventA>();
			var y = _config.On<StateA, EventA>();
			Assert.NotSame(x, y);
		}

		[Fact]
		public void CanChainStateAndEvent()
		{
			var s = _config.In<StateA>();
			var e = s.On<EventA>();

			s.OnEnter(_ => Debug("enter"));
			e.OnTrigger(_ => Debug("trigger"));
		}

		[Fact]
		public void TriggersEnterOnInitialState()
		{
			_config.In<StateA>().OnEnter(_ => Visit("A"));
			AssertVisited("");

			_config.NewExecutor(new Context(), new StateA());
			AssertVisited("A");
		}

		[Fact]
		public void TriggersExitOnExit()
		{
			_config.In<StateA>()
				.OnEnter(_ => Visit("Ea"))
				.OnExit(_ => Visit("Xa"));
			_config.In<StateB>()
				.OnEnter(_ => Visit("Eb"));

			_config.On<StateA, EventA>()
				.Goto(
					_ => {
						Visit("Gab");
						return new StateB();
					});

			AssertVisited("");

			var exe = _config.NewExecutor(new Context(), new StateA());
			AssertVisited("Ea");
			exe.Fire(new EventA());

			AssertVisited("EaXaGabEb");
			Assert.IsType<StateB>(exe.State);
		}

		[Fact]
		public void EnterIsTriggeredBottomUp()
		{
			_config.In<State>().OnEnter(_ => Visit("0"));
			_config.In<StateA>().OnEnter(_ => Visit("A"));
			_config.On<StateB, EventA>().Goto(
				_ => {
					Visit("G");
					return new StateA();
				});
			var exe = _config.NewExecutor(new Context(), new StateB());
			AssertVisited("0");
			exe.Fire(new EventA());
			AssertVisited("0G0A");
		}

		[Fact]
		public void ExitIsTriggeredTopDown()
		{
			_config.In<State>().OnExit(_ => Visit("0"));
			_config.In<StateA>().OnExit(_ => Visit("A"));
			_config.On<StateA, Event>().Goto(
				_ => {
					Visit("Ga");
					return new StateB();
				});
			_config.On<StateB, Event>().Goto(
				_ => {
					Visit("Gb");
					return new StateA();
				});
			var exe = _config.NewExecutor(new Context(), new StateA());
			AssertVisited("");
			exe.Fire(new Event()); // StateA -> StateB
			AssertVisited("GaA0");
		}
	}
}

//[Fact]
//public void `Triggers are triggered when pattern matches`() {
//cfg.state(State::class)
//
//cfg.state(StateA::class)
//
//cfg.event (State::class, Event::class).trigger {
//	visit("0")
//}
//
//cfg.event (StateA::class, EventA::class).goto {
//	StateB()
//}
//cfg.event (StateB::class, EventB::class).goto {
//	StateA()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//assertVisited("")
//exe.fire(EventA())
//assertVisited("0")
//}
//[Fact]
//public void `Triggers are not triggered when filtered out`() {
//cfg.state(State::class)
//
//cfg.state(StateA::class)
//
//cfg.event (State::class, Event::class).trigger {
//	visit("A")
//}.filter {
//	false
//}
//
//cfg.event (StateA::class, EventA::class).goto {
//	StateB()
//}
//cfg.event (StateB::class, EventB::class).goto {
//	StateA()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//assertVisited("")
//exe.fire(EventA())
//assertVisited("")
//}
//[Fact]
//public void `Multiple triggers can be triggered`() {
//cfg.event (State::class, Event::class).trigger {
//	visit("0")
//}
//
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("1")
//}
//
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("2")
//}.goto {
//	visit("3");
//	StateB()
//}
//cfg.event (StateB::class, EventB::class).goto {
//	StateA()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//exe.fire(EventA())
//assertVisited("0123")
//}
//[Fact]
//public void `Triggers are triggered bottom-up and in order of definition`() {
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("0")
//}
//
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("1")
//}.goto {
//	visit("2");
//	StateB()
//}
//cfg.event (State::class, Event::class).trigger {
//	visit("3")
//}
//
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("4")
//}
//
//cfg.event (StateB::class, EventB::class).trigger {
//	visit("5")
//}.goto {
//	StateA()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//exe.fire(EventA())
//assertVisited("30142")
//}
//[Fact]
//public void `Only one transition from state is allowed`() {
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("Ta1")
//}.goto {
//	visit("Ga1");
//	StateB()
//}
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("Ta2")
//}.goto {
//	visit("Ga2");
//	StateB()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//assertFailsWith(UnsupportedOperationException::class) {
//	exe.fire(EventA())
//}
//assertVisited("") // nothing gets not executed
//}
//[Fact]
//public void `Fallback is triggered in declaration order, but used last for transition`() {
//cfg.event (StateA::class, Event::class).filter {
//	true
//}.trigger {
//	visit("t1")
//}
//
//cfg.event (StateA::class, Event::class).trigger {
//	visit("fb")
//}.goto {
//	visit("Gb");
//	StateB()
//}
//cfg.event (StateA::class, Event::class).filter {
//	true
//}.trigger {
//	visit("t2")
//}.goto {
//	visit("Ga");
//	StateA()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//exe.fire(Event())
//assertVisited("t1fbt2Ga")
//}
//[Fact]
//public void `Triggers are executed in order of hierarchy, closest one is used for transition`
//() {
//cfg.event (StateA::class, Event::class).trigger {
//	visit("2")
//}
//
//cfg.event (State::class, Event::class).trigger {
//	visit("0")
//}
//
//cfg.event (StateA::class, EventA::class).trigger {
//	visit("3")
//}.goto {
//	visit("Gx");
//	StateB()
//}
//cfg.event (State::class, EventA::class).trigger {
//	visit("1")
//}.goto {
//	visit("Gy");
//	StateB()
//}
//cfg.event (State::class, Event::class)
//
//val exe = cfg.createExecutor(Context(), StateA())
//exe.fire(EventA())
//assertVisited("0123Gx")
//}
//[Fact]
//public void `Multiple transition clash witch each-other`() {
//cfg.event (StateA::class, EventA::class)
//	.trigger {
//	visit("T1")
//}
//
//.filter {
//	true
//}
//
//.goto {
//	visit("G1");
//	StateB()
//}
//cfg.event (StateA::class, EventA::class)
//	.trigger {
//	visit("T2")
//}
//
//.filter {
//	true
//}
//
//.goto {
//	visit("G2");
//	StateB()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//assertFailsWith(UnsupportedOperationException::class) {
//	exe.fire(EventA())
//}
//assertVisited("")
//}
//[Fact]
//public void `Multiple transition can be defined for the same state as long as they are
//exclusive`(
//) {
//cfg.event (StateA::class, EventA::class)
//	.trigger {
//	visit("T1")
//}
//
//.filter {
//	false
//}
//
//.goto {
//	visit("G1");
//	StateB()
//}
//cfg.event (StateA::class, EventA::class)
//	.trigger {
//	visit("T2")
//}
//
//.filter {
//	true
//}
//
//.goto {
//	visit("G2");
//	StateB()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//exe.fire(EventA())
//assertVisited("T2G2")
//}
//[Fact]
//public void `Multiple transition can be defined for the same state as long as they are of
//different rank`() {
//cfg.event (StateA::class, Event::class)
//	.trigger {
//	visit("T1")
//}
//
//.filter {
//	true
//}
//
//.goto {
//	visit("G1");
//	StateB()
//}
//cfg.event (StateA::class, EventA::class)
//	.trigger {
//	visit("T2")
//}
//
//.filter {
//	true
//}
//
//.goto {
//	visit("G2");
//	StateB()
//}
//val exe = cfg.createExecutor(Context(), StateA())
//exe.fire(EventA())
//assertVisited("T1T2G2")
//}
//}
