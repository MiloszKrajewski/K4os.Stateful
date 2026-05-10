## 1. Configurator Changes

- [x] 1.1 Add `WithStateChangeIf(Func<TState, TState, bool>)` to `IMachineConfig` returning `IMachineConfig`; store predicate in `MachineConfigBuilder`
- [x] 1.2 Pass predicate (or `!ReferenceEquals` default) into `MachineDefinition` constructor from `Build()`

## 2. MachineDefinition Changes

- [x] 2.1 Add `StateChangePredicate` property (`Func<TState, TState, bool>`) to `MachineDefinition`
- [x] 2.2 Add `Create()` method to `MachineDefinition` returning a new `MachineExecutor<TContext, TState, TEvent>`

## 3. Domain Exceptions

- [x] 3.1 Create `UnhandledEventException` (inherits `Exception`; carries event object and state type)
- [x] 3.2 Create `ConcurrentFireException` (inherits `Exception`)

## 4. MachineExecutor Core

- [x] 4.1 Create `MachineExecutor<TContext, TState, TEvent>` with `_state`, `_context`, `_definition`, `_firing` fields
- [x] 4.2 Implement `Start(TContext context, TState state)` — assigns fields, no entry/exit
- [x] 4.3 Implement `State` property returning current `_state`
- [x] 4.4 Implement guard walk: iterate ranked handlers, evaluate guard, short-circuit on first pass; return null on no match
- [x] 4.5 Implement entry/exit firing: separate enter/exit caches with correct sort orders; entry iterates far→close (interface before class), exit iterates close→far (class before interface)
- [x] 4.6 Implement `FireAsync(TEvent @event, CancellationToken ct)`: concurrent check → guard walk → action → predicate → entry/exit; throw `UnhandledEventException` on no match
- [x] 4.7 Implement `TryFireAsync(TEvent @event, CancellationToken ct)`: same as `FireAsync` but return `false` instead of throwing on no match
- [x] 4.8 Implement `Fire` and `TryFire` sync wrappers via `.GetAwaiter().GetResult()`
- [x] 4.9 Wrap firing loop in try/finally to clear `_firing` flag even on exception

## 5. Unit Tests — Executor Lifecycle (Layer 9)

- [x] 5.1 `Start` sets `State` without firing `OnEnter`
- [x] 5.2 `State` returns correct value after `Start` and after `FireAsync`
- [x] 5.3 Two executors from same definition are independent (fire one, other is unaffected)

## 6. Unit Tests — Entry/Exit Firing Order (Layer 5)

- [x] 6.1 `OnEnter` fires base→derived on transition in
- [x] 6.2 `OnExit` fires derived→base on transition out
- [x] 6.3 `OnExit` completes fully before `OnEnter` starts
- [x] 6.4 Handlers registered on an interface fire at the correct rank
- [x] 6.5 Levels with no registered handler are silently skipped
- [x] 6.6 Multiple `OnEnter` registrations for the same type all fire in declaration order

## 7. Unit Tests — State-Change Predicate (Layer 6)

- [x] 7.1 Default predicate: `Stay()` (same ref) → no entry/exit
- [x] 7.2 Default predicate: `GoTo` returning new instance → entry/exit fires
- [x] 7.3 Default predicate: `GoTo` returning same type but new instance → entry/exit fires
- [x] 7.4 Custom always-true predicate: entry/exit fires even for Stay-equivalent
- [x] 7.5 Custom always-false predicate: entry/exit never fires regardless of state change
- [x] 7.6 Custom type-change predicate: fires only on type change
- [x] 7.7 `Start` does not fire `OnEnter` regardless of predicate

## 8. Unit Tests — Transitions / Guard Walk (Layer 7)

- [x] 8.1 Guarded handler fires when guard passes; second matching handler does NOT fire (short-circuit)
- [x] 8.2 Guard returns false → falls through to next candidate
- [x] 8.3 Unguarded handler acts as fallback when all guarded handlers above it fail
- [x] 8.4 Same-type transition via `with` expression
- [x] 8.5 Cross-type transition (A→B where B ≠ A)
- [x] 8.6 `Stay()` with no callback — silent no-op, state ref unchanged
- [x] 8.7 `Stay(callback)` — callback runs, state ref unchanged, no entry/exit
- [x] 8.8 `FireAsync` awaits completion before returning (async action side-effects complete)
- [x] 8.9 `TryFireAsync` returns `true` when a rule matches

## 9. Unit Tests — Unhandled Events (Layer 8)

- [x] 9.1 `FireAsync` throws `UnhandledEventException` when no handler registered
- [x] 9.2 `FireAsync` throws `UnhandledEventException` when all guards fail
- [x] 9.3 `TryFireAsync` returns `false` when no handler registered
- [x] 9.4 `TryFireAsync` returns `false` when all guards fail
- [x] 9.5 Catch-all rule (`In<TState>().On<TEvent>()` no guard) suppresses `UnhandledEventException`

## 10. Unit Tests — Concurrent Fire Detection (Layer 10 partial)

- [x] 10.1 Two concurrent `FireAsync` calls on same executor — second throws `ConcurrentFireException`
- [x] 10.2 Sequential `FireAsync` calls complete correctly with no exception
