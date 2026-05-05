using K4os.Stateful.Internal;
using EventHandler = K4os.Stateful.Internal.EventHandler;

namespace K4os.Stateful.Tests;

public class EventHandlerRankerTests
{
    // State hierarchy:
    //   object ← [IBase] ← A ← [IMid] ← B ← [ILeaf] ← C
    //   IBase introduced at A (distance 2 from C), IMid at B (d=1), ILeaf at C (d=0)
    private interface IBase;
    private interface IMid;
    private interface ILeaf;
    private class A : IBase;
    private class B : A, IMid;
    private class C : B, ILeaf;

    // Event hierarchy:
    //   object ← [IEventBase] ← EventA ← EventB
    //   IEventBase introduced at EventA (distance 0 from EventA, distance 1 from EventB)
    private interface IEventBase;
    private class EventA : IEventBase;
    private class EventB : EventA;

    // Unrelated types — not in any hierarchy above
    private class UnrelatedState;
    private class UnrelatedEvent;

    private static EventHandler Handler(Type state, Type evt, bool hasGuard = false, int order = 0) =>
        new(state, evt, hasGuard, order);

    private static EventHandler[] Ranked(IReadOnlyList<EventHandler> handlers, Type state, Type evt) =>
        EventHandlerRanker.RankedCandidates(handlers, state, evt);

    [Fact]
    public void ConcreteTypeRuleRanksBeforeBaseTypeRule()
    {
        var baseHandler = Handler(typeof(A), typeof(EventA));
        var concreteHandler = Handler(typeof(C), typeof(EventA));

        var result = Ranked([baseHandler, concreteHandler], typeof(C), typeof(EventA));

        Assert.Equal(concreteHandler, result[0]);
        Assert.Equal(baseHandler, result[1]);
    }

    [Fact]
    public void MultiLevelHierarchyRankedByIncreasingDistance()
    {
        var handlerA = Handler(typeof(A), typeof(EventA)); // state distance 2
        var handlerB = Handler(typeof(B), typeof(EventA)); // state distance 1
        var handlerC = Handler(typeof(C), typeof(EventA)); // state distance 0

        var result = Ranked([handlerA, handlerB, handlerC], typeof(C), typeof(EventA));

        Assert.Equal(handlerC, result[0]);
        Assert.Equal(handlerB, result[1]);
        Assert.Equal(handlerA, result[2]);
    }

    [Fact]
    public void ClassRuleRanksBeforeInterfaceRuleAtSameStateDistance()
    {
        // ILeaf is introduced by C — both C and ILeaf are at state distance 0
        var interfaceHandler = Handler(typeof(ILeaf), typeof(EventA));
        var classHandler = Handler(typeof(C), typeof(EventA));

        var result = Ranked([interfaceHandler, classHandler], typeof(C), typeof(EventA));

        Assert.Equal(classHandler, result[0]);
        Assert.Equal(interfaceHandler, result[1]);
    }

    [Fact]
    public void InterfaceAtDeeperLevelRanksBeforeClassAtShallowerLevel()
    {
        // ILeaf: state distance 0 (interface) vs A: state distance 2 (class)
        // Distance dominates class/interface — deeper interface beats shallower class
        var shallowClass = Handler(typeof(A), typeof(EventA));     // (d=2, class)
        var deepInterface = Handler(typeof(ILeaf), typeof(EventA)); // (d=0, interface)

        var result = Ranked([shallowClass, deepInterface], typeof(C), typeof(EventA));

        Assert.Equal(deepInterface, result[0]);
        Assert.Equal(shallowClass, result[1]);
    }

    [Fact]
    public void SpecificEventRanksBeforeBaseEventAtSameStateDistance()
    {
        // When firing EventB: EventB (d=0) beats EventA (d=1)
        var baseEvent = Handler(typeof(C), typeof(EventA));     // event distance 1 from EventB
        var specificEvent = Handler(typeof(C), typeof(EventB)); // event distance 0 from EventB

        var result = Ranked([baseEvent, specificEvent], typeof(C), typeof(EventB));

        Assert.Equal(specificEvent, result[0]);
        Assert.Equal(baseEvent, result[1]);
    }

    [Fact]
    public void ClassEventRuleRanksBeforeInterfaceEventRuleAtSameEventDistance()
    {
        // IEventBase is introduced at EventA — both at event distance 0 from EventA
        var interfaceEvent = Handler(typeof(C), typeof(IEventBase));
        var classEvent = Handler(typeof(C), typeof(EventA));

        var result = Ranked([interfaceEvent, classEvent], typeof(C), typeof(EventA));

        Assert.Equal(classEvent, result[0]);
        Assert.Equal(interfaceEvent, result[1]);
    }

    [Fact]
    public void GuardedRuleRanksBeforeUnguardedAtSameDistanceAndKind()
    {
        var unguarded = Handler(typeof(C), typeof(EventA), hasGuard: false, order: 0);
        var guarded = Handler(typeof(C), typeof(EventA), hasGuard: true, order: 1);

        var result = Ranked([unguarded, guarded], typeof(C), typeof(EventA));

        Assert.Equal(guarded, result[0]);
        Assert.Equal(unguarded, result[1]);
    }

    [Fact]
    public void UnguardedClassRuleRanksBeforeGuardedInterfaceAtSameStateDistance()
    {
        // Class beats interface at same distance, regardless of guard.
        // Specificity (class vs interface) has higher priority than guard presence.
        var guardedInterface = Handler(typeof(ILeaf), typeof(EventA), hasGuard: true, order: 0);
        var unguardedClass = Handler(typeof(C), typeof(EventA), hasGuard: false, order: 1);

        var result = Ranked([guardedInterface, unguardedClass], typeof(C), typeof(EventA));

        Assert.Equal(unguardedClass, result[0]);
        Assert.Equal(guardedInterface, result[1]);
    }

    [Fact]
    public void DeclarationOrderBreaksTiesAmongEquallyRankedRules()
    {
        var first = Handler(typeof(C), typeof(EventA), hasGuard: true, order: 0);
        var second = Handler(typeof(C), typeof(EventA), hasGuard: true, order: 1);
        var third = Handler(typeof(C), typeof(EventA), hasGuard: true, order: 2);

        // Pass in reverse order to ensure sort is not a no-op
        var result = Ranked([third, first, second], typeof(C), typeof(EventA));

        Assert.Equal(first, result[0]);
        Assert.Equal(second, result[1]);
        Assert.Equal(third, result[2]);
    }

    [Fact]
    public void RuleRegisteredForSubtypeDoesNotMatchActualBaseType()
    {
        // Handler registered for C; actual state is B — B is not a subtype of C
        var tooSpecific = Handler(typeof(C), typeof(EventA));

        var result = Ranked([tooSpecific], typeof(B), typeof(EventA));

        Assert.Empty(result);
    }

    [Fact]
    public void IncompatibleEventTypeExcludesRule()
    {
        var incompatible = Handler(typeof(C), typeof(UnrelatedEvent));
        var compatible = Handler(typeof(C), typeof(EventA));

        var result = Ranked([incompatible, compatible], typeof(C), typeof(EventA));

        Assert.Single(result);
        Assert.Equal(compatible, result[0]);
    }

    [Fact]
    public void UnrelatedStateTypeIsExcluded()
    {
        var unrelated = Handler(typeof(UnrelatedState), typeof(EventA));
        var compatible = Handler(typeof(C), typeof(EventA));

        var result = Ranked([unrelated, compatible], typeof(C), typeof(EventA));

        Assert.Single(result);
        Assert.Equal(compatible, result[0]);
    }

    [Fact]
    public void NoMatchingRulesReturnsEmptyArray()
    {
        var unrelated = Handler(typeof(UnrelatedState), typeof(EventA));

        var result = Ranked([unrelated], typeof(C), typeof(EventA));

        Assert.Empty(result);
    }

    [Fact]
    public void FullSortProducesCorrectOrder()
    {
        // All handlers fire against actual type C, event EventA.
        //
        // h0: B  (class, sd=1), EventA (class, ed=0), guarded,   order=0 → key (1,0, 0,0, 0, 0)
        // h1: C  (class, sd=0), EventA (class, ed=0), unguarded, order=1 → key (0,0, 0,0, 1, 1)
        // h2: C  (class, sd=0), EventA (class, ed=0), guarded,   order=2 → key (0,0, 0,0, 0, 2)
        // h3: ILeaf (iface,sd=0), EventA (class, ed=0), guarded, order=3 → key (0,1, 0,0, 0, 3)
        //
        // Ascending sort → priority order: h2, h1, h3, h0
        var h0 = Handler(typeof(B), typeof(EventA), hasGuard: true, order: 0);
        var h1 = Handler(typeof(C), typeof(EventA), hasGuard: false, order: 1);
        var h2 = Handler(typeof(C), typeof(EventA), hasGuard: true, order: 2);
        var h3 = Handler(typeof(ILeaf), typeof(EventA), hasGuard: true, order: 3);

        var result = Ranked([h0, h1, h2, h3], typeof(C), typeof(EventA));

        Assert.Equal(h2, result[0]);
        Assert.Equal(h1, result[1]);
        Assert.Equal(h3, result[2]);
        Assert.Equal(h0, result[3]);
    }
}
