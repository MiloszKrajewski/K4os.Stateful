## Context

Layers 1–4 are complete and tested: `TypeExtensions.DistanceFrom`, `EventHandlerRanker`, the full
DSL builder chain, and `Activation<>` types. `MachineDefinition<C,S,E>` holds frozen handler arrays
and two lazy caches (`GetEventHandlers`, `GetStateHandlers`). Nothing yet drives the machine at runtime.

The executor is the only remaining piece needed to make the library usable end-to-end for Layers 5–9.
Layers 10–14 (thread safety, error propagation, CancellationToken, nested states, Auto transitions)
are explicitly out of scope here — they layer onto a working executor.

## Goals / Non-Goals

**Goals:**
- Implement `MachineExecutor<TContext,TState,TEvent>` covering lifecycle, transitions, entry/exit, and unhandled events
- Add `WithStateChangeIf` to the configurator and store it in `MachineDefinition`
- Add `MachineDefinition.Create()` as the executor factory
- Full unit test coverage for all executor behaviour

**Non-Goals:**
- Thread safety (Layer 10) — concurrent detection is included but deeper sync not needed here
- Error propagation guarantees (Layer 11) — basic propagation is implicit; explicit state-rollback tests are Layer 11
- CancellationToken flow (Layer 12)
- Nested states (Layer 13)
- Auto transitions (Layer 14)

## Decisions

### D1 — `MachineExecutor` is a plain class, not a struct

An executor is stateful (holds current state, context, in-flight flag) and is meant to live for the
lifetime of a logical entity (session, device, user). A class is correct. Interfaces are not exposed —
callers receive the concrete type from `Create()`.

### D2 — State-change predicate stored on `MachineDefinition`

`WithStateChangeIf` is set once at definition time and shared (read-only) across all executors.
Storing it on `MachineDefinition` is natural — the definition already owns all shared, immutable
executor configuration. Default: `static bool DefaultPredicate(TState s1, TState s2) => !ReferenceEquals(s1, s2)`.

### D3 — Entry/exit order from existing `GetStateHandlers` sort

`MachineDefinition.GetStateHandlers` already returns handlers sorted ascending by distance
(index 0 = most derived). The executor uses this directly:
- **OnExit**: iterate forward (index 0 → N) — derived first
- **OnEnter**: iterate in reverse (index N → 0) — base first

No new sort logic needed; iteration direction is the only distinction.

### D4 — `FireAsync` owns the full transition pipeline atomically

The execution sequence inside `FireAsync` is:

```
1. Set in-flight flag (Interlocked) → ConcurrentFireException if already set
2. Get ranked event handlers from cache
3. Walk list: evaluate guard → short-circuit on first pass
4. No match → UnhandledEventException (or return false for TryFireAsync)
5. Invoke action → get nextState
6. Evaluate state-change predicate(currentState, nextState)
7. If changed: run OnExit (forward), update _state, run OnEnter (reverse)
8. If not changed: update _state (Stay returns same ref; still assign for clarity)
9. Clear in-flight flag
```

Exceptions at any step propagate immediately; `_state` is only updated at step 7/8 after the action
succeeds. The in-flight flag is cleared in a `finally` block.

### D5 — `Stay` action returns the exact same reference

`Stay()` internally stores `action: x => ValueTask.FromResult(x.State)` — the state reference is
returned unchanged. The predicate sees `ReferenceEquals → true → no entry/exit`. This is the
canonical "no transition" path. `Stay(callback)` runs the callback before returning `x.State`.

### D6 — Concurrent-fire detection via `Interlocked.CompareExchange`

A single `int _firing` field (0 = idle, 1 = in-flight). `Interlocked.CompareExchange(ref _firing, 1, 0)`
at entry; `Volatile.Write(ref _firing, 0)` in `finally`. If the exchange fails, throw
`ConcurrentFireException` immediately (no waiting). The exception is thrown before any state is touched.

### D7 — Sync wrappers call `.GetAwaiter().GetResult()`

`Fire` and `TryFire` are convenience wrappers that block on `FireAsync`/`TryFireAsync`. They accept
the same `CancellationToken` parameter. This is correct for sync-only callers and matches .NET
convention (e.g., `HttpClient` sync methods). Deadlock risk on synchronization contexts is the
caller's responsibility — the library targets .NET 8+ where this is a known trade-off.

### D8 — `UnhandledEventException` and `ConcurrentFireException` are domain exceptions

Both inherit from `Exception` directly (not `InvalidOperationException`). This lets callers catch
them specifically. `UnhandledEventException` carries the event and state type for diagnostics.

## Risks / Trade-offs

- **State update before OnEnter throws**: If `OnEnter` throws after `_state` is already updated and
  `OnExit` has run, the executor is left in an inconsistent lifecycle state. Mitigation: this is
  explicitly Layer 11 (error propagation); tests there will define rollback behaviour. For now,
  exceptions propagate and the caller should discard the executor.

- **Sync wrappers and SynchronizationContext**: `.GetAwaiter().GetResult()` can deadlock on
  ASP.NET Classic / WinForms. Accepted trade-off — the library is async-first; sync wrappers are
  explicitly convenience methods for fully synchronous callers.
