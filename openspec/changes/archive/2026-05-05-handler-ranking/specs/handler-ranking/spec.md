## ADDED Requirements

### Requirement: TransitionRule holds rule metadata
`TransitionRule` is a class that captures the four fields needed to rank a registered transition rule: the registered state type, the registered event type, a flag indicating whether a guard predicate is present, and a declaration order index assigned by the configurator. It carries no callbacks at this layer; guard and action delegates are added in the DSL layer.

#### Scenario: Fields are preserved after construction
- **WHEN** a `TransitionRule` is constructed with `(stateType, eventType, hasGuard, declarationOrder)`
- **THEN** `StateType`, `EventType`, `HasGuard`, and `DeclarationOrder` return the values passed to the constructor

---

### Requirement: HandlerRanker excludes type-incompatible rules
`HandlerRanker.RankedCandidates` SHALL exclude any rule where `actualStateType.DistanceFrom(rule.StateType)` or `actualEventType.DistanceFrom(rule.EventType)` returns `null` (the registered type is not an ancestor of the actual type).

#### Scenario: Rule with unrelated state type is excluded
- **WHEN** a rule is registered for a state type unrelated to the actual state type
- **THEN** the rule does not appear in the ranked result

#### Scenario: Rule registered for a subtype does not match an actual base type
- **WHEN** a rule is registered for `ChildState` and the actual state is `ParentState`
- **THEN** the rule does not appear in the ranked result

#### Scenario: Rule with unrelated event type is excluded
- **WHEN** a rule is registered for an event type unrelated to the actual event type
- **THEN** the rule does not appear in the ranked result

#### Scenario: No compatible rules returns empty array
- **WHEN** no registered rule is type-compatible with the actual `(stateType, eventType)` pair
- **THEN** `RankedCandidates` returns an empty array

---

### Requirement: HandlerRanker ranks by state specificity first
Rules with a smaller state distance (closer to the actual state type) SHALL rank higher than rules with a larger state distance. At equal state distance, a rule registered on a class SHALL rank higher than a rule registered on an interface.

#### Scenario: Concrete state type ranks before base class
- **WHEN** rules are registered for both `ConcreteState` (distance 0) and `BaseState` (distance 1)
- **THEN** the `ConcreteState` rule appears first in the ranked result

#### Scenario: Multi-level hierarchy ranked by increasing distance
- **WHEN** rules are registered for types at distances 0, 1, and 2 from the actual state
- **THEN** the ranked result is ordered distance 0, 1, 2

#### Scenario: Class registration ranks before interface at same state distance
- **WHEN** a rule is registered for `ConcreteState` (class, distance 0) and another for `IStateInterface` introduced by `ConcreteState` (interface, distance 0)
- **THEN** the class-registered rule appears first

#### Scenario: Interface at closer distance ranks before class at farther distance
- **WHEN** a rule is registered for an interface at distance 0 and a class at distance 2
- **THEN** the interface rule appears first (distance dominates class/interface)

---

### Requirement: HandlerRanker ranks by event specificity second
At equal state specificity, rules with a smaller event distance SHALL rank higher. At equal event distance, a rule registered on an event class SHALL rank higher than one registered on an event interface.

#### Scenario: Specific event ranks before base event at same state distance
- **WHEN** rules are registered for both `ConcreteEvent` (distance 0) and `BaseEvent` (distance 1) against the same state type
- **THEN** the `ConcreteEvent` rule appears first

#### Scenario: Class event registration ranks before interface event at same event distance
- **WHEN** a rule is registered for `ConcreteEvent` (class, event distance 0) and another for `IEventInterface` introduced at `ConcreteEvent` (interface, event distance 0)
- **THEN** the class-registered rule appears first

---

### Requirement: HandlerRanker ranks guarded rules before unguarded at same specificity
At equal state and event specificity (distance and class/interface), a rule with `HasGuard = true` SHALL rank higher than a rule with `HasGuard = false`. Specificity takes precedence: an unguarded concrete-class rule ranks higher than a guarded interface rule at the same distance.

#### Scenario: Guarded rule ranks before unguarded at same distance and kind
- **WHEN** two rules share state type, event type, and declaration order but differ in `HasGuard`
- **THEN** the guarded rule appears first

#### Scenario: Unguarded class rule ranks before guarded interface at same distance
- **WHEN** an unguarded rule is registered on a class and a guarded rule on an interface at the same state distance
- **THEN** the unguarded class rule appears first (class/interface specificity outweighs guard)

---

### Requirement: HandlerRanker uses declaration order as stable tie-break
When all other sort fields are equal, the rule with the smaller `DeclarationOrder` value SHALL appear first. This ensures deterministic ordering regardless of the input sequence.

#### Scenario: Earlier declaration order ranks first among otherwise equal rules
- **WHEN** multiple rules are identical except for `DeclarationOrder`
- **THEN** the ranked result is ordered by ascending `DeclarationOrder` regardless of input order

---

### Requirement: MachineDefinition holds a frozen rule array and a lazy ranked-handler cache
`MachineDefinition<TContext, TState, TEvent>` SHALL store an immutable copy of the rules provided at construction. `GetRankedHandlers(actualStateType, actualEventType)` SHALL return the ranked `TransitionRule[]` for the given type pair, computing it on first call and returning the cached result on all subsequent calls.

#### Scenario: GetRankedHandlers returns ranked result for a type pair
- **WHEN** `GetRankedHandlers` is called with a valid `(actualStateType, actualEventType)` pair
- **THEN** it returns the same ordered array as `HandlerRanker.RankedCandidates` for that pair

#### Scenario: GetRankedHandlers caches results per type pair
- **WHEN** `GetRankedHandlers` is called twice with the same `(actualStateType, actualEventType)` pair
- **THEN** both calls return the same array reference (the result was cached after the first call)

#### Scenario: GetRankedHandlers is thread-safe
- **WHEN** multiple threads call `GetRankedHandlers` concurrently with the same or different type pairs
- **THEN** each call returns a correct ranked array and no exceptions are thrown
