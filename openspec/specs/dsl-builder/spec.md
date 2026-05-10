# Spec: DSL Builder

## Purpose

Defines the fluent DSL API for configuring a state machine. All configuration flows from a single
static entry point and uses chained interface methods to register event handlers and lifecycle
callbacks, culminating in an immutable `MachineDefinition`.

## Requirements

### Requirement: StateMachine.Define is the sole entry point

The library SHALL expose a single static entry point `StateMachine.Define<TContext, TState, TEvent>()`
that returns `IMachineConfig`. All configuration MUST flow from this one call; no other public
constructors or factory methods for the configuration graph SHALL exist.

#### Scenario: Entry point captures all three type parameters
- **WHEN** `StateMachine.Define<DoorContext, IDoorState, IDoorEvent>()` is called
- **THEN** all subsequent DSL calls are constrained to those three types without repeating them

---

### Requirement: In<T> scopes configuration to a state type

`IMachineConfig.In<TCurrentState>()` and `IStateConfig<T>.In<TNextState>()` SHALL return
`IStateConfig<TCurrentState>` scoped to that state type. `TCurrentState` MUST be assignable
to `TState`.

#### Scenario: In<T> narrows state type for subsequent lambdas
- **WHEN** `.In<ClosedState>().On<LockEvent>().GoTo(x => ...)` is registered
- **THEN** `x.State` is typed as `ClosedState`, not `IDoorState`

#### Scenario: In<T> can pivot from any point in the chain
- **WHEN** `.In<ClosedState>().On<LockEvent>().GoTo(...).In<OpenState>().On<CloseEvent>().GoTo(...)` is called
- **THEN** both handlers are registered; `GoTo` on `ClosedState` returns `IStateConfig<ClosedState>` enabling `.In<OpenState>()` chaining

---

### Requirement: On<E> scopes configuration to an event type

`IStateConfig<TCS>.On<TCurrentEvent>()` SHALL return `IEventConfig<TCS, TCurrentEvent>`.
`TCurrentEvent` MUST be assignable to `TEvent`.

#### Scenario: On<E> narrows event type for subsequent lambdas
- **WHEN** `.On<LockEvent>().When(x => ...)` is registered
- **THEN** `x.Event` is typed as `LockEvent`, not `IDoorEvent`

---

### Requirement: When adds a guard to the current event handler

`IEventConfig.When(guard)` SHALL add a guard to the current `EventHandlerConfig` and return the
same `IEventConfig<TCS, TCE>` — allowing further `.When()`, `.GoTo()`, or `.Stay()` calls.
Multiple `.When()` calls SHALL combine their guards with AND semantics: all guards must pass for
the handler to fire. `HasGuard` SHALL be `true` as soon as any `.When()` is called.

#### Scenario: When with sync bool guard is accepted
- **WHEN** `.When(x => !x.State.IsLocked)` is registered
- **THEN** the handler has `HasGuard = true` and the guard delegate is stored

#### Scenario: Multiple When calls combine with AND
- **WHEN** `.When(g1).When(g2).GoTo(...)` is registered
- **THEN** the handler fires only when both `g1` and `g2` return true; if either returns false the handler is skipped

#### Scenario: When returns IEventConfig allowing GoTo or Stay to follow
- **WHEN** `.When(guard).GoTo(action)` is called
- **THEN** the handler is registered with the guard and action; further `.On<E>()` or `.In<T>()` can chain from the returned `IStateConfig`

---

### Requirement: GoTo registers a transition action

`IEventConfig.GoTo(fn)` SHALL register an `EventHandler` with the accumulated state type,
event type, optional guard, and the provided action delegate. It SHALL return
`IStateConfig<TCurrentState>` to allow continued chaining.

The action delegate SHALL accept `Activation<TContext, TCurrentState, TCurrentEvent>` and return
`ValueTask<TState>`. Sync (`TState`) and `Task<TState>`-returning overloads are provided as
interface default implementations.

#### Scenario: GoTo registers EventHandler with correct fields
- **WHEN** `In<ClosedState>().On<LockEvent>().GoTo(x => x.State with { IsLocked = true })` is called
- **THEN** one `EventHandler` is registered with `StateType = typeof(ClosedState)`,
  `EventType = typeof(LockEvent)`, `HasGuard = false`, and a non-null `Action` delegate

#### Scenario: GoTo with When registers guarded EventHandler
- **WHEN** `In<ClosedState>().On<LockEvent>().When(x => !x.State.IsLocked).GoTo(...)` is called
- **THEN** one `EventHandler` is registered with `HasGuard = true` and a non-null combined guard delegate

#### Scenario: GoTo returns IStateConfig allowing chaining
- **WHEN** `.On<LockEvent>().GoTo(...).On<OpenEvent>().GoTo(...)` is called on the same state
- **THEN** both event handlers are registered without repeating `In<ClosedState>()`

---

### Requirement: Stay registers a no-transition action

`IEventConfig.Stay()` SHALL register an `EventHandler` whose action returns the current state
reference unchanged. An optional side-effect callback variant `Stay(action)` SHALL run the
callback then return the same state reference.

#### Scenario: Stay() registers EventHandler with identity action
- **WHEN** `In<ClosedState>().On<KnockEvent>().Stay()` is called
- **THEN** one `EventHandler` is registered with a non-null `Action` that returns `x.State` unchanged

#### Scenario: Stay(callback) registers EventHandler that runs callback
- **WHEN** `In<ClosedState>().On<KnockEvent>().Stay(x => Log("knock"))` is called
- **THEN** one `EventHandler` is registered with a non-null `Action` that runs the callback and returns `x.State`

---

### Requirement: OnEnter registers a state entry lifecycle handler

`IStateConfig<TCS>.OnEnter(callback)` SHALL register an entry callback for the current state.
Multiple `OnEnter` calls for the same `In<T>()` context SHALL accumulate; callbacks are chained
in declaration order and executed within a single `StateHandler`.

#### Scenario: OnEnter registers StateHandler with entry callback
- **WHEN** `In<ClosedState>().OnEnter(x => Log("entered"))` is called
- **THEN** one `StateHandler` is registered with `StateType = typeof(ClosedState)` and a non-null `OnEnter` delegate

---

### Requirement: OnExit registers a state exit lifecycle handler

`IStateConfig<TCS>.OnExit(callback)` SHALL register an exit callback for the current state.
Multiple `OnExit` calls for the same `In<T>()` context SHALL accumulate; callbacks are chained
in declaration order and executed within a single `StateHandler`.

#### Scenario: OnExit registers StateHandler with exit callback
- **WHEN** `In<ClosedState>().OnExit(x => Log("exited"))` is called
- **THEN** one `StateHandler` is registered with `StateType = typeof(ClosedState)` and a non-null `OnExit` delegate

---

### Requirement: Chained and disconnected styles produce identical registrations

Registering handlers via a continuous fluent chain SHALL produce the same `MachineDefinition`
as registering them via separate `c.In<T>()...` calls in any order.

#### Scenario: Chained style matches disconnected style
- **WHEN** two configurations register the same handlers — one chained, one via separate `c.In<T>()` calls
- **THEN** both produce `MachineDefinition` instances with the same handler count, types, and declaration order

---

### Requirement: Build() freezes the definition

`IMachineConfig.Build()` and `IStateConfig.Build()` SHALL convert all accumulated
`EventHandlerConfig` and `StateHandlerConfig` instances into frozen `EventHandler<C,S,E>` and
`StateHandler<C,S>` objects and return an immutable `MachineDefinition<C,S,E>`.

After `Build()` the definition SHALL be safe to share across concurrent executor instances
with no locking.

#### Scenario: Build returns immutable MachineDefinition
- **WHEN** `Build()` is called after registering handlers
- **THEN** a `MachineDefinition<C,S,E>` is returned with all handlers frozen as readonly fields

#### Scenario: Multiple Build() calls produce independent definitions
- **WHEN** `Build()` is called twice on the same configurator
- **THEN** two independent `MachineDefinition` instances are returned with independent caches

---

### Requirement: Interface default methods provide sync and Task overloads

`IEventConfig` and `IStateConfig` provide default method implementations for sync and `Task<>`
overloads — no separate extension class is needed. Guard chaining is done via multiple `.When()`
calls (AND semantics) rather than a separate `IGuardedEventConfig` interface:
- `When(Func<Activation<C,TCS,TCE>, bool>)` — sync guard; chains with prior `When()` via AND
- `When(Func<Activation<C,TCS,TCE>, Task<bool>>)` — Task guard
- `GoTo(Func<Activation<C,TCS,TCE>, TState>)` — sync action
- `GoTo(Func<Activation<C,TCS,TCE>, Task<TState>>)` — Task action
- `Stay(Action<Activation<C,TCS,TCE>>)` — sync side-effect shorthand

#### Scenario: Sync GoTo extension is accepted and wraps correctly
- **WHEN** `.GoTo(x => x.State with { IsLocked = true })` is used (sync return)
- **THEN** the registration succeeds and the stored action wraps the sync lambda in `ValueTask`

---

### Requirement: IMachineConfig exposes WithStateChangeIf for predicate configuration

`IMachineConfig` SHALL expose `WithStateChangeIf(Func<TState, TState, bool> predicate)` returning
`IMachineConfig` for fluent chaining. The predicate SHALL be copied into the `MachineDefinition`
at `Build()` time. Calling `WithStateChangeIf` more than once SHALL replace the previous predicate
(last write wins). If never called, the default predicate `!ReferenceEquals(s1, s2)` SHALL be used.

#### Scenario: WithStateChangeIf stores predicate in MachineDefinition
- **WHEN** `.WithStateChangeIf((s1, s2) => s1.GetType() != s2.GetType()).Build()` is called
- **THEN** the returned `MachineDefinition` uses that predicate for all executors created from it

#### Scenario: Default predicate is ReferenceEquals negation
- **WHEN** `Build()` is called without calling `WithStateChangeIf`
- **THEN** the `MachineDefinition` uses `!ReferenceEquals(s1, s2)` as the state-change predicate

#### Scenario: WithStateChangeIf returns IMachineConfig for chaining
- **WHEN** `.WithStateChangeIf(pred).In<SomeState>().On<SomeEvent>().GoTo(...)` is called
- **THEN** the chain compiles and both the predicate and the handler are registered
