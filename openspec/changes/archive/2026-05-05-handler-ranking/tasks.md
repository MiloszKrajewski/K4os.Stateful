## 1. TransitionRule

- [x] 1.1 Create `Internal/TransitionRule.cs` with `StateType`, `EventType`, `HasGuard`, `DeclarationOrder` fields and constructor
- [x] 1.2 Add comment noting Layer 4 (DSL) will extend this class with `Guard` and `Action` callback fields

## 2. TransitionRuleRanker

- [x] 2.1 Create `Internal/TransitionRuleRanker.cs` with private `RuleSortKey` struct (six fields: stateDistance, stateIsInterface, eventDistance, eventIsInterface, hasNoGuard, declarationOrder)
- [x] 2.2 Implement `RuleSortKey.TryCreate` — calls `DistanceFrom` on both axes, returns `null` if either is null
- [x] 2.3 Implement `RuleSortKey.CompareTo` — ascending comparison across all six fields
- [x] 2.4 Implement `TransitionRuleRanker.RankedCandidates(rules, actualStateType, actualEventType) → TransitionRule[]` — build candidate list, sort, return

## 3. MachineDefinition

- [x] 3.1 Create `MachineDefinition<TContext, TState, TEvent>` with `internal` constructor taking `IReadOnlyList<TransitionRule>`
- [x] 3.2 Add `ConcurrentDictionary<(Type, Type), TransitionRule[]>` cache field
- [x] 3.3 Implement `internal GetRankedHandlers(actualStateType, actualEventType)` using `ConcurrentDictionary.GetOrAdd` with static factory (no closure allocation)

## 4. Tests

- [x] 4.1 Create `TransitionRuleRankerTests.cs` — type hierarchy fixtures (state: `A : IBase`, `B : A, IMid`, `C : B, ILeaf`; event: `EventA : IEventBase`, `EventB : EventA`)
- [x] 4.2 Test: concrete state type ranks before base type (`ConcreteTypeRuleRanksBeforeBaseTypeRule`)
- [x] 4.3 Test: multi-level state hierarchy ordered by distance (`MultiLevelHierarchyRankedByIncreasingDistance`)
- [x] 4.4 Test: class registration ranks before interface at same state distance (`ClassRuleRanksBeforeInterfaceRuleAtSameStateDistance`)
- [x] 4.5 Test: interface at closer distance beats class at farther distance (`InterfaceAtDeeperLevelRanksBeforeClassAtShallowerLevel`)
- [x] 4.6 Test: specific event ranks before base event at same state distance (`SpecificEventRanksBeforeBaseEventAtSameStateDistance`)
- [x] 4.7 Test: class event registration ranks before interface event at same event distance (`ClassEventRuleRanksBeforeInterfaceEventRuleAtSameEventDistance`)
- [x] 4.8 Test: guarded rule ranks before unguarded at same distance and kind (`GuardedRuleRanksBeforeUnguardedAtSameDistanceAndKind`)
- [x] 4.9 Test: unguarded class beats guarded interface at same distance (`UnguardedClassRuleRanksBeforeGuardedInterfaceAtSameStateDistance`)
- [x] 4.10 Test: declaration order breaks ties (`DeclarationOrderBreaksTiesAmongEquallyRankedRules`)
- [x] 4.11 Test: rule registered for subtype does not match actual base type (`RuleRegisteredForSubtypeDoesNotMatchActualBaseType`)
- [x] 4.12 Test: incompatible event type excluded (`IncompatibleEventTypeExcludesRule`)
- [x] 4.13 Test: unrelated state type excluded (`UnrelatedStateTypeIsExcluded`)
- [x] 4.14 Test: no matching rules returns empty array (`NoMatchingRulesReturnsEmptyArray`)
- [x] 4.15 Test: full combined sort produces correct order (`FullSortProducesCorrectOrder`) — uses the example from `docs/rule-rank.md`
