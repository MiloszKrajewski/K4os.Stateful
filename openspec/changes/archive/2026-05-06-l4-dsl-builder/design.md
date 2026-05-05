## Context

Layers 1 and 2 are complete: `TypeExtensions` computes type distance and `EventHandlerRanker`
(formerly `TransitionRuleRanker`) sorts candidate handlers. `MachineDefinition<C,S,E>` exists as
a frozen cache wrapper but currently holds skeleton `EventHandler` objects with no delegates.

This layer wires up the full DSL: callers write `StateMachine.Define<C,S,E>().In<T>()...Build()`
and get a frozen `MachineDefinition` ready for an executor.

## Goals / Non-Goals

**Goals:**
- Fluent DSL that compiles `In→On→When→GoTo/Stay` and `In→OnEnter/OnExit` into frozen handler objects
- Typed `Activation` bundles so all lambdas receive a single well-typed parameter
- `Build()` produces an immutable `MachineDefinition` — no further mutation possible
- Sync and `Task<>`-returning overloads for all callbacks via extension methods

**Non-Goals:**
- Executor / `FireAsync` — that is Layer 7
- Entry/exit firing order — that is Layer 5
- Guard evaluation — that is Layer 3
- `Auto()` completion transitions — that is Layer 14

## Decisions

### D1 — EventHandler is generic; base class carries ranking metadata

`EventHandler<TContext, TState, TEvent>` inherits from the non-generic `EventHandler` base,
which carries `StateType`, `EventType`, `HasGuard`, `DeclarationOrder`.

**Why:** `EventHandlerRanker` works on the base class — no changes to the ranking algorithm or
its tests. The generic subclass adds typed `Guard` and `Action` delegates. `MachineDefinition`
stores and exposes the generic subclass, so the executor gets fully typed delegates with no casts.

**Alternative considered:** Single generic class, ranker made generic via interface constraint.
Rejected: more churn on existing tests, no benefit.

### D2 — Mutable config twins; frozen at Build()

`EventHandlerConfig<C,S,E>` accumulates data across `In→On→When→GoTo/Stay`. Once `GoTo()` or
`Stay()` is called, all fields are known and the config is converted immediately to a frozen
`EventHandler<C,S,E>` and appended to the builder's list.

`StateHandlerConfig<C,S,TCS>` accumulates `OnEnter` and `OnExit` for one `In<TCS>()` context.
Converted to `StateHandler<C,S>` when `In<>` pivots to another state or when `Build()` is called.

**Why:** Immutable frozen objects guarantee that `MachineDefinition` is safe to share across
concurrent executors with no locking. Mutable state is contained entirely within the builder
phase, which is single-threaded by convention.

### D3 — Callbacks stored at base-type granularity; builder wraps concrete lambdas

All stored delegates use the machine's base types `(TContext, TState, TEvent)` — not the
concrete chain types `(TCS, TCE)`. The builder wraps concrete lambdas at registration time:

```csharp
// User writes:
.GoTo((Activation<C, ClosedState, LockEvent> x) => x.State with { IsLocked = true })

// Builder stores:
(Activation<C, IDoorState, IDoorEvent> x) =>
    ValueTask.FromResult<IDoorState>(
        userFn(new Activation<C, ClosedState, LockEvent>(
            (ClosedState)x.State, (LockEvent)x.Event, x.Context, x.CancellationToken)));
```

**Why:** `MachineDefinition` and the executor work with one concrete type for all delegates —
no generics at dispatch time, no reflection. The wrapping cost is paid once at configuration.

**Alternative considered:** Store as `Delegate` and cast at call site. Rejected: casts everywhere
in the executor, harder to reason about.

### D4 — Single `_stateHandlers[]`; per-actual-state slice is cached; OnExit reverses the slice

`MachineDefinition` holds one `StateHandler<C,S>[]` as the raw store of all registered lifecycle
handlers. When a transition occurs for `actualStateType`, the executor requests the relevant slice
via `GetSortedStateHandlers(actualStateType)`, which filters to handlers whose `StateType` is
assignable from the actual type, sorts them by ascending distance (most derived first), and caches
the result — exactly parallel to `_ruleCache` for event handlers.

OnEnter iterates that cached slice in **reverse** (base → derived = descending distance).
OnExit iterates it **forward** (derived → base = ascending distance).

**Why:** Both enter and exit handlers are registered together per `In<T>()` call, so splitting
into two arrays would duplicate the filtering and sorting concern. A single filtered slice stored
once in the cache, iterated in opposite directions for enter vs exit, is simpler and correct.

### D5 — No `IGuardedEventConfig`; multiple `.When()` calls AND their guards

`On<E>()` returns `IEventConfig` which has `.When()`, `GoTo()`, and `Stay()`. Calling `.When()`
returns the same `IEventConfig<TCS, TCE>` — there is no separate guarded facet. Multiple `.When()`
calls accumulate guards in the `EventHandlerConfig`; all must pass (AND semantics) for the handler
to fire.

**Why:** AND is the only semantically unambiguous combination for chained guards. OR would require
grouping syntax; replacement semantics would be surprising. AND is natural ("fire if this AND that"),
eliminates a whole interface, and keeps the fluent chain uniform. Callers who need OR compose it
inside a single `.When(x => g1(x) || g2(x))`.

### D6 — `GoTo()` and `Stay()` return `IStateConfig<TCurrentState>`, not `void`

After registering a handler, the chain returns to the state facet, enabling:
```csharp
c.In<ClosedState>()
 .On<LockEvent>().GoTo(...)   // ← returns IStateConfig<ClosedState>
 .On<OpenEvent>().GoTo(...)   // ← chains directly, no need for c.In<ClosedState>() again
```

**Why:** Eliminates the need to repeat `c.In<T>()` for each handler on the same state.
Disconnected style (`c.In<T>().On<E>()...` repeated) still works identically.

## Risks / Trade-offs

- **Wrapping overhead (D3):** Each concrete lambda is heap-allocated once at config time into a
  closure. Negligible for state machine configuration which happens once at startup.

- **Two-class hierarchy (D1):** The non-generic base class carries ranking fields; the generic
  subclass adds delegates. This means `EventHandlerRanker` cannot see the delegates — correct by
  design, but requires awareness that ranking and execution use different views of the same object.

- **StateHandlerConfig lifetime (D2):** The mutable config for the current `In<T>()` context must
  be flushed to a frozen `StateHandler` before the builder transitions to a new `In<TOther>()`.
  The builder implementation must track "current state config" carefully.

## Migration Plan

1. Rename `TransitionRule.cs` → `EventHandler.cs`; rename class, add generic subclass
2. Rename `TransitionRuleRanker.cs` → `EventHandlerRanker.cs`; update references
3. Rename `TransitionRuleRankerTests.cs` → `EventHandlerRankerTests.cs`; update helper types
4. Update `MachineDefinitionTests.cs` to use `EventHandler<C,S,E>` with null delegates
5. Add new source files for `Activation`, DSL interfaces, builder classes, extension methods
6. Update `MachineDefinition` to hold `EventHandler<C,S,E>[]` and `StateHandler<C,S>[]`
7. All tests must remain green at each step

## Open Questions

None — all design decisions made during exploration session.
