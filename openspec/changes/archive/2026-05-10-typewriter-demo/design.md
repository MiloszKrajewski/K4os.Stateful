## Context

`K4os.Stateful` has no end-to-end demo. The design doc (`docs/mechanicus-design.md`) uses a typing state machine as its motivating example, but it exists only as pseudocode. This change makes it real: a runnable console app that a developer can clone and immediately understand the library's value.

The library's core novelty is that **state carries data as fields** (records), not just a type label. The typewriter scenario exploits this: `TypingState.TextSoFar` is the text being typed, `TypingState.LastKeyAt` is the timestamp of the last arm-strike, and `JammedState.Parent` is the full state snapshot that gets restored on unjam. None of this data lives in a context object — it lives in the state, exactly as the library intends.

## Goals / Non-Goals

**Goals:**
- Demonstrate state-as-data: text accumulated in `TypingState.TextSoFar`, not in a mutable context field
- Demonstrate emergent transitions: jam is a guard on `KeyPressedEvent`, not a separate `JamEvent`
- Demonstrate nested state (data pattern): `JammedState(Parent)` restores full prior state on unjam
- Demonstrate base-type event matching: `In<JammedState>().On<ITypewriterEvent>().Stay()` as catch-all
- Produce a visually pleasant console app using Spectre.Console

**Non-Goals:**
- Persistence or serialization of typewriter state (the library supports it, but not demoed here)
- Audio feedback (typewriter sounds)
- Multi-line editing or cursor movement within the typing line
- Unit tests for the demo project

## Decisions

### D1 — Only 2 state types; no `IdleState`

**Decision:** `TypingState(string TextSoFar, DateTimeOffset LastKeyAt)` is the only non-jam state. "Idle" is `TypingState("")` — same type, empty text.

**Rationale:** A separate `IdleState` would add a third state type purely for structural symmetry. In the typewriter metaphor the paper is always loaded; you are always ready to type. The `LastKeyAt = default` (i.e. `DateTimeOffset.MinValue`) sentinel ensures the first key never triggers a jam — any real timestamp is years away from `MinValue`, so Δt ≫ 20 ms.

**Alternative considered:** Separate `IdleState` to demonstrate cross-type transitions. Rejected — two-state design is cleaner and still demonstrates cross-type transition when jamming (`TypingState` → `JammedState`) and when unjamming (`JammedState` → `TypingState`).

### D2 — Jam is a guard, not a separate event

**Decision:** Jamming is detected inside `TypingState.On<KeyPressedEvent>()` via a `When` guard:
```
WHEN Stopwatch.GetElapsedTime(x.State.LastKeyTick) <  x.Context.JamThresholdMs → JammedState(Parent: currentState)
WHEN Stopwatch.GetElapsedTime(x.State.LastKeyTick) >= x.Context.JamThresholdMs → TypingState { TextSoFar += Key, LastKeyTick = event.Timestamp }
```

`KeyPressedEvent` carries a `long Timestamp` captured via `Stopwatch.GetTimestamp()` at the moment of the keypress. `TypingState` stores `long LastKeyTick`. Elapsed time is computed with `Stopwatch.GetElapsedTime(startingTimestamp)` (.NET 7+), which uses `Stopwatch.Frequency` for sub-millisecond resolution.

**Rationale:** There is no "jam button" on a typewriter. The jam is a physical consequence of rapid key presses. Modelling it as an emergent guard keeps the state machine semantically faithful and showcases that guards can inspect `x.State` (LastKeyTick), `x.Event` (Timestamp), and `x.Context` (JamThresholdMs) — all three bundles — from a single lambda.

### D3 — Backspace does not update `LastKeyAt` (Case B)

**Decision:** `BackspaceEvent` has no timestamp and transition to `TypingState { TextSoFar[..^1] }` leaves `LastKeyTick` unchanged.

**Rationale:** A typewriter backspace key has no physical arm that could collide with another arm. Therefore it cannot participate in jam detection, and the tick for arm-collision timing is unaffected by a backspace press. This means pressing Backspace between two fast key presses does not reset the jam window — physically faithful.

### D4 — `JammedState` catch-all via base-type event

**Decision:**
```csharp
.In<JammedState>()
    .On<UnjamEvent>().GoTo(x => x.State.Parent)
    .On<ITypewriterEvent>().Stay()   // catch-all
```

**Rationale:** Showcases the library's polymorphic event dispatch. `On<ITypewriterEvent>` (interface, distance = 0 for all concrete events) ranks lower than `On<UnjamEvent>` (concrete class, distance = 0, class beats interface at same rank). The unjam rule fires for `UnjamEvent`; everything else silently stays.

**Alternative considered:** Enumerate all other event types explicitly. Rejected — verbose and misses the point of the dispatch system.

### D5 — ESC is context-sensitive: clear while typing, unjam while jammed

**Decision:** The input loop maps `ConsoleKey.Escape` to `UnjamEvent` when `executor.State is JammedState`, and to `EscapeEvent` (clear current line) when in `TypingState`. `Ctrl+C` exits the application.

**Rationale:** The input loop is a dumb key→event translator with no knowledge of the state machine's current state. ESC unconditionally produces `UnjamEvent`. In `JammedState` the machine restores `Parent`; in `TypingState` it `Stay()`s — a no-op. This keeps the input loop simple and the state machine fully responsible for event semantics.

**Key mapping table:**

| Key | State | Event sent |
|-----|-------|-----------|
| `Escape` | any | `UnjamEvent` |
| `Backspace` | any | `BackspaceEvent` |
| `Enter` | any | `EnterPressedEvent` |
| printable char | `TypingState` | `KeyPressedEvent(ch, DateTimeOffset.UtcNow)` |
| printable char | `JammedState` | ignored |
| `Ctrl+C` | any | exits application |
| any other | any | ignored |

### D6 — Rendering: `AnsiConsole.Live` with `ctx.UpdateTarget` for flicker-free updates

**Decision:** Wrap the entire `ReadKey` loop inside `AnsiConsole.Live(...).StartAsync(ctx => ...)`. After each event is processed, call `ctx.UpdateTarget(BuildRenderable(executor.State, context))` to replace the displayed renderable. `BuildRenderable` returns a composed `Rows` of `Markup` items and `Rule` dividers.

**Rationale:** Spectre.Console's `Live` display uses cursor-up ANSI escape sequences to overwrite the previous render in place — no screen clear, no flicker. `ctx.UpdateTarget` replaces the entire renderable on each keypress, which is simpler than mutating a shared table. The `Rows` compositor stacks: paper lines → `Rule` → prompt line → `Rule` → status line. Paper zone height is `Console.WindowHeight - 5`.

## Risks / Trade-offs

- **Jam threshold is configurable but not user-adjustable at runtime** → `JamThresholdMs` is set once at construction. Changing it live would require a new executor; acceptable for a demo.
- **`Stopwatch` resolution on Windows** → `Stopwatch.IsHighResolution` is `true` on all modern Windows systems (uses QPC, ~100 ns resolution). Sub-millisecond timing is reliable. No mitigation needed.
- **`AnsiConsole.Live` + `Console.ReadKey` interaction** → `Live` starts its own internal render context; `Console.ReadKey(true)` blocks inside it. This is supported — the render only happens on explicit `ctx.UpdateTarget` calls, not on a timer. No threading issues expected.
