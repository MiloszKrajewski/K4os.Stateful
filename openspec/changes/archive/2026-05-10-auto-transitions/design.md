## Context

The executor currently processes one external event at a time: fire → find handler → transition → return. Some states need to route themselves forward immediately after being entered, without an external trigger — the UML "completion transition" / epsilon transition pattern. Without it, callers must fire synthetic routing events after every transition, coupling the caller to internal machine structure.

The existing `StateHandler` already holds `OnEnter` and `OnExit` delegates. The `MachineDefinition._stateCache` already indexes handlers by actual state type. The executor already has a clean post-transition hook point (after `OnEnter` returns). These make adding Auto low-risk and well-contained.

## Goals / Non-Goals

**Goals:**
- Add `.Auto()` to `IStateConfig<TCurrentState>` — registers a completion handler returning `ValueTask<TState>`
- After `OnEnter` completes, the executor checks for an Auto handler and chains transitions until no change occurs
- Auto uses the same ranked-handler lookup as OnEnter/OnExit (most-derived state type wins)
- `Create()` does not trigger Auto — consistent with OnEnter not firing on start
- Full entry/exit lifecycle fires for every hop in an Auto chain

**Non-Goals:**
- Infinite loop detection / `WithMaxAutoDepth` — acknowledged risk, deferred
- Multiple `.Auto()` accumulation via chaining — a state has one effective Auto handler (last registration wins per state type, consistent with the distance-based winner-takes-all model)
- Auto triggered by `Create()` or by deserialization restore

## Decisions

### D1 — Auto stored on StateHandler alongside OnEnter/OnExit

**Decision:** Add an `Auto` delegate field (`Func<Activation<TContext, TState>, ValueTask<TState>>?`) to `StateHandler<TContext, TState>`, next to `OnEnter` and `OnExit`.

**Why:** The caching and lookup infrastructure (`_stateCache`, distance-sorted `StateHandler[]`) already exists. Reusing it means Auto gets the same hierarchical lookup (most-derived fires) with zero new data structures.

**Alternative considered:** A separate `_autoCache` keyed by state type. Rejected — adds a third cache identical in structure to `_stateCache`; no benefit.

### D2 — Auto uses winner-takes-all, not "all fire"

**Decision:** Only the Auto handler from the most-derived matching `StateHandler` fires.

**Why:** Auto returns a `TState` value — chaining multiple Auto handlers is semantically undefined (which return value wins?). The most-derived handler is the right authority, matching event-handler semantics. OnEnter/OnExit fire "all" because they are side-effect-only; Auto is a decision function.

**Alternative considered:** Requiring exactly one Auto per concrete state type (throw on ambiguity). Rejected — too strict; base-type Auto handlers as fallbacks are a valid use case.

### D3 — Multiple .Auto() calls on the same state type: last wins

**Decision:** Calling `.Auto()` more than once on the same `In<S>()` scope replaces the previous handler (last write wins within the `StateHandlerConfig`).

**Why:** Unlike `OnEnter`/`OnExit` (both run, chained sequentially), Auto is a value-returning decision. Chaining two decision functions has no clear semantics. Last-wins is the simplest rule and matches how `WithStateChangeIf` behaves.

### D4 — Auto lambda shape matches OnEnter/OnExit: `Activation<TContext, TCurrentState>`

**Decision:** The Auto callback receives `Activation<TContext, TCurrentState>` (context + state + CT, no event).

**Why:** There is no triggering event when Auto fires — it runs after OnEnter, which itself has no event reference. Consistency with OnEnter/OnExit makes the API predictable. The caller can still read `x.Context` and `x.State` to make routing decisions.

### D5 — Auto loop runs inside the existing `_firing` guard

**Decision:** The entire Auto chain (including all intermediate OnExit/OnEnter calls) runs inside the same `_firing = 1` window set by the original `FireAsync` call.

**Why:** Auto transitions are logically part of the same event-handling unit as the triggering `FireAsync`. Allowing concurrent fires during the Auto chain would violate the single-threaded executor contract. The caller sees one `await FireAsync(...)` and the executor returns only after all chained Auto transitions complete.

## Risks / Trade-offs

**Infinite Auto loop** → Mitigation: documented as caller's responsibility. A `WithMaxAutoDepth(n)` guard is noted as a future option. The most common case (routing states that transition to a stable state) terminates naturally.

**Auto on base type fires for all subtypes** → Mitigation: same ranking rules as event handlers apply; a more-derived Auto handler overrides a base-type one. This is the intended polymorphic behaviour.

**Exception mid-chain leaves state at intermediate value** → Mitigation: consistent with existing error propagation — `_state` is written after each individual transition succeeds; if Auto throws, the state is the last successfully-entered state. Documented in the spec.
