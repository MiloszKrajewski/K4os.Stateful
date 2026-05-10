K4os.Stateful
===
**Typed, async-native state machine library for .NET**

## Why

Most .NET state machine libraries model state as an enum — a label with no data. This forces actual state data to live outside the machine, defeating the point of structured state management.

K4os.Stateful treats state and events as **plain .NET objects**:

- State is a typed record/class that carries data
- Events carry typed payloads — no boxing
- Transitions are async-native (`ValueTask`)
- Handlers match polymorphically via class inheritance — a handler on `Animal` fires for `Dog : Animal`
- Side effects go through an injected **context** object; the library has no output channel

## Quick start

```csharp
// States
interface IDoorState { }
record ClosedState(bool IsLocked) : IDoorState;
record OpenState : IDoorState;

// Events
interface IDoorEvent { }
record OpenEvent : IDoorEvent;
record CloseEvent : IDoorEvent;
record LockEvent : IDoorEvent;
record UnlockEvent : IDoorEvent;

// Context — injected services
class DoorContext
{
    public ILogger Logger { get; init; } = null!;
}

// Configure once — frozen, thread-safe definition
var definition = StateMachine.Configure<DoorContext, IDoorState, IDoorEvent>()
    .In<ClosedState>()
        .On<OpenEvent>()
            .When(x => !x.State.IsLocked)
            .GoTo(x => new OpenState())
        .On<LockEvent>()
            .When(x => !x.State.IsLocked)
            .GoTo(x => x.State with { IsLocked = true })
        .On<UnlockEvent>()
            .When(x => x.State.IsLocked)
            .GoTo(x => x.State with { IsLocked = false })
    .In<OpenState>()
        .On<CloseEvent>()
            .GoTo(x => new ClosedState(IsLocked: false))
    .Build();

// Create one executor per entity — shares the frozen definition
var executor = definition.Create(new DoorContext { Logger = logger }, new ClosedState(false));

await executor.FireAsync(new LockEvent());
await executor.FireAsync(new OpenEvent());    // throws UnhandledEventException (locked)

bool ok = await executor.TryFireAsync(new OpenEvent());  // returns false instead of throwing
```

## Concepts

### States and events are objects

States and events are plain C# records or classes. They carry data as properties. No enums, no string labels.

```csharp
record IdleState : IState;
record ProcessingState(string JobId, int Retries) : IState;
record DoneState(string Result) : IState;
```

Transitions can return a **different type** — discriminated-union style:

```csharp
.GoTo(x => new DoneState(result))  // cross-type transition: ProcessingState → DoneState
```

### Context

Context is a shared object injected once per executor — your integration point with the outer world (logger, message bus, database, etc.). The library never inspects it.

```csharp
.GoTo(async x =>
{
    await x.Context.Bus.Publish(new JobCompleted(x.State.JobId), x.CancellationToken);
    return new DoneState(result);
})
```

### Hierarchical matching

Handlers match by inheritance distance. The most-derived match wins:

```csharp
c.In<IDoorState>().On<IDoorEvent>()   // distance > 0 — catch-all fallback
    .Stay(x => x.Context.Logger.LogDebug("unhandled {E}", x.Event));

c.In<ClosedState>().On<OpenEvent>()   // distance 0 — takes priority over above
    .GoTo(x => new OpenState());
```

This works for both state and event types independently. A handler on `IState` fires for any concrete state; a handler on `IDoorEvent` fires for any door event.

### Entry and exit hooks

`OnEnter`/`OnExit` fire for **every type in the inheritance chain** that has a registered handler. Entry fires base → derived; exit fires derived → base.

```csharp
c.In<IDoorState>()
    .OnEnter(x => x.Context.Logger.LogDebug("entering a door state"));  // fires for all door states

c.In<ClosedState>()
    .OnEnter(x => x.Context.Logger.LogDebug("entering closed"))   // fires after the above
    .OnExit(x => x.Context.Logger.LogDebug("leaving closed"));
```

### Guard evaluation

`.When(...)` filters handlers. Multiple `.When()` calls on the same handler combine with AND logic. The first handler whose guard passes fires; the rest are skipped. A handler with no `.When()` acts as an unguarded fallback.

```csharp
c.In<ClosedState>().On<OpenEvent>()
    .When(x => !x.State.IsLocked)
    .When(x => x.State.OpenCount < 100)   // AND with above
    .GoTo(x => new OpenState())
.On<OpenEvent>()                          // fallback (no When)
    .Stay(x => x.Context.Logger.LogWarning("door cannot be opened"));
```

### State-change predicate

Entry/exit hooks fire when the state-change predicate returns `true` for `(previousState, nextState)`. The default is `!ReferenceEquals` — so `.Stay()` (returns the same reference) suppresses hooks, while `GoTo(x => x.State with { })` (new record instance) triggers them.

```csharp
StateMachine.Configure<Ctx, IState, IEvent>()
    .WithStateChangeIf((s1, s2) => s1.GetType() != s2.GetType())  // fire only on type change
    // ...
```

### Executor lifecycle

One frozen `MachineDefinition` → many `MachineExecutor` instances (one per entity, session, etc.).

```csharp
// Fresh start
var executor = definition.Create(context, new IdleState());

// Restore from persisted state — identical API
var executor = definition.Create(context, JsonSerializer.Deserialize<IState>(saved)!);
```

`Create` does **not** fire `OnEnter`. The executor is being positioned at a state, not transitioning into it.

### Serialization

The library holds no non-serializable state. `executor.State` returns the raw state object; the caller owns persistence:

```csharp
string json = JsonSerializer.Serialize(executor.State);
// later:
var state = JsonSerializer.Deserialize<IState>(json)!;
var executor = definition.Create(context, state);
```

## DSL reference

### `IMachineConfig`

| Method | Description |
|--------|-------------|
| `.In<TState>()` | Open a state scope |
| `.WithStateChangeIf(pred)` | Override the state-change predicate |
| `.Build()` | Freeze and return `MachineDefinition<C,S,E>` |

### `IStateConfig<TState>`

| Method | Description |
|--------|-------------|
| `.OnEnter(callback)` | Register entry hook |
| `.OnExit(callback)` | Register exit hook |
| `.On<TEvent>()` | Open an event scope |
| `.In<TState>()` | Pivot to a different state scope |
| `.Build()` | Freeze the definition |

### `IEventConfig<TState, TEvent>`

| Method | Description |
|--------|-------------|
| `.When(guard)` | Add a guard (AND-accumulated) |
| `.GoTo(fn)` | Transition to a new state; returns state scope for chaining |
| `.Stay()` | No state change; suppress entry/exit |
| `.Stay(callback)` | No state change; run a side-effect callback |

All callbacks accept `ValueTask`, `Task`, or synchronous forms.

### Activation bundles

| Type | Available in | Properties |
|------|-------------|------------|
| `Activation<TContext, TState>` | `OnEnter`, `OnExit` | `.Context`, `.State`, `.CancellationToken` |
| `Activation<TContext, TState, TEvent>` | `When`, `GoTo`, `Stay` | + `.Event` |

## Exceptions

| Exception | When |
|-----------|------|
| `UnhandledEventException` | `FireAsync` finds no matching handler |
| `ConcurrentFireException` | `FireAsync` called while another fire is in progress |
| `IncompleteEventHandlerException` | `Build()` called with an `.On<E>()` that has no `.GoTo()`/`.Stay()` |

`TryFireAsync` returns `false` instead of throwing `UnhandledEventException`.

## Handler priority

When multiple handlers could match, the ranking is (ascending = higher priority):

1. State type distance (most derived wins)
2. Class over interface at same distance
3. Event type distance (most derived wins)
4. Class over interface at same distance
5. Guarded before unguarded (`.When(...)` present)
6. Declaration order (stable tie-break)
