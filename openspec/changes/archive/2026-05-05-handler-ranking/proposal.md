## Why

The executor needs to determine which registered transition rule fires when an event arrives in a given state. This requires a ranking algorithm that resolves polymorphic matches (rules registered on base types/interfaces) into a deterministic, priority-ordered candidate list. Without it, no event dispatch is possible.

## What Changes

- Introduces `TransitionRule` — the unit of registration: state type, event type, guard flag, and declaration order. Callbacks (guard/action lambdas) will be added in a later layer (DSL/Layer 4); this layer defines the skeleton.
- Introduces `HandlerRanker` — a static utility that, given a flat list of rules and the actual runtime `(stateType, eventType)` pair, returns a priority-ordered `TransitionRule[]`. Rules incompatible with the actual types are excluded. Sort key is six fields: `stateDistance, stateIsInterface, eventDistance, eventIsInterface, hasNoGuard, declarationOrder`.
- Introduces `MachineDefinition<TContext, TState, TEvent>` — the frozen snapshot produced by `Build()`. Holds the rule array and a `ConcurrentDictionary` cache that maps `(actualStateType, actualEventType)` to their ranked handler array, filled lazily on first encounter and reused across all executors of that definition.

## Capabilities

### New Capabilities

- `handler-ranking`: Rule registration metadata (`TransitionRule`), six-field priority sort (`HandlerRanker`), and lazy ranked-handler cache in the machine definition (`MachineDefinition`).

### Modified Capabilities

*(none)*

## Impact

- New files in `K4os.Stateful` project: `Internal/TransitionRule.cs`, `Internal/HandlerRanker.cs`, `MachineDefinition.cs`.
- Depends on `type-distance` (Layer 1 — `TypeExtensions.DistanceFrom`).
- `MachineDefinition<TContext, TState, TEvent>` is the shared, frozen object future layers (DSL, executor) will build on; its constructor is `internal` until Layer 4 wires up `Build()`.
- No breaking changes — all new types.
