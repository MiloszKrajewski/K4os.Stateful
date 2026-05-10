# Spec: Machine Executor

## Purpose

Defines the runtime execution model for a state machine. A `MachineExecutor` is created from a
frozen `MachineDefinition`, holds mutable per-instance state, and processes events by resolving
ranked handlers, evaluating guards, invoking transition actions, and firing lifecycle callbacks.

## Requirements

### Requirement: Executor is created from a frozen MachineDefinition

The system SHALL provide `MachineDefinition<C,S,E>.Create()` returning a new, independent
`MachineExecutor<C,S,E>` instance. Each call to `Create()` MUST return a distinct executor with
its own state; the shared `MachineDefinition` MUST NOT be mutated.

#### Scenario: Create returns independent executor instances
- **WHEN** `definition.Create()` is called twice
- **THEN** two distinct executor instances are returned that do not share mutable state

---

### Requirement: Start positions the executor without firing OnEnter

`MachineExecutor.Start(context, state)` SHALL set the executor's current context and state.
It MUST NOT invoke any `OnEnter` or `OnExit` handlers. It SHALL be callable once before any
`FireAsync` call.

#### Scenario: Start sets State without firing OnEnter
- **WHEN** `executor.Start(ctx, new ClosedState())` is called
- **THEN** `executor.State` returns the `ClosedState` instance and no `OnEnter` callback fires

#### Scenario: State property returns current state after Start
- **WHEN** `executor.Start(ctx, someState)` is called
- **THEN** `executor.State` returns `someState`

---

### Requirement: FireAsync executes a full transition

`MachineExecutor.FireAsync(event, ct)` SHALL:
1. Settle any pending Auto chain on the current state before dispatching (same logic as `IdleAsync`; this is a no-op when the machine is already in a stable state, and a recovery step when a prior Auto threw)
2. Retrieve ranked event handlers for `(currentState.GetType(), event.GetType())`
3. Walk the list and evaluate each guard; short-circuit on the first handler whose guard passes
4. Invoke the matched handler's action to obtain `nextState`
5. Evaluate the state-change predicate with `(currentState, nextState)`
6. If the predicate returns true: fire all matching `OnExit` handlers (derived→base), update state, fire all matching `OnEnter` handlers (base→derived)
7. If the predicate returns false: update `_state` to `nextState` (same reference for Stay)
8. After OnEnter completes (step 6), check for an `Auto` handler on the newly-entered state (most-derived match wins). If found, invoke it and treat the result as a new transition — repeating steps 5–8 until no Auto handler is found or the predicate returns false
9. Return normally on success

#### Scenario: GoTo transition fires OnExit then OnEnter
- **WHEN** `FireAsync(new OpenEvent())` is called and the matched handler returns a new `OpenState`
- **THEN** `OnExit` fires for the old state, `executor.State` is updated, then `OnEnter` fires for the new state

#### Scenario: Stay does not fire OnExit or OnEnter
- **WHEN** `FireAsync(new KnockEvent())` is called and the matched handler returns the same state reference
- **THEN** neither `OnExit` nor `OnEnter` fires; `executor.State` is unchanged

#### Scenario: Cross-type transition updates state type
- **WHEN** `FireAsync(new OpenEvent())` transitions from `ClosedState` to `OpenState`
- **THEN** `executor.State` is an `OpenState` instance after the call

#### Scenario: State is unchanged when action throws
- **WHEN** the matched handler's action throws an exception
- **THEN** the exception propagates and `executor.State` retains its pre-fire value

#### Scenario: Auto fires after OnEnter and before FireAsync returns
- **WHEN** `FireAsync` causes a transition into a state that has an Auto handler
- **THEN** the Auto handler runs after OnEnter, the resulting transition completes fully, and `FireAsync` returns only after the entire chain settles

---

### Requirement: TryFireAsync returns false on no match instead of throwing

`MachineExecutor.TryFireAsync(event, ct)` SHALL behave identically to `FireAsync` except:
- When no handler matches (no candidates, or all guards fail) it SHALL return `false`
- On success it SHALL return `true`
- It SHALL NOT throw `UnhandledEventException`

#### Scenario: TryFireAsync returns true on successful transition
- **WHEN** a matching handler exists and its guard passes
- **THEN** `TryFireAsync` returns `true` and the transition fires normally

#### Scenario: TryFireAsync returns false when no handler matches
- **WHEN** no registered handler matches the current `(state, event)` pair
- **THEN** `TryFireAsync` returns `false`; state is unchanged

#### Scenario: TryFireAsync returns false when all guards fail
- **WHEN** handlers exist for the pair but all guards return false
- **THEN** `TryFireAsync` returns `false`; state is unchanged

---

### Requirement: FireAsync throws UnhandledEventException on no match

`MachineExecutor.FireAsync(event, ct)` SHALL throw `UnhandledEventException` when no handler
matches (no candidates, or all guards fail). The exception MUST carry the actual event instance
and current state for diagnostics.

#### Scenario: FireAsync throws when no handler registered
- **WHEN** `FireAsync` is called with an event for which no handler is registered
- **THEN** `UnhandledEventException` is thrown; `executor.State` is unchanged

#### Scenario: FireAsync throws when all guards fail
- **WHEN** handlers exist but all guards return false
- **THEN** `UnhandledEventException` is thrown; `executor.State` is unchanged

---

### Requirement: Entry handlers fire base→derived on state entry

When a state transition occurs and the predicate returns true, all `OnEnter` handlers matching
the new state's inheritance chain SHALL fire in base→derived order (most general first,
most specific last). Handlers at the same distance level SHALL fire in declaration order.

#### Scenario: OnEnter fires base before derived
- **WHEN** entering `DeadlockedState : ClosedState` and both `In<ClosedState>().OnEnter(...)` and `In<DeadlockedState>().OnEnter(...)` are registered
- **THEN** the `ClosedState` handler fires first, then the `DeadlockedState` handler

#### Scenario: OnEnter fires for each registered ancestor
- **WHEN** entering a state with three registered ancestors
- **THEN** all three `OnEnter` handlers fire in base→derived order

#### Scenario: OnEnter skips levels with no registered handler
- **WHEN** `In<BaseState>().OnEnter(...)` and `In<ConcreteState>().OnEnter(...)` are registered but not the middle class
- **THEN** only two handlers fire; the unregistered level is silently skipped

---

### Requirement: Exit handlers fire derived→base on state exit

When a state transition occurs and the predicate returns true, all `OnExit` handlers matching the
old state's inheritance chain SHALL fire in derived→base order (most specific first,
most general last). Handlers at the same distance level SHALL fire in declaration order.

#### Scenario: OnExit fires derived before base
- **WHEN** exiting `DeadlockedState : ClosedState` and both exit handlers are registered
- **THEN** the `DeadlockedState` handler fires first, then the `ClosedState` handler

#### Scenario: OnExit completes fully before OnEnter starts
- **WHEN** a transition changes state type
- **THEN** all `OnExit` handlers complete before any `OnEnter` handler fires

---

### Requirement: State-change predicate controls whether entry/exit fires

A configurable predicate `Func<TState, TState, bool>` stored on `MachineDefinition` SHALL
determine whether entry/exit handlers fire after a transition. The default predicate is
`!ReferenceEquals(s1, s2)`.

#### Scenario: Default predicate suppresses entry/exit for Stay
- **WHEN** a handler returns the exact same state reference (Stay semantics)
- **THEN** the default predicate returns false; no entry/exit handlers fire

#### Scenario: Default predicate fires entry/exit for new instance
- **WHEN** a handler returns a new state instance (even of the same type)
- **THEN** the default predicate returns true; entry/exit handlers fire

#### Scenario: Custom predicate overrides default behaviour
- **WHEN** `WithStateChangeIf((s1, s2) => s1.GetType() != s2.GetType())` is configured
- **THEN** entry/exit only fires on type change, not on same-type mutation

#### Scenario: Start does not fire OnEnter regardless of predicate
- **WHEN** `executor.Start(ctx, state)` is called with any predicate configured
- **THEN** no `OnEnter` handler fires

---

### Requirement: Concurrent FireAsync throws ConcurrentFireException

`MachineExecutor` is single-threaded by contract. If `FireAsync` is called while another
`FireAsync` is still executing on the same instance, the second call SHALL throw
`ConcurrentFireException` immediately without touching state. Detection SHALL use
`Interlocked.CompareExchange`.

#### Scenario: Second concurrent FireAsync throws ConcurrentFireException
- **WHEN** `FireAsync` is called while a previous `FireAsync` has not yet completed on the same executor
- **THEN** `ConcurrentFireException` is thrown by the second call; state is unchanged

#### Scenario: Sequential FireAsync calls succeed
- **WHEN** `FireAsync` calls are made sequentially (awaited before the next starts)
- **THEN** all calls complete normally with no exception

---

### Requirement: IdleAsync settles pending Auto transitions

`MachineExecutor.IdleAsync(ct)` (and its sync wrapper `Idle`) SHALL run the Auto chain on the
current state (same as the pre-fire settle step in `FireAsync`) and return. It is a no-op when
the current state has no Auto handler.

Primary use cases:
- After `Create()` with an initial state that has an Auto handler — the machine is positioned at that state without Auto having fired; `IdleAsync()` advances it into a stable state.
- After an Auto exception in a prior `FireAsync` call left the machine in a state with an unresolved Auto handler — `IdleAsync()` (or the next `FireAsync`) resumes the chain.

#### Scenario: IdleAsync is a no-op when current state has no Auto
- **WHEN** `IdleAsync()` is called and the current state has no Auto handler
- **THEN** no entry/exit fires and the state is unchanged

#### Scenario: IdleAsync settles a pending Auto after Create
- **WHEN** `Create(ctx, routerState)` is followed by `await IdleAsync()`
- **THEN** the Auto handler on `routerState` fires, the resulting transition completes, and the executor reaches a stable state

#### Scenario: IdleAsync resumes after a prior Auto exception
- **WHEN** a previous `FireAsync` left the executor at a state with an Auto handler that had thrown, and `IdleAsync()` is called subsequently
- **THEN** the Auto handler is retried and the chain completes (or throws again if still failing)

---

### Requirement: Sync Fire and TryFire convenience wrappers exist

`Fire(event, ct)` and `TryFire(event, ct)` SHALL be synchronous wrappers over `FireAsync` and
`TryFireAsync` respectively, blocking via `.GetAwaiter().GetResult()`.

#### Scenario: Fire completes synchronously
- **WHEN** `executor.Fire(new LockEvent())` is called from synchronous code
- **THEN** the transition completes and `executor.State` is updated before the call returns

#### Scenario: TryFire returns bool synchronously
- **WHEN** `executor.TryFire(new LockEvent())` is called
- **THEN** it returns `true` on match or `false` on no match without throwing
