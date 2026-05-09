# .NET State Machine Library — Requirements & Decision

> **Status:** Draft — gathering requirements and evaluating options
> **Last updated:** 2026-05-01

---

## Problem Statement

Most .NET state machine libraries model *state as an enum* — a named label with no data.
This is an **academic abstraction** that forces developers to keep actual state data outside
the machine, defeating the purpose of structured state management.

A practical state machine must:
- Hold state as a **typed data object** (not a label)
- Allow transitions to return a **different type** of state object (discriminated-union style)
- Carry data on **events/commands** (triggers are not just signals)
- Support **async side effects** natively — not via a "fire-then-reenter" workaround
- Integrate with the outer world via an injected **Context** object (bus, logger, config)

---

## Core Model

```
Fire(event) ──▶  (CurrentState, Event)  ──▶  NextState
                       ↑ typed input           ↑ typed output (same or different type)
```

- `CurrentState` and `NextState` are .NET objects — they may be the same type or different types
- `Event` is a typed object carrying all relevant data
- Side effects (including output events) go through **Context** — the library has no output channel
- Transition handlers are **async-native** (`Task`-based)

### Example — Typing State Machine

```
State:  { TextSoFar: string }
Events: KeyPressed { Key: char } | EnterPressed

Transitions:
  (TypingState, KeyPressed)   → TypingState { TextSoFar + key }
  (TypingState, EnterPressed) → TypingState { "" }  + context.Bus.Dispatch(new TextSubmitted(TextSoFar))
```

---

## Key Requirements

### R1 — State is a rich object
State must be a .NET class/record. The library must not require state to be an enum.
Transitions can return the **same type** (mutation) or a **different type** (discriminated union step).

### R2 — Typed events/commands
Every trigger carries a typed payload. No `object` boxing.

### R3 — Async-first transitions
Transition handlers and side-effect handlers must support `async Task`.
Synchronous-only support is a disqualifier.

Rationale: a sync-only library forces this workaround:

```
// BAD: async via reentry
await StartOperation();
// later:
machine.Fire(new OperationFinishedEvent(result));  // explosion of ceremony classes
```

### R4 — Output events
Output events (side effects visible to the outer world) are dispatched via **Context**, not via
the library. The library provides no output channel. Example: `a.Context.Bus.Dispatch(new DoorOpened())`.

This keeps the library scope minimal and lets the user choose their own dispatch mechanism
(in-process event bus, MassTransit, NATS, simple callback, etc.).

### R5 — State serialization
The state object must be **serializable** (JSON, binary, etc.).
The library provides no storage — persistence is an external concern.
The library must not hold non-serializable references inside state.

### R6 — Invalid transition handling
Dual API, matching .NET conventions:

| Method | Behavior |
|--------|----------|
| `FireAsync(cmd, ct)` | Throws `UnhandledEventException` if no rule matches |
| `TryFireAsync(cmd, ct)` | Returns `bool` — no exception |

Sync convenience wrappers (`Fire`, `TryFire`) exist; `FireAsync`/`TryFireAsync` are the primary API.

### R7 — Entry / exit actions ✅ Resolved

States support entry and exit hooks. Firing is controlled by a configurable state-change
predicate — see [D1](#d1--what-triggers-entryexit-actions--resolved).
Handlers fire for every type in the inheritance chain; order is determined by type distance
— see [Entry / Exit — hierarchical firing order](#entry--exit--hierarchical-firing-order).

---

## Existing Library Evaluation

| Library | State model | Typed events | Async | Output events | Verdict |
|---------|-------------|--------------|-------|---------------|---------|
| **Stateless** | Enum / generic label | ❌ trigger only, no payload class | ✅ async Fire | ❌ no output events | ❌ enum model, no output |
| **Automatonymous** (MassTransit) | Enum-backed State<T> | ✅ via MassTransit message | ✅ | ✅ via MassTransit pipeline | ⚠️ tied to MassTransit ecosystem |
| **Appccelerate.StateMachine** | Enum / int / string | ❌ | ✅ active mode | ❌ | ❌ enum model |
| **StateMechanic** | Named instance nodes + custom subclasses | ✅ `Event<T>` with typed payload | ❌ sync only | ❌ no output events | ❌ abandoned (2018), sync only |

### Notes

- **Stateless** is the most popular but fundamentally models state as a label. State data lives in
  a closure outside the machine. Entry/exit actions and trigger-with-parameter exist but the model
  is not type-safe across state variants.
- **Automatonymous** has the richest model but drags in the entire MassTransit infrastructure.
  If MassTransit is already in the stack this may be the right answer; otherwise the dependency
  is too heavy.
- **Appccelerate** is feature-complete for enum machines but does not address the core gap.
- **StateMechanic** ([github](https://github.com/canton7/StateMechanic)) — interesting design choices
  but ultimately insufficient and abandoned:
  - ✅ States are instances (not enums); custom subclasses allow attaching data to state nodes
  - ✅ `Event<T>` carries typed payload; `info.EventData` in transition handlers
  - ✅ Fire / TryFire dual API
  - ✅ Hierarchical state machines; StateGroups for shared entry/exit
  - ✅ Entry/exit handlers receive `{ From, To, Event }` info — clean approach
  - ❌ **Sync only** — README explicitly warns against blocking in handlers; no `Task` support anywhere
  - ❌ **No output events** — transitions have no return value or output channel
  - ❌ **State is still a named node**, not a data object; custom subclasses add data *on top of* a label model
  - ❌ Serialization stores only which state-node is active, not state data
  - ❌ Last commit October 2018 — abandoned

### Open: other candidates to evaluate

- [ ] [Workflow Core](https://github.com/danielgerlag/workflow-core) — workflow-oriented, not FSM
- [ ] [LiquidState](https://github.com/prasannavl/LiquidState) — concurrent/async state machines, enum-based
- [ ] Custom minimal library (see [Design Sketch](#design-sketch-if-build))

---

### Stateless feature gap analysis

Features supported by Stateless that this library **will not support**, grouped by reason.

#### Covered differently — not a gap

| Stateless feature | Our equivalent |
|---|---|
| `.SubstateOf()` — explicit superstate hierarchy | Hierarchy is implicit via C# type inheritance (`In<IBase>()` matches all subtypes). For push/pop nested states, carry a `Parent` field in the nested state record and use `GoTo(x => x.State.Parent)` — nesting is a data pattern, not a library feature. The whole stack serializes as nested JSON automatically. |
| `sm.IsInState(superstate)` — hierarchy-aware query | `executor.State is TBase` — standard C# pattern matching |
| `.PermitReentry()` — fires entry/exit on self-loop | `.GoTo(x => x.State with { })` returns new instance → fires entry/exit under default predicate |
| `InternalTransition` — no entry/exit on self-loop | `.Stay()` — returns same reference, predicate suppresses entry/exit |
| Trigger parameters (1–3 typed args) | Events are first-class typed objects — richer, no arity limit |
| Multi-guard AND logic (`.PermitIf(t, d, g1, g2)`) | Single `.When(x => g1(x) && g2(x))` — lambda composition |
| External state storage (getter/setter injection) | `executor.State` + `executor.Start(ctx, state)` — caller owns persistence |

#### Intentionally not supported — out of scope or by design

| Stateless feature | Reason not supported |
|---|---|
| **DOT / Mermaid graph visualization** | Out of scope. Guards are arbitrary lambdas — a complete graph cannot be statically derived. |
| **`sm.GetInfo()` reflection / introspection API** | Out of scope. No static rule graph; structure exists only at runtime. |
| **`CanFire(trigger)` / `GetPermittedTriggers()`** | Out of scope. Equivalent: evaluate guard lambda manually. |
| **`OnActivate` / `OnDeactivate`** | Out of scope. Our `executor.Start()` positions without firing `OnEnter` — caller handles restore logic. |
| **`OnEntryFrom(trigger, ...)` — per-trigger entry** | By design. Per-trigger side effects belong in `.GoTo()`; `OnEnter` is trigger-agnostic. |
| **`OnTransitioned` / `OnTransitionCompleted`** | Out of scope. Cross-cutting logging belongs in `OnEnter`/`OnExit` or in context. |
| **`OnUnhandledTrigger` callback** | By design. `Fire` throws; `TryFire` returns false. "Add a catch-all rule" is the extension point. |
| **`FiringMode.Queued`** — buffer triggers fired during active fire | By design. Concurrent fire throws `ConcurrentFireException`. Caller serializes. |
| **Guard descriptions (string metadata)** | No visualization → no need for labels. |
| **`RetainSynchronizationContext`** | Out of scope. Targeted at Orleans Grains; not a current concern. |

---

## Design Decisions

### D1 — What triggers entry/exit actions? ✅ Resolved

**Decision: configurable predicate; default is `!ReferenceEquals(s1, s2)`.**

```csharp
// Definition-level setting — fluent, optional
var c = StateMachine
    .Define<DoorContext, IDoorState, IDoorEvent>()
    .WithStateChangeIf((s1, s2) => !object.ReferenceEquals(s1, s2));  // this is the default
```

Entry/exit handlers fire when `WithStateChangeIf` returns `true` for `(previousState, nextState)`.

#### Why `!ReferenceEquals` is the right default for immutable records

| Operation | Returns | Same reference? | Entry/exit fires? |
|-----------|---------|-----------------|-------------------|
| `.Stay()` | `x.State` (exact same object) | ✅ yes | ❌ no |
| `.Ignore()` | same (no GoTo) | ✅ yes | ❌ no |
| `.GoTo(x => x.State with { IsLocked = true })` | new record instance | ❌ no | ✅ yes |
| `.GoTo(x => new OpenState(...))` | new instance, different type | ❌ no | ✅ yes |

The `with`-expression on a C# record **always** produces a new instance — even
`s with { }` (no changes) creates a new object. This means reference equality naturally
expresses intent: if you returned the exact same object, you meant "nothing changed";
if you returned a new object, you meant "something changed".

#### Custom predicates

```csharp
// ETag-stamped state — change only when the stamp advances
.WithStateChangeIf((s1, s2) => s1.Etag != s2.Etag)

// Value equality — change when any property differs (record Equals)
.WithStateChangeIf((s1, s2) => !s1.Equals(s2))

// Type-change only — classic FSM semantics
.WithStateChangeIf((s1, s2) => s1.GetType() != s2.GetType())

// Always fire entry/exit on every GoTo (even .Stay)
.WithStateChangeIf((_, _) => true)
```

#### Consequence for `.Stay()` implementation

`.Stay()` must return the **exact same object reference**, not a copy:

```csharp
// ✅ correct — same reference, no entry/exit
.Stay()  // internally: x => x.State  (returns x.State itself)

// ⚠️ subtle — creates a new instance, entry/exit fires under default rule
.GoTo(x => x.State with { })  // equivalent to Stay semantically, but new reference
```

This is a useful property: the user can force entry/exit on a conceptual "no-op" transition
simply by returning a new instance (e.g. to reset a timer in `OnEnter`).

---

## Design Sketch (if build)

> Based on [stateful4k](https://github.com/MiloszKrajewski/stateful4k) (Kotlin) — adapted for C# with async and output events added.

### Key design principles carried over from stateful4k

| Principle | Description |
|-----------|-------------|
| **Dot-Driven Development** | Only one entry point (`StateMachine`); everything else discovered via IntelliSense |
| **Configurator / Executor split** | One config object encodes all rules; many executor instances run against it (one per device, session, etc.) |
| **Base-class matching + fallback** | Rules declared on `IState` match all subtypes; rules without `.When(…)` are lower-ranked fallbacks |
| **Context** | Shared service/infrastructure object injected per-executor (e.g. `ILogger`, `ISoundPlayer`); not part of state |

### Domain types (example — Door)

```csharp
// States — plain records, no base class required (marker interface optional)
interface IDoorState { }
record ClosedState(bool IsLocked) : IDoorState;
record OpenState(bool IsLocked)   : IDoorState;

// Events — plain records
interface IDoorEvent { }
record OpenEvent    : IDoorEvent;
record CloseEvent   : IDoorEvent;
record LockEvent    : IDoorEvent;
record UnlockEvent  : IDoorEvent;

// Domain events dispatched via Context.Bus — not part of the library
record DoorOpened(bool WasLocked);
record DoorClosed;

// Context — injected services, shared across all states of one executor instance
// This is the integration point with the outer world; the library does not inspect it
class DoorContext
{
    public ISoundPlayer Sounds { get; init; } = null!;
    public IMessageBus  Bus    { get; init; } = null!;
    public ILogger      Logger { get; init; } = null!;
}
```

### Fluent chain — type threading

The DSL entry point captures `TContext`, `TState`, `TEvent` once:

```csharp
var c = StateMachine.Define<DoorContext, IDoorState, IDoorEvent>();
```

Each subsequent method narrows to a **more specific type** and returns a builder
carrying all accumulated types. Callers never write angle brackets after `In` or `On`.

After `GoTo()`/`Stay()` the chain returns to the **state-level facet** — the same
surface as after `.In<T>()`. This means a new `.On<TEvent>()` (same state, new handler)
or `.In<TOtherState>()` (new state) can be chained directly without starting a new
`c.In<T>()` call.

```
c                             : IMachineConfig          (exposes In<T>, Build)
 .In<ClosedState>()           : IStateConfig<Closed>    (exposes OnEnter, OnExit, On<T>, In<T>)
 .OnEnter(x => ...)           : IStateConfig<Closed>    (same facet — fluent self)
 .On<LockEvent>()             : IEventConfig<Closed, LockEvent>
 .When(x => ...)              : IEventConfig<...>       (same facet — guard added)
 .GoTo(x => ...)              : IStateConfig<Closed>    ← back to state facet!
 .On<KnockEvent>()            : IEventConfig<Closed, KnockEvent>
 .Stay()                      : IStateConfig<Closed>
 .In<OpenState>()             : IStateConfig<Open>      (new state — any facet can pivot)
 .On<CloseEvent>().GoTo(...)  : IStateConfig<Open>
 .Build()                     : StateMachine<...>       (terminal — on IMachineConfig)
```

At `.In<ClosedState>()` the builder fixes `TCurrentState = ClosedState`.
At `.On<LockEvent>()` it fixes `TCurrentEvent = LockEvent`.
Every subsequent lambda receives a **single bundle object** typed at the concrete level.

#### Facet interfaces — one class, multiple visible surfaces

Proliferating builder classes (`InBuilder`, `OnBuilder`, `PostGoToBuilder`, …) leads to
an ever-growing class graph with subtly different members. The better model:
**define the API surface as interfaces (facets) and implement them in a minimal set of
concrete classes**.

```csharp
// Nested inside StateMachine<TContext, TState, TEvent>
// so C, S, E are implicit in every interface.

// Top-level facet — exposed by the StateMachine factory
public interface IMachineConfig
{
    IStateConfig<TCurrentState> In<TCurrentState>() where TCurrentState : TState;
    StateMachine<TContext, TState, TEvent> Build();
}

// State facet — returned by In<T>(), GoTo(), Stay()
public interface IStateConfig<TCurrentState> where TCurrentState : TState
{
    IStateConfig<TCurrentState> OnEnter(
        Func<Activation<TContext, TCurrentState>, ValueTask> callback);
    IStateConfig<TCurrentState> OnExit(
        Func<Activation<TContext, TCurrentState>, ValueTask> callback);

    IEventConfig<TCurrentState, TCurrentEvent> On<TCurrentEvent>()
        where TCurrentEvent : TEvent;

    // pivot to a new state — available from anywhere in the chain
    IStateConfig<TNextState> In<TNextState>() where TNextState : TState;

    StateMachine<TContext, TState, TEvent> Build();
}

// Event facet — returned by On<T>() and by When()
// Multiple .When() calls accumulate as AND logic — all guards must pass.
public interface IEventConfig<TCurrentState, TCurrentEvent>
    where TCurrentState : TState
    where TCurrentEvent : TEvent
{
    // Returns itself — may be called again to add more guards (AND semantics)
    IEventConfig<TCurrentState, TCurrentEvent> When(
        Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask<bool>> guard);

    // Terminals — available with or without a prior .When()
    IStateConfig<TCurrentState> GoTo(
        Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask<TState>> next);
    IStateConfig<TCurrentState> Stay(
        Func<Activation<TContext, TCurrentState, TCurrentEvent>, ValueTask>? action = null);
}
```

**Key insight:** `GoTo` and `Stay` are no longer `void` — they return `IStateConfig<TCurrentState>`.
This makes the continuation natural: after registering one handler the chain is already at the
right facet to register the next handler (`.On<T>`) or pivot to a new state (`.In<T>`).

**Extension methods** (not interface members) provide `Task<X>` and sync overloads for `.When`, `.GoTo`, and `.Stay`. C# resolves them uniformly with instance methods — callers see no difference:

- `.When(Func<..., bool>)`, `.When(Func<..., Task<bool>>)` — wrap into `ValueTask<bool>`
- `.GoTo(Func<..., TState>)`, `.GoTo(Func<..., Task<TState>>)`, `.GoTo(Func<TCurrentState, TState>)` — wrap into `ValueTask<TState>`
- `.Stay(Action<Activation<...>>)` — sync side-effect shorthand, wraps into `ValueTask`

**Callback storage:** All callbacks are stored internally as `Func<Activation<TContext, TState, TEvent>, ValueTask<X>>` — using the **base** state and event types (`TState`, `TEvent`), not the concrete chain types. When registering `In<AS>().On<AE>().GoTo(fn)`, the builder wraps the lambda: `activation => fn(new Activation<C, AS, AE>((AS)activation.State, (AE)activation.Event, ...))`. Async-native callers using `async x => ...` reach the interface directly with zero overhead.

**Multiple `.When()` calls — AND semantics:** Calling `.When()` more than once on the same
`IEventConfig` accumulates guards — all must pass. This resolves the AND vs OR ambiguity in
favour of AND, the only supported composition. Callers who need OR semantics write a single
lambda with `||` inside.

**Implementation:** A small number of concrete builder classes implement these interfaces.
The outer `StateMachine<C,S,E>` itself can implement `IMachineConfig`; a single inner class
`StateConfigBuilder<TCurrentState>` can implement `IStateConfig<TCurrentState>`;
`EventConfigBuilder<TCurrentState, TCurrentEvent>` implements `IEventConfig<...>` — returned
from both `.On<T>()` and `.When()`, so callers always hold the same interface regardless of
how many guards have been added.
The concrete types are never written by callers — `var` and method chaining mean types are
always inferred. The interfaces are the only visible contract.

**Nested-class benefit retained:** Nesting `StateConfigBuilder` and `EventConfigBuilder`
inside `StateMachine<TContext, TState, TEvent>` means the three domain-level type parameters
are inherited implicitly. Neither class nor its callers need to repeat them.

---

### Handler bundle — `Activation<TContext, TCurrentState, TCurrentEvent>`

Multiple lambda parameters `(ctx, s, e, ct)` don't scale — every new cross-cutting
concern adds another parameter to every existing lambda.
Instead, all lambdas receive **one `Activation` object** with named properties.

```csharp
// Before (4 params)
.GoTo(async (ctx, s, e, ct) => { await ctx.Sounds.PlayAsync(e.Title, ct); return s; })

// After (1 Activation)
.GoTo(async x => { await x.Context.Sounds.PlayAsync(x.Event.Title, x.CancellationToken); return x.State; })
```

#### `Activation<TContext, TCurrentState, TCurrentEvent>` and `Activation<TContext, TCurrentState>`

```csharp
// Used by .When / .GoTo / .Stay — full bundle including the triggering event
public sealed class Activation<TContext, TCurrentState, TCurrentEvent>
{
    public TContext          Context           { get; }  // "state of the world": bus, config, IServiceProvider…
    public TCurrentState     State             { get; }  // concrete current state
    public TCurrentEvent     Event             { get; }  // concrete triggering event
    public CancellationToken CancellationToken { get; }  // from the Fire/TryFire call site
}

// Used by OnEnter / OnExit / Auto — no Event (entry/exit is not tied to a specific event type)
public sealed class Activation<TContext, TCurrentState>
{
    public TContext          Context           { get; }
    public TCurrentState     State             { get; }
    public CancellationToken CancellationToken { get; }
}
```

**Output events are out of scope for the library.**
Context is the integration point with the outer world — dispatching output, publishing to a bus,
logging, or any other side effect goes through `a.Context`:

```csharp
.GoTo(async x =>
{
    await x.Context.Bus.Dispatch(new DoorOpened(x.State.IsLocked), x.CancellationToken);
    await x.Context.Sounds.PlayAsync("squeak", x.CancellationToken);
    return new OpenState(IsLocked: false);
})
```

The library has no opinion on what "emitting an event" means. That is entirely the user's domain.

#### What each method uses in practice

| Method | Typically uses | Note |
|--------|---------------|------|
| `.When(x => ...)` | `x.State`, `x.Event`, maybe `x.Context` | interface: `ValueTask<bool>`; sync/Task forms via extension methods |
| `.GoTo(async x => ...)` | all of `x` — side effects + state decision | async; returns `TState` |
| `.Stay(async x => ...)` | `x.Context`, `x.CancellationToken` | optional callback; state ref unchanged |
| `.OnEnter/OnExit(x => …)` | `x.Context`, `x.State` | `Activation<TContext, TState>` — no Event |

#### Revised examples

```csharp
c.In<ClosedState>()
 .On<LockEvent>()
 .When(x => !x.State.IsLocked)
 .GoTo(async x =>
 {
     await x.Context.Sounds.PlayAsync("clack", x.CancellationToken);
     await x.Context.Bus.Dispatch(new DoorLocked(), x.CancellationToken);  // output via Context
     return x.State with { IsLocked = true };                              // x.State is ClosedState ✅
 });

c.In<ClosedState>()
 .On<OpenEvent>()
 .When(x => !x.State.IsLocked)
 .GoTo(async x =>
 {
     await x.Context.Sounds.PlayAsync("squeak", x.CancellationToken);
     await x.Context.Bus.Dispatch(new DoorOpened(x.State.IsLocked), x.CancellationToken);
     return new OpenState(IsLocked: false);                                // cross-type ✅
 });
```

#### `.GoTo` — async, side effects included

`.GoTo` owns both side effects and the state decision. A side effect may influence which
state to return (e.g. a query result), so separating them was never safe:

```csharp
.GoTo(async x => {
    var profile = await x.Context.Db.LoadAsync(x.Event.UserId, x.CancellationToken);
    await x.Context.Bus.Dispatch(new DoorOpened(), x.CancellationToken);
    return profile.RequiresLock ? new LockedState() : new OpenState(IsLocked: false);
})
```

For simple cases with no async side effects, sync extension methods keep things concise:

```csharp
.GoTo(x => x.State with { IsLocked = true })   // sync extension — full Activation
.GoTo(s => s with { IsLocked = true })          // sync extension — state-only (s: TCurrentState)
```

The shorthand is sugar — the builder generates `x => fn(x.State)` internally.
Return type of all `.GoTo` overloads is `TState`; any subtype is accepted,
enabling cross-type transitions.

`.Stay()` is the terminal for "no state change" — returns the same reference.
An optional callback handles side effects without breaking the reference guarantee:

```csharp
.Stay()                                                          // silent
.Stay(async x => await x.Context.Logger.LogAsync("no-op"))      // with side effect
```

---

### Configuration DSL (revised)

```csharp
var c = StateMachine.Define<DoorContext, IDoorState, IDoorEvent>();

// ── Transitions ────────────────────────────────────────────────────────────
//
// c.In<TState>()       — fixes TCurrentState; x.State is TCurrentState in all lambdas
// .On<TEvent>()        — fixes TCurrentEvent; x.Event is TCurrentEvent in all lambdas
// .When(x => …)       — optional guard; no .When = lower-ranked fallback
// .GoTo(async x => …) — registers rule, returns IStateConfig<TCurrentState> for chaining
// .Stay()              — registers rule (same ref), returns IStateConfig<TCurrentState>
// .Stay(async x => …) — Stay with optional side-effect callback
//
// GoTo/Stay return the *state facet* — allows chaining .On<TEvent>() or .In<TOtherState>()
// without repeating c.In<T>() for each handler.

// ── Chained style (same state, multiple handlers) ─────────────────────────

c.In<ClosedState>()
 .OnEnter(async x => await x.Context.Logger.LogAsync("entered closed"))
 .OnExit( async x => await x.Context.Logger.LogAsync("left closed"))
 .On<LockEvent>()
     .When(x => !x.State.IsLocked)
     .GoTo(async x =>
     {
         await x.Context.Sounds.PlayAsync("clack", x.CancellationToken);
         return x.State with { IsLocked = true };
     })
 .On<OpenEvent>()
     .When(x => !x.State.IsLocked)
     .GoTo(async x =>
     {
         await x.Context.Sounds.PlayAsync("squeak", x.CancellationToken);
         await x.Context.Bus.Dispatch(new DoorOpened(), x.CancellationToken);
         return new OpenState(IsLocked: false);                              // cross-type ✅
     })
 .On<OpenEvent>()                                                            // fallback (no .When)
     .Stay(async x => await x.Context.Sounds.PlayAsync("click-click", x.CancellationToken))
 .In<OpenState>()                                                            // pivot to next state
 .OnEnter(async x => await x.Context.Logger.LogAsync($"entered open, locked={x.State.IsLocked}"))
 .On<CloseEvent>()
     .GoTo(x => new ClosedState(IsLocked: false))
 .Build();

// ── Disconnected style (also valid — same result) ─────────────────────────
// c.In<ClosedState>().On<LockEvent>().When(...).GoTo(...);
// c.In<ClosedState>().On<OpenEvent>().When(...).GoTo(...);
// …
// Both styles register into the same StateMachine definition.

// ── Base-type rule: cross-cutting logging ──────────────────────────────────

c.In<IDoorState>()
 .On<IDoorEvent>()
 .Stay(x => x.Context.Logger.LogDebug("Event {E} in state {S}", x.Event, x.State));  // sync extension
```

### Entry / Exit — hierarchical firing order

Entry/exit handlers are **different from transition rules** — where rules use "highest rank wins"
(one rule fires), entry/exit handlers **all fire** for every type in the inheritance chain
that has a registered handler.

Given:
```csharp
// State hierarchy: IDoorState ← ClosedState ← DeadlockedState
c.In<IDoorState>()    .OnEnter(x => Log("entered any door state"));
c.In<ClosedState>()   .OnEnter(x => Log("entered closed"));
c.In<DeadlockedState>().OnEnter(x => Log("entered deadlocked"));
```

Transitioning **into** `DeadlockedState` fires in **base → derived** order:
```
OnEnter<IDoorState>      ← fires first  (most general)
OnEnter<ClosedState>     ← fires second
OnEnter<DeadlockedState> ← fires last   (most specific)
```

Transitioning **out of** `DeadlockedState` fires in **derived → base** order:
```
OnExit<DeadlockedState>  ← fires first  (most specific)
OnExit<ClosedState>      ← fires second
OnExit<IDoorState>       ← fires last   (most general)
```

**Rationale:** Entry mirrors C# constructor order (base initialises before derived);
exit mirrors destructor/`Dispose` order (derived cleans up before base).
It also matches UML hierarchical state machine semantics exactly.

#### Distance algorithm (classes + interfaces)

Every registered handler type gets a **distance** from the actual state type.
Entry fires in **descending** distance order (far → close); exit in **ascending** order.

**Rules:**
1. Actual state type → distance **0**
2. Each class in the base chain → distance = number of steps up (`+1` per level)
3. An interface → **same distance** as the first class in the chain that declares it

At the same distance, **class is considered closer than interface** — so within a rank level,
interface handlers fire first (farther), class handler fires last (closer).

**Example** — actual type `C`:

```
interface IBase {}   // introduced by A
interface IMid  {}   // introduced by B
interface ILeaf {}   // introduced by C

class A : IBase      {}   // distance 2
class B : A, IMid    {}   // distance 1
class C : B, ILeaf   {}   // distance 0  ← actual type
```

| Rank | Types at this rank | Fire order within rank |
|------|-------------------|----------------------|
| 3 | `object` | — |
| 2 | `IBase`, `A` | IBase → A |
| 1 | `IMid`, `B` | IMid → B |
| 0 | `ILeaf`, `C` | ILeaf → C |

Interfaces are **orthogonal** to the class chain — they attach laterally at the rank of their
introducing class. The rank table above is the correct mental model; a linear arrow notation
would falsely imply `IBase → A → IMid` is a chain.

Entry order (descending rank): `object → IBase → A → IMid → B → ILeaf → C`
Exit  order (ascending rank):  `C → ILeaf → B → IMid → A → IBase → object`

**Multiple interfaces at the same class** (e.g. `class C : B, IA, IB, IC`):
all sit at rank 0, all fire before `C`. Secondary sort within interfaces: **declaration order** (left to right).

**Key distinction between the two systems:**

| | Event handlers (`.In<T>.On<E>`) | State handlers (`.OnEnter<T>` / `.OnExit<T>`) |
|---|---|---|
| How many fire? | **One** — first match in sort order | **All** — every handler whose `T` is assignable from actual state |
| Sort/order | `(stateDistance, stateClass/Interface, eventDistance, eventClass/Interface, hasGuard, declarationOrder)` | Descending rank (entry); ascending (exit); interface before class within rank |
| No match | `UnhandledEventException` / `TryFire` returns false | Silent (no handler registered at that level) |

### `.When` — single lambda shape, AND accumulation

With `x.State` and `x.Event` both available, there is **one lambda shape** instead of three.
The interface declares the `ValueTask<bool>` form; extension methods add sync `bool` and `Task<bool>` overloads.
Multiple `.When()` calls on the same handler accumulate as AND — all guards must pass.

```csharp
.When(x => !x.State.IsLocked)             // sync extension — state only
.When(x => x.Event.IsForced)              // sync extension — event only; AND with above if chained
.When(x => x.State.Count < x.Event.Max)  // sync extension — state + event, same signature
.When(async x => await x.Context.Db.IsAllowedAsync(x.State.Id, x.CancellationToken))  // interface — ValueTask<bool>
```

### `.GoTo` — async, sync, and state-only overloads

`.GoTo` owns both side effects and state decision. The interface accepts `ValueTask<TState>`;
extension methods provide `Task<TState>`, sync `TState`, and state-only shorthand overloads:

```csharp
.GoTo(async x => { ...; return new OpenState(); })  // interface — ValueTask<TState>
.GoTo(x => x.State with { IsLocked = true })        // sync extension — full Activation
.GoTo(s => s with { IsLocked = true })              // sync extension — state-only (s: TCurrentState)
```

Return type of the `.GoTo` lambda is always `TState` (base). Any subtype is accepted,
enabling cross-type transitions.


### Executor — lifecycle and state portability

The executor is the **running instance** of a machine definition.
One `sm` (frozen definition) → many executors (one per entity: device, session, user, etc.).

```csharp
// Freeze definition once — shared, immutable, thread-safe
var sm = c.Build();

// Create an executor and start it — fresh state
var executor = sm.Create();
executor.Start(
    context: new DoorContext { Sounds = soundPlayer, Logger = logger },
    state:   new ClosedState(IsLocked: false));

// Restore from persisted state (same API — library doesn't care which it is)
var executor2 = sm.Create();
executor2.Start(
    context: new DoorContext { Sounds = soundPlayer, Logger = logger },
    state:   JsonSerializer.Deserialize<IDoorState>(savedJson)!);
```

`Start` does **not** fire `OnEnter` — the executor is being positioned at a state, not
transitioning into it. This is correct for both fresh starts and deserialized resumes:
the library cannot distinguish the two, and `OnEnter` semantics are strictly
*"a transition just brought us here"*. Initialization logic on first start is the caller's responsibility.

#### Firing events

```csharp
// Async primary — ct threads through to x.CancellationToken in every lambda
await executor.FireAsync(new OpenEvent(), ct);             // throws UnhandledEventException
bool ok = await executor.TryFireAsync(new CloseEvent(), ct);  // false on miss, no throw

// Sync convenience wrappers
executor.Fire(new LockEvent(), ct);
bool ok2 = executor.TryFire(new UnlockEvent(), ct);
```

#### Reading and saving state

```csharp
// Read current state for inspection or serialization
IDoorState current = executor.State;
if (current is OpenState open) { /* pattern match */ }

// Save — the library returns the state object; the caller owns serialization
string json = JsonSerializer.Serialize(executor.State);  // any serializer

// Restore later — pass deserialized state into Start (see above)
```

The library serializes nothing. `executor.State` returns the raw state object;
`executor.Start(context, state)` accepts any state of type `TState` — deserialized or fresh.

### Event handler matching & ranking

Event handlers share the same **distance** concept as state handlers, but the algorithm is
**winner-takes-all**: handlers are sorted and walked top-to-bottom; the first handler whose guard
passes (short-circuit) fires. No handler fires twice.

Event handlers are sorted by a six-part key:

| Key | Direction | Meaning |
|-----|-----------|---------|
| state `distance` | ASC (smaller first) | concrete state subtype beats base type |
| state `class / interface` | class first | class beats interface at same state distance |
| event `distance` | ASC (smaller first) | concrete event subtype beats base type |
| event `class / interface` | class first | class beats interface at same event distance |
| `hasGuard` | guarded first | conditional rule beats unconditional fallback |
| `declarationOrder` | ASC (earlier first) | stable tie-break; user controls by ordering declarations |

```csharp
c.In<C>().On<E>().When(x => x.State.IsReady).GoTo(...)  // (0, class, 0, class, guarded, 0) — tried first
c.In<C>().On<E>().GoTo(...)                              // (0, class, 0, class, unguarded, 1) — fallback
c.In<B>().On<E>().When(x => x.State.Count > 0).GoTo(...)// (1, class, 0, class, guarded, 2)
c.In<A>().On<E>().GoTo(...)                              // (2, class, 0, class, unguarded, 3) — catch-all
```

If no handler matches → `Fire` throws `UnhandledEventException`; `TryFire` returns `false`.

### Settled design notes

**`.Do` removed** — `.GoTo(async x => ...)` owns both side effects and the state decision.
Side effects can influence the next state (e.g. a query), so separating them into `.Do`
was artificial. `.Stay(callback?)` covers the side-effect-without-state-change case.
Lambda composition for sequencing is the caller's responsibility — no chaining API needed.

**Unhandled events** — no static analysis, no `BuildStrict()`. That would be a false promise
(guards are arbitrary lambdas; subtype enumeration is unbounded). `Build()` is enough.
At runtime: `TryFire` returns `false`, `Fire` throws `UnhandledEventException`.
Users who want a guaranteed catch-all add a handler themselves:
```csharp
c.In<TState>().On<TEvent>().Stay(x => x.Context.Logger.LogWarning("Unhandled"));  // sync extension
```

**Thread safety** — the executor is single-threaded by contract. Concurrent `FireAsync`
calls on the same executor are detected via `Interlocked` and throw `ConcurrentFireException`.
Caller is responsible for serializing access.

**Error propagation** — exceptions in `.When`, `OnEnter`, `OnExit`, `.GoTo`, `.Stay` propagate immediately.
No swallowing, no "skip remaining levels". An error is an error.

**`Stay()` / `Ignore()`** — no distinction. `Stay()` returns the same state reference;
state-change predicate sees `ReferenceEquals → true → no entry/exit`. One terminal, not two.

---

### Residual mutation problem: `In<IDoorState>` (base-type rules)

With the `In<TState>().On<TEvent>()` chain, the `with`-expression problem is **fully resolved for
concrete-type rules** — `s` is the concrete type the caller declared.

It only reappears if the caller *intentionally* writes `In<IDoorState>()` and then tries to mutate.
That case is now **self-inflicted and obvious** — the caller sees `IDoorState s` in their lambda
and understands why `with` doesn't compile. Options:

| | |
|---|---|
| **Write concrete-type rules** (one per subtype) | Recommended — type-safe, more rules |
| **Interface mutation method** (`WithLocked(bool)`) | Good when all subtypes share the property |
| **Pattern match + `switch`** | Opt-in escape hatch; add `_ => throw new UnreachableException()` |

The fourth option from the previous analysis (D — rule composition) is moot because
`In<TState>()` makes the type explicit, so base-type rules for side-effects and
concrete-type rules for mutation are cleanly separate without any framework magic.

---

### `.Auto()` — completion transitions

> ⚠️ **Not yet implemented** — Layer 14 (planned; depends on Layers 5 and 7).

A state can declare an **automatic transition** that fires immediately after `OnEnter` completes,
without waiting for an external event. This is the completion transition / epsilon transition from
UML state machines.

```csharp
c.In<DeviceConnectedState>()
 .OnEnter(x => ...)
 .Auto(async x => x.State.IsRegistered
     ? new RegisteredState(...)
     : (IDeviceState)new UnregisteredState(...));
```

**Lambda shape:** Receives `Activation<TContext, TCurrentState>` (same as `OnEnter`/`OnExit`) —
there is no triggering event.

**"No transition" semantics:** Return the same reference → predicate sees no change → stays in
current state. Consistent with `.Stay()`.

**Execution sequence:** After `OnEnter` completes, the executor checks for an `Auto` handler:

```
FireAsync(event)
  → GoTo handler runs → new state S1
  → predicate: changed? → OnExit(old), OnEnter(S1)
  → Auto(S1)? → returns S2 (different ref)
    → predicate: changed? → OnExit(S1), OnEnter(S2)
    → Auto(S2)? → ...
  → return to caller
```

`FireAsync` may chain multiple transitions internally. The caller sees one await, one final state.

**Chaining:** Multiple states in sequence can each declare `Auto`, enabling logical routing pipelines
without external triggers. Each hop fires its full `OnEnter`/`OnExit` cycle.

> ⚠️ **Infinite loop risk:** If `Auto` always returns a new instance the chain will not terminate.
> This is currently the caller's responsibility to avoid. A future `WithMaxAutoDepth(n)` option
> may be added to detect runaway chains and throw `AutoTransitionDepthException`.

**API facet:** `.Auto()` is available on `IStateConfig<TCurrentState>` alongside `OnEnter` and `OnExit`.
Returns `IStateConfig<TCurrentState>` for continued chaining.

```csharp
public interface IStateConfig<TCurrentState> where TCurrentState : TState
{
    // ... existing members ...
    IStateConfig<TCurrentState> Auto(
        Func<Activation<TContext, TCurrentState>, ValueTask<TState>> handler);
}
```

Extension methods provide sync and `Task<TState>` overloads, matching the pattern established by
`.GoTo()`.

---

## Testing Concepts

> Not test cases — concepts that must be covered. One concept may yield multiple cases (happy path, edge, error).

### Handler distance & rule ranking
- Concrete type rule fires over base class rule (distance wins)
- Base class rule fires over interface rule at same distance (class-before-interface)
- Interface rule fires before class rule within same rank for entry/exit (entry/exit: all fire, ordered)
- Multi-level hierarchy: A → B → C — correct winner at each level
- Interface introduced at different class levels — correct distance assigned
- Multiple interfaces on one class — declaration order as tie-break

### Guard evaluation
- Guarded rule fires over unguarded at same distance
- First passing guard short-circuits (second matching rule does NOT fire)
- Guard returns false → falls through to next candidate
- All guards fail → `UnhandledEventException` / `TryFire` returns false
- Declaration order resolves tie between two unguarded rules at same distance
- Guard throws → exception propagates, no state change
- Multiple `.When()` calls on the same handler use AND logic — all must pass to fire

### Entry / exit firing
- Entry fires base → derived order on transition in
- Exit fires derived → base order on transition out
- Only handlers registered for types in the actual inheritance chain fire
- Multiple `OnEnter` registrations for same type all fire (in declaration order)
- `OnExit` completes before `OnEnter` starts

### State-change predicate
- Default `!ReferenceEquals`: `Stay()` → no entry/exit (same ref)
- Default: `GoTo(x => x.State with { })` → fires entry/exit (new instance)
- Default: `GoTo(x => new OtherState())` → fires entry/exit
- Custom predicate: ETag-based, value-equality, always-true, always-false
- `executor.Start()` does NOT fire `OnEnter` regardless of predicate

### Transitions
- Same-type transition (mutation via `with`)
- Cross-type transition (A → B where B ≠ A)
- Transition to base type accepted (`GoTo` returns `TState`, not `TCurrentState`)
- `Stay()` with no callback — silent no-op
- `Stay(callback)` — callback runs, no entry/exit fires
- `GoTo` async — side effects complete before state updates
- `GoTo` sync overload — wraps correctly

### Unhandled events
- `Fire` throws `UnhandledEventException` when no rule matches
- `TryFire` returns `false` when no rule matches
- `Fire` throws when rules exist but all guards fail
- Catch-all rule (`In<TState>.On<TEvent>` with no guard) suppresses exception

### Executor lifecycle
- `Start()` sets state without firing `OnEnter`
- `executor.State` returns current state after `Start()` and after `Fire()`
- Multiple executors from the same frozen definition are fully independent
- Chained style and disconnected style register identical rules (same behaviour)

### Thread safety
- Concurrent `FireAsync` on the same executor throws `ConcurrentFireException`
- Sequential fires on same executor work correctly

### Error propagation
- Exception in `GoTo` → propagates, state does NOT change
- Exception in `When` guard → propagates, state does NOT change
- Exception in `OnExit` → propagates (OnEnter does not run)
- Exception in `OnEnter` → propagates

### Nested states (data pattern)
- `GoTo(x => x.State.Parent)` returns to parent state correctly
- Entry/exit fires for parent state on return
- Nested state serializes/deserializes as plain JSON (whole stack in one object)

### `WithStateChangeIf` configuration
- Custom predicate receives `(previousState, nextState)` correctly typed
- Predicate returning `false` suppresses entry/exit even on type change
- Predicate returning `true` always fires entry/exit even on `Stay()`-equivalent GoTo

### CancellationToken
- Token passed to `FireAsync(event, ct)` flows through to `x.CancellationToken` in all lambdas
- Cancellation in `GoTo` propagates correctly

### Auto transitions (completion transitions)
- `Auto` handler fires after `OnEnter` completes, not after `Start()`
- Returning same reference → no-op (predicate suppresses further transition)
- Returning different state → full transition (OnExit → OnEnter → Auto check again)
- Chain of Auto transitions — final state reached before `FireAsync` returns to caller
- Auto on a state with no further Auto — stops cleanly
- `Start()` does NOT trigger `Auto` (same reasoning as `OnEnter`)
- `Auto` lambda receives `Activation<TContext, TCurrentState>` (no event) — context and state available
- Cancellation token flows through to `Auto` lambda
- Exception in `Auto` propagates; state is left at the state that triggered the Auto


---

## Next Steps

- [ ] Investigate LiquidState async capabilities in detail
- [ ] Prototype Stateless with a state-object wrapper — how much ceremony does it add?
- [ ] Decide: adopt vs build
