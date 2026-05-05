## RENAMED Requirements

### Requirement: TransitionRule holds rule metadata
FROM: `TransitionRule holds rule metadata`
TO: `EventHandler holds handler metadata`

---

## MODIFIED Requirements

### Requirement: EventHandler holds handler metadata
`EventHandler` is a non-generic base class that captures the four fields needed to rank a
registered event handler: the registered state type, the registered event type, a flag indicating
whether a guard predicate is present, and a declaration order index assigned by the configurator.

`EventHandler<TContext, TState, TEvent>` is a generic sealed subclass that additionally carries
the typed delegate fields: `Guard` (`Func<Activation<TContext, TState, TEvent>, ValueTask<bool>>?`)
and `Action` (`Func<Activation<TContext, TState, TEvent>, ValueTask<TState>>`). These are set once
in the constructor and are readonly.

`EventHandlerRanker` (formerly `TransitionRuleRanker`) operates on the non-generic `EventHandler`
base class — the ranking algorithm is unchanged.

#### Scenario: Fields are preserved after construction
- **WHEN** an `EventHandler` is constructed with `(stateType, eventType, hasGuard, declarationOrder)`
- **THEN** `StateType`, `EventType`, `HasGuard`, and `DeclarationOrder` return the values passed to the constructor

#### Scenario: Generic subclass carries typed delegates
- **WHEN** an `EventHandler<C,S,E>` is constructed with guard and action delegates
- **THEN** `Guard` and `Action` return the delegates passed to the constructor, and are readonly

---

### Requirement: MachineDefinition holds a frozen handler array and a lazy ranked-handler cache
`MachineDefinition<TContext, TState, TEvent>` SHALL store an immutable copy of the
`EventHandler<TContext, TState, TEvent>[]` provided at construction. It SHALL also store an
immutable `StateHandler<TContext, TState>[]` for lifecycle callbacks.

`GetRankedHandlers(actualStateType, actualEventType)` SHALL return the ranked
`EventHandler<TContext, TState, TEvent>[]` for the given type pair, computing it on first call
and returning the cached result on all subsequent calls.

#### Scenario: GetRankedHandlers returns ranked result for a type pair
- **WHEN** `GetRankedHandlers` is called with a valid `(actualStateType, actualEventType)` pair
- **THEN** it returns the same ordered array as `EventHandlerRanker.RankedCandidates` for that pair

#### Scenario: GetRankedHandlers caches results per type pair
- **WHEN** `GetRankedHandlers` is called twice with the same `(actualStateType, actualEventType)` pair
- **THEN** both calls return the same array reference (the result was cached after the first call)

#### Scenario: GetRankedHandlers is thread-safe
- **WHEN** multiple threads call `GetRankedHandlers` concurrently with the same or different type pairs
- **THEN** each call returns a correct ranked array and no exceptions are thrown
