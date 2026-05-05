# .NET State Machine Library — Implementation Plan

> **Status:** In progress
> **Last updated:** 2026-05-05
> **Companion document:** [mechanicus-design.md](mechanicus-design.md) — requirements, design decisions, DSL spec

---

## Overview

The library is built in layers. Each layer is independently testable before the next one is started.
Layers 1–4 have no mutual dependency and can be developed in any order (or in parallel).
Layers 5+ build on the executor and require earlier layers to be complete.

```
Layer 1: Distance algorithm          ─┐
Layer 2: Rule ranking / sort          ├─ pure algorithms, no executor needed
Layer 3: Guard evaluation             │
Layer 4: DSL / builder               ─┘
Layer 5: Entry / Exit firing order
Layer 6: State-change predicate
Layer 7: Transitions (happy paths)
Layer 8: Unhandled events
Layer 9: Executor lifecycle
Layer 10: Thread safety
Layer 11: Error propagation
Layer 12: CancellationToken
Layer 13: Nested states (data pattern)
```

---

## Sub-tasks

### Layer 1 — Distance Algorithm

**Design reference:** [mechanicus-design.md § Distance algorithm](mechanicus-design.md#distance-algorithm-classes--interfaces)

**What it is:**
A pure function that, given an actual runtime type and a registered handler type, returns an integer
distance representing how specific the match is. This is the foundation for both rule ranking and
entry/exit firing order.

**Rules (from spec):**
- Actual state type → distance **0**
- Each class step up the inheritance chain → `+1` per level
- An interface → same distance as the first class in the chain that *introduces* it
- Within the same distance, class is considered closer than interface

**Inputs / outputs:**
```
distance(actualType: Type, registeredType: Type) → int
```

**Test concepts:**
- Concrete type matches itself → 0
- Direct base class → 1; grandparent → 2
- Interface introduced by the actual type → 0 (same rank as type itself)
- Interface introduced by a base class → same rank as that base class
- Multiple interfaces on one class — all at the same rank as their introducing class
- Unrelated type (not in chain) → not applicable / excluded from candidates
- Multi-level hierarchy: A → B → C — correct distance at each level

---

### Layer 2 — Event Handler Ranking / Sort

**Design reference:** [mechanicus-design.md § Event handler matching & ranking](mechanicus-design.md#event-handler-matching--ranking)

**What it is:**
Event handlers are sorted by a four-part composite key before evaluation. The sort order determines
which handler is tried first when multiple handlers could match a given `(state, event)` pair.

**Sort key (from spec):**

| Key | Direction | Meaning |
|-----|-----------|---------|
| `distance` | ASC (smaller = more specific) | concrete type beats base type |
| `class / interface` | class first | class beats interface at same distance |
| `hasGuard` | guarded first | conditional handler beats unconditional fallback |
| `declarationOrder` | ASC (earlier first) | stable tie-break |

**Test concepts:**
- Concrete-type handler sorts before base-type handler
- Class handler sorts before interface handler at same distance
- Guarded handler sorts before unguarded at same distance and class/interface rank
- Declaration order resolves remaining ties stably
- Full four-key sort: mixed handlers produce the correct ordered list

---

### Layer 3 — Guard Evaluation

**Design reference:** [mechanicus-design.md § Event handler matching & ranking](mechanicus-design.md#event-handler-matching--ranking), [§ .When — single lambda shape](mechanicus-design.md#when--single-lambda-shape)

**What it is:**
Walking the sorted handler list and short-circuiting at the first handler whose guard passes.
Depends on Layer 1 (distance) and Layer 2 (sort) to produce the ordered candidate list.

**Test concepts:**
- First passing guard fires; second matching handler does NOT fire (short-circuit)
- Guard returns `false` → falls through to the next candidate
- All guards fail → caller receives "no match" signal (throws or returns false)
- Unguarded handler acts as fallback (fires when all guarded handlers above it fail)
- Guard throws → exception propagates immediately; no state change, no further handlers evaluated
- Async guard — `ValueTask<bool>` is awaited correctly

---

### Layer 4 — DSL / Builder

**Design reference:** [mechanicus-design.md § Fluent chain — type threading](mechanicus-design.md#fluent-chain--type-threading), [§ Facet interfaces](mechanicus-design.md#facet-interfaces--one-class-multiple-visible-surfaces), [§ Configuration DSL (revised)](mechanicus-design.md#configuration-dsl-revised), [§ Handler bundle — Activation](mechanicus-design.md#handler-bundle--activationtcontext-tcurrentstate-tcurrentevent)

**What it is:**
The fluent configuration chain. Tests here verify that calling the DSL methods registers the expected
handlers in the internal handler tables — not that those handlers execute correctly (that is Layer 7+).

**Key types:**
- `EventHandler<TContext, TState, TEvent>` — frozen event handler (state type, event type, guard, action)
- `EventHandlerConfig<TContext, TState, TEvent>` — mutable twin; accumulated across `In→On→When→GoTo/Stay`
- `StateHandler<TContext, TState>` — frozen state lifecycle handler (state type, OnEnter, OnExit)
- `StateHandlerConfig<TContext, TState, TCurrentState>` — mutable twin; accumulated across `In→OnEnter/OnExit`
- `Activation<TContext, TCurrentState, TCurrentEvent>` — lambda bundle for `When` / `GoTo` / `Stay`
- `Activation<TContext, TCurrentState>` — lambda bundle for `OnEnter` / `OnExit` / `Auto`

**Key interfaces (from spec):**
- `IMachineConfig` — entry point; exposes `In<T>()` and `Build()`
- `IStateConfig<TCurrentState>` — returned by `In<T>()`, `GoTo()`, `Stay()`
- `IEventConfig<TState, TEvent>` — returned by `On<T>()`
- `IGuardedEventConfig<TState, TEvent>` — returned by `When()`; no second `.When()` available

**`MachineDefinition` internal structure:**
- `_eventHandlers: EventHandler<TContext, TState, TEvent>[]` — all registered event handlers (frozen)
- `_stateHandlers: StateHandler<TContext, TState>[]` — all registered state handlers (frozen); OnEnter iterates forward (base → derived), OnExit iterates in reverse (derived → base)

**Test concepts:**
- `In<T>().On<E>().GoTo(fn)` registers one `EventHandler` with correct state type, event type, and action
- `In<T>().On<E>().When(guard).GoTo(fn)` registers a guarded `EventHandler`
- `In<T>().On<E>().Stay()` registers a stay `EventHandler`
- `In<T>().On<E>().Stay(callback)` registers a stay `EventHandler` with a side-effect callback
- `In<T>().OnEnter(fn)` registers a `StateHandler` with an entry callback
- `In<T>().OnExit(fn)` registers a `StateHandler` with an exit callback
- Chained style and disconnected style register identical handlers
- `Build()` freezes the definition; the returned `MachineDefinition<C,S,E>` is immutable
- Extension method overloads (sync, `Task<>`) are accepted and wrapped correctly
- `.When().When()` is a compile-time error (only `IGuardedEventConfig` returned, which has no `.When()`)

---

### Layer 5 — Entry / Exit Firing Order

**Design reference:** [mechanicus-design.md § Entry / Exit — hierarchical firing order](mechanicus-design.md#entry--exit--hierarchical-firing-order)

**What it is:**
When a state transition is detected, entry and exit handlers fire for every type in the actual state's
inheritance chain that has a registered handler. Unlike rules (one fires), entry/exit handlers **all fire**.

**Firing order (from spec):**
- **Entry**: base → derived (descending distance order — far first)
- **Exit**: derived → base (ascending distance order — close first)
- Within the same rank: interfaces fire before the class at that rank; secondary sort is declaration order

**Test concepts:**
- Entry fires base → derived order on transition in
- Exit fires derived → base order on transition out
- Only handlers registered for types actually in the chain fire
- Multiple `OnEnter` registrations for the same type all fire, in declaration order
- `OnExit` completes fully before `OnEnter` starts
- Handlers registered on an interface fire at the correct rank (rank of introducing class)
- No handler registered at a level → that level is silently skipped

---

### Layer 6 — State-Change Predicate

**Design reference:** [mechanicus-design.md § D1 — What triggers entry/exit actions?](mechanicus-design.md#d1--what-triggers-entryexit-actions--resolved)

**What it is:**
Entry/exit handlers only fire when the state-change predicate returns `true` for
`(previousState, nextState)`. The default is `!ReferenceEquals(s1, s2)`.

**Test concepts:**
- Default `!ReferenceEquals`: `Stay()` returns same ref → no entry/exit
- Default: `GoTo(x => x.State with { })` returns new instance → fires entry/exit
- Default: `GoTo(x => new OtherState())` → fires entry/exit
- Custom ETag-based predicate: only fires when ETag differs
- Custom value-equality predicate: fires on any property change
- Custom always-true predicate: fires even when `Stay()` returns same ref
- Custom always-false predicate: never fires entry/exit regardless of state change
- `executor.Start()` does NOT fire `OnEnter` regardless of predicate

---

### Layer 7 — Transitions (Happy Paths)

**Design reference:** [mechanicus-design.md § .GoTo — async, sync, and state-only overloads](mechanicus-design.md#goto--async-sync-and-state-only-overloads), [§ Firing events](mechanicus-design.md#firing-events)

**What it is:**
The full `FireAsync`/`TryFireAsync` execution path: find the matching rule (Layers 1–3), execute the
handler, apply the new state, evaluate the predicate (Layer 6), run entry/exit (Layer 5).

**Test concepts:**
- Same-type transition via `with` expression
- Cross-type transition (A → B where B ≠ A)
- `GoTo` returning a base-type value is accepted
- `Stay()` with no callback — silent no-op; state reference unchanged
- `Stay(callback)` — callback runs; state reference unchanged; no entry/exit
- Async `GoTo` — side effects complete before state is updated
- Sync `GoTo` extension overload — wraps and executes correctly
- State-only `GoTo(s => ...)` extension — receives correct concrete type
- `FireAsync` awaits completion before returning
- `TryFireAsync` returns `true` when a rule matches

---

### Layer 8 — Unhandled Events

**Design reference:** [mechanicus-design.md § R6 — Invalid transition handling](mechanicus-design.md#r6--invalid-transition-handling), [§ Unhandled events](mechanicus-design.md#settled-design-notes)

**What it is:**
Behaviour when no rule matches for the given `(state, event)` pair (either no rule exists, or all
guards failed).

**Test concepts:**
- `Fire`/`FireAsync` throws `UnhandledEventException` when no rule matches
- `TryFire`/`TryFireAsync` returns `false` when no rule matches
- `Fire` throws when rules exist but all guards return `false`
- A catch-all rule (`In<TState>().On<TEvent>()` with no guard) suppresses the exception

---

### Layer 9 — Executor Lifecycle

**Design reference:** [mechanicus-design.md § Executor — lifecycle and state portability](mechanicus-design.md#executor--lifecycle-and-state-portability)

**What it is:**
Creation, startup, and state access for executor instances.

**Test concepts:**
- `Start()` sets `executor.State` without firing `OnEnter`
- `executor.State` returns the correct state after `Start()`
- `executor.State` returns the correct state after `FireAsync()`
- Multiple executors created from the same frozen definition are fully independent
- Restoring from a deserialized state object behaves identically to a fresh start

---

### Layer 10 — Thread Safety

**Design reference:** [mechanicus-design.md § Thread safety](mechanicus-design.md#settled-design-notes)

**What it is:**
Concurrent `FireAsync` calls on the same executor are detected and rejected.
The executor is single-threaded by contract; the caller is responsible for serialising access.

**Test concepts:**
- Two concurrent `FireAsync` calls on the same executor → one throws `ConcurrentFireException`
- Sequential fires on the same executor complete correctly with no errors

---

### Layer 11 — Error Propagation

**Design reference:** [mechanicus-design.md § Error propagation](mechanicus-design.md#settled-design-notes)

**What it is:**
Exceptions in any handler propagate immediately. State must NOT change if an exception occurs
during the transition.

**Test concepts:**
- Exception in `GoTo` → propagates to caller; `executor.State` is unchanged
- Exception in `.When` guard → propagates; state unchanged
- Exception in `OnExit` → propagates; `OnEnter` does NOT run
- Exception in `OnEnter` → propagates
- After a failed `FireAsync`, the executor remains usable (state is still valid)

---

### Layer 12 — CancellationToken

**Design reference:** [mechanicus-design.md § Handler bundle — Activation](mechanicus-design.md#handler-bundle--activationtcontext-tcurrentstate-tcurrentevent)

**What it is:**
The `CancellationToken` passed to `FireAsync(event, ct)` must flow through to `x.CancellationToken`
in every lambda: `.When`, `.GoTo`, `.Stay`, `OnEnter`, `OnExit`.

**Test concepts:**
- Token is available as `x.CancellationToken` in a `GoTo` lambda
- Token is available in `.When`, `.Stay`, `OnEnter`, `OnExit` lambdas
- Cancellation raised inside `GoTo` propagates as `OperationCanceledException`; state unchanged
- `CancellationToken.None` works as the no-cancellation default

---

### Layer 13 — Nested States (Data Pattern)

**Design reference:** [mechanicus-design.md § Covered differently — not a gap](mechanicus-design.md#covered-differently--not-a-gap) (`.SubstateOf()` row)

**What it is:**
Nesting is a **data pattern**, not a library feature. A nested state carries a `Parent` field of type
`TState`; returning to the parent is `GoTo(x => x.State.Parent)`. The whole stack serializes as
nested JSON automatically.

**Test concepts:**
- `GoTo(x => x.State.Parent)` returns to the parent state correctly
- Entry/exit fires for the parent state on return from nested state
- Nested state (including `Parent` reference) serializes and deserializes as plain JSON
- Restoring from a serialized nested state and firing events works correctly

---

### Layer 14 — Auto Transitions (Completion Transitions)

**Design reference:** [mechanicus-design.md § .Auto() — completion transitions](mechanicus-design.md#auto--completion-transitions)

**What it is:**
A state can declare an automatic transition that fires after `OnEnter` completes, without any external
event. The executor chains through Auto transitions internally before returning to the caller.
This enables logical routing states and multi-step initialization pipelines with no external triggers.

**Depends on:** Layer 7 (transitions), Layer 5 (entry/exit firing)

**Test concepts:**
- `Auto` handler fires after `OnEnter` completes
- `Start()` does NOT trigger `Auto` (same reasoning as `OnEnter`)
- Returning same reference → no-op (predicate suppresses transition)
- Returning different state → full transition fires (OnExit → OnEnter → Auto check continues)
- Chain of Auto states — final state reached before `FireAsync` returns to caller
- Auto on a state with no further Auto — stops cleanly
- Cancellation token flows through to `Auto` lambda
- Exception in `Auto` propagates; state is left at the state that triggered the Auto

> ⚠️ **Infinite loop risk** is acknowledged but not guarded against in this iteration.
> A future `WithMaxAutoDepth(n)` option is noted for later consideration.

---

## Implementation Order

```
Start here:  Layer 1 (distance) + Layer 4 (DSL) — independent, lay the foundation
Then:        Layer 2 (ranking) + Layer 3 (guards) — depends on Layer 1
Then:        Layer 5 (entry/exit order) — depends on Layer 1
Then:        Layer 6 (predicate) — depends on Layer 5
Then:        Layer 7 (transitions) — all of the above
Then:        Layers 8–13 in any order — all depend on the executor being functional
Then:        Layer 14 (Auto) — depends on Layer 7 and Layer 5
```
