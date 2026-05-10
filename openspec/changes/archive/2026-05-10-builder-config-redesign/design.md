## Context

The DSL builder currently uses a "builder owns state" model: each `In<S>()` call creates a new
`StateConfigBuilder<S>` instance holding a private `StateHandlerConfig`. The config is only promoted
to the machine when `Flush()` is triggered — either by a subsequent `In<T>()` call on the same
builder or by `Build()` on the state builder. This creates two failure modes:

1. **Silent drop**: `config.In<S>().OnEnter(X)` as a statement expression creates a builder that is
   immediately abandoned; `Flush()` is never called and the handler is lost with no error.
2. **Ordering asymmetry**: calling `In<S>()` twice in a chain (triggering two `Flush()` calls)
   creates two separate `StateHandler` entries for the same type. `GetSortedStateHandlers` sorts
   them by `DeclarationOrder` ascending; `InReverse()` in `OnEnter` then fires them in
   reverse-declaration order, while `OnExit` fires forward — inconsistent within the same type level.

Both bugs share one root cause: the `StateHandlerConfig` lives inside the builder, not in the
machine config.

## Goals / Non-Goals

**Goals:**
- Make `In<S>()` idempotent so multiple calls always target the same state entry.
- Eliminate `Flush()` so disconnected-style registration is correct for both state and event handlers.
- Guarantee `OnEnter` and `OnExit` callbacks for the same state type always fire in declaration order.
- Detect incomplete `On<E>()` chains at `Build()` time with a clear exception.
- Make `Build()` non-destructive so successive calls produce independent snapshots.

**Non-Goals:**
- Changing the public DSL interfaces (`IMachineConfig`, `IStateConfig`, `IEventConfig`).
- Changing `MachineDefinition`, `MachineExecutor`, or any runtime/ranking logic.
- Supporting concurrent mutation of the machine config from multiple threads.

## Decisions

### Decision 1 — Config dictionary keyed by `Type`, not by generic type parameter

`MachineConfigBuilder` holds `Dictionary<Type, StateHandlerConfig<TContext, TState>>` keyed by the
raw `Type` (e.g., `typeof(StateA)`). `StateConfigBuilder<TCurrentState>` resolves `typeof(TCurrentState)`
at construction and receives a reference to the entry.

**Alternative**: keep separate generic `StateConfigBuilder<S>` instances per call and merge at
`Build()` time. Rejected — merging at build is more complex than preventing duplicates at
registration time, and still risks ordering issues with the merge strategy.

### Decision 2 — `StateHandlerConfig` owns the event handler list

Each `StateHandlerConfig` carries a `List<EventHandlerConfig>` ordered by registration. `On<E>()`
appends a new `EventHandlerConfig` to this list (incomplete). This replaces the flat machine-level
`List<EventHandler>`.

`Build()` collects handlers by iterating all state entries and flattening their event lists, then
passes them to `MachineDefinition` unchanged — same ranked-array logic as today.

**Alternative**: keep a map keyed by event type inside the state config. Rejected — multiple handlers
per `(state, event)` pair (different guards, different transitions) are valid and must be preserved
in declaration order; a plain list is the correct structure.

### Decision 3 — `On<E>()` adds an incomplete entry immediately (Option A)

`On<E>()` creates a new `EventHandlerConfig`, adds it to the state's list with `IsComplete = false`,
and returns an `EventConfigBuilder` that holds a reference to that entry. `GoTo` / `Stay` sets the
action and flips `IsComplete = true`.

`Build()` iterates all entries and throws `IncompleteEventHandlerException` for any with
`IsComplete = false`, reporting state type and event type.

**Alternative** (Option B): hold the config in `EventConfigBuilder` and only add it on `GoTo`/`Stay`.
Rejected — `Build()` cannot detect the incomplete case because the entry is never in the list.

### Decision 4 — `Build()` calls `ToFrozen()` at snapshot time

`StateHandlerConfig` and `EventHandlerConfig` remain mutable after `GoTo`/`Stay`. `Build()` calls
`ToFrozen()` on each entry, which snapshots the current state (guards list via `.ToArray()`). This
means successive `Build()` calls on the same config object produce independent definitions that
reflect the config at the time of each call — a useful property for incremental definition building.

`ToFrozen()` is moved from the point of `GoTo`/`Stay` commit (current) to the point of `Build()`.

### Decision 5 — `StateConfigBuilder` becomes a zero-state typed cursor

After the redesign, `StateConfigBuilder<TCurrentState>` has no mutable fields of its own:

```
StateConfigBuilder<TCurrentState>
  - _machine: MachineConfigBuilder   (for In<T>() and Build() delegation)
  - _config:  StateHandlerConfig     (reference to the shared dictionary entry)
```

`OnEnter(cb)` / `OnExit(cb)` call `_config.Combine(...)` directly; no `Flush()` step.
`In<TNextState>()` no longer needs to call `Flush()` first — it simply looks up/creates the next
state entry and returns a new cursor.

### Decision 6 — `DeclarationOrder` counter stays on `MachineConfigBuilder`

`NextOrder()` remains a monotonic counter on `MachineConfigBuilder`. It is stamped onto each
`EventHandlerConfig` at `On<E>()` time (not `GoTo`/`Stay` time) to preserve registration sequence.
`StateHandlerConfig` does not carry an order — for same-type state handlers there is now only one
entry, so relative ordering within a type level is irrelevant.

## Risks / Trade-offs

**`Build()` is now non-destructive / re-callable** → callers who mutate the config after `Build()`
and re-call `Build()` get a different definition. This is the intended and useful behaviour, but
it is a subtle semantic change from the implicit "one-shot" feel of the current API. Mitigated by
documenting `Build()` as a snapshot operation.

**`On<E>()` with no terminal is only caught at `Build()` time** → incomplete chains in dead code
paths (e.g., inside a disabled feature branch) will not be caught until `Build()` is called. This
is acceptable; the exception carries the state and event types for easy diagnosis.

**Mid-stream capture allows post-`GoTo` `When()` calls** → the concrete `EventConfigBuilder` type
remains usable after `GoTo` because the config entry is still mutable. The fluent interface
(`IEventConfig<S,E>`) prevents this pattern for chained callers; it only surfaces if callers hold
a direct reference to the builder. Behaviour is defined (adds guards to the same handler) and
consistent with the snapshot semantics of `Build()`. No runtime guard is added.

**`StateHandlerConfig` is not generic on `TCurrentState`** → `OnEnter`/`OnExit` delegates are
stored as `Func<Activation<TContext, TState>, ValueTask>` (base types). The covariant conversion
(`x.Convert<TCurrentState>()`) is applied inside `StateConfigBuilder.Combine()` at registration
time, same as today.

## Open Questions

None — all design decisions were resolved during the explore session.
