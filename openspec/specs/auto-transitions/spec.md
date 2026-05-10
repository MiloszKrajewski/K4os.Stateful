# Spec: Auto Transitions

## Purpose

Defines the automatic completion transition capability, where a state can declare an `.Auto(handler)`
that fires immediately after `OnEnter` completes for that state. This enables router/transient states
that immediately redirect into a target state without requiring an explicit event.

## Requirements

### Requirement: Auto registers a completion transition handler

A state MAY declare an automatic completion transition via `.Auto(handler)` on `IStateConfig<TCurrentState>`.
The handler SHALL accept `Activation<TContext, TCurrentState>` (no event) and return `ValueTask<TState>`.
Sync (`TState`) and `Task<TState>` overloads SHALL be provided as interface default implementations.
If `.Auto()` is called more than once for the same state type, the last registration SHALL replace
the previous one (last-wins).

#### Scenario: Auto handler is registered via DSL
- **WHEN** `In<RouterState>().Auto(x => new TargetState())` is called
- **THEN** one `StateHandler` is registered for `RouterState` with a non-null `Auto` delegate

#### Scenario: Multiple Auto calls on same state type — last wins
- **WHEN** `In<S>().Auto(handlerA)` and then `In<S>().Auto(handlerB)` are registered for the same state type
- **THEN** only `handlerB` is invoked when the executor processes Auto for `S`

---

### Requirement: Auto fires after OnEnter completes, not after Create

After `OnEnter` handlers finish for a newly entered state, the executor SHALL check for an
`Auto` handler on that state type (most-derived match wins) and invoke it.
`Create()` SHALL NOT trigger `Auto`, consistent with `Create()` not triggering `OnEnter`.

#### Scenario: Auto fires after OnEnter on a triggered transition
- **WHEN** `FireAsync(event)` causes a transition into a state that has an Auto handler
- **THEN** `OnEnter` fires first, then `Auto` is invoked before `FireAsync` returns

#### Scenario: Create does not trigger Auto
- **WHEN** `definition.Create(ctx, new RouterState())` is called
- **THEN** no `Auto` handler fires; the executor is positioned at `RouterState` without advancing

---

### Requirement: Auto returning a different state triggers a full transition

If the `Auto` handler returns a state for which the state-change predicate returns `true`,
the executor SHALL perform a full transition: `OnExit` (derived→base) for the Auto-source state,
update `_state`, `OnEnter` (base→derived) for the Auto-target state, then check for another
`Auto` handler on the new state. This chain SHALL continue until no Auto handler is found or
the predicate returns `false`.

#### Scenario: Auto causes OnExit then OnEnter for the transition
- **WHEN** the `Auto` handler on `RouterState` returns `new TargetState()`
- **THEN** `OnExit` fires for `RouterState`, state is updated to `TargetState`, `OnEnter` fires for `TargetState`

#### Scenario: Auto chain resolves before FireAsync returns
- **WHEN** `FireAsync` triggers a chain of three Auto transitions: A → B → C → (no Auto)
- **THEN** `executor.State` is `C` and all intermediate `OnExit`/`OnEnter` hooks have fired before `FireAsync` returns

#### Scenario: Auto on final state with no further Auto stops cleanly
- **WHEN** the last state reached by Auto has no registered Auto handler
- **THEN** the Auto loop terminates; `FireAsync` returns normally

---

### Requirement: Auto returning same reference is a no-op

If the `Auto` handler returns a reference for which the state-change predicate returns `false`
(e.g. the same object reference under the default predicate), no transition occurs and the
Auto loop terminates.

#### Scenario: Auto returning same reference stops the chain
- **WHEN** an Auto handler returns `x.State` (exact same reference)
- **THEN** the predicate returns false; no entry/exit fires; `FireAsync` returns with state unchanged

---

### Requirement: Exception in Auto propagates; state is left at the last stable state

If the `Auto` handler throws, the exception SHALL propagate out of `FireAsync`. The executor's
`_state` SHALL remain the state that triggered the failing `Auto` call (i.e. the state entered
by the most recent successful `OnEnter`).

#### Scenario: Exception in Auto propagates to FireAsync caller
- **WHEN** an `Auto` handler throws an exception
- **THEN** `FireAsync` propagates the exception; `executor.State` is the state that entered the Auto, not any partial next state

---

### Requirement: CancellationToken flows through to Auto handlers

The `CancellationToken` passed to `FireAsync(event, ct)` SHALL be available as
`x.CancellationToken` inside every `Auto` lambda in the chain.

#### Scenario: CT is accessible in Auto lambda
- **WHEN** `FireAsync(event, ct)` triggers an Auto handler
- **THEN** `x.CancellationToken` inside the Auto lambda equals the `ct` passed to `FireAsync`
