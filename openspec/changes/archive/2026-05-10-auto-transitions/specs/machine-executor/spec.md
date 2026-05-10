## MODIFIED Requirements

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
