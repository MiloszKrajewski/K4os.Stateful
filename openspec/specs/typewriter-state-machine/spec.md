# Spec: Typewriter State Machine

## Purpose

Defines the states, events, context, and transition logic for the typewriter demo state machine. Demonstrates typed state records, guard conditions with context access, polymorphic event dispatch, and a catch-all jammed state.

## Requirements

### Requirement: State types are typed records with data fields

The state machine SHALL define exactly two state types, both implementing `ITypewriterState`:
- `TypingState(string TextSoFar, long LastKeyTick = 0)` — the text typed so far and the `Stopwatch.GetTimestamp()` tick of the last key press
- `JammedState(ITypewriterState Parent)` — holds the full prior state snapshot for restoration

`LastKeyTick` SHALL default to `0`. `Stopwatch.GetElapsedTime(0)` returns the time since system boot, which is always far greater than any jam threshold, so the first keypress can never trigger a jam.

#### Scenario: TypingState holds accumulated text

- **WHEN** a `TypingState` is constructed with `TextSoFar = "hello"`
- **THEN** `state.TextSoFar` returns `"hello"`

#### Scenario: JammedState holds parent reference

- **WHEN** a `JammedState` is constructed with a `TypingState("hi", t)` as `Parent`
- **THEN** `state.Parent` is that exact `TypingState` instance

---

### Requirement: KeyPressedEvent carries key character and timestamp

`KeyPressedEvent(char Key, long Timestamp)` SHALL carry both the character pressed and a `Stopwatch.GetTimestamp()` tick captured at the moment of the press. The tick is used by the jam guard.

`BackspaceEvent`, `EnterPressedEvent`, and `UnjamEvent` SHALL carry no timestamp (they cannot cause jams). There is no `EscapeEvent` — ESC has no physical typewriter analog while typing and is handled entirely at the input-loop level (mapped to `UnjamEvent` unconditionally).

#### Scenario: KeyPressedEvent exposes Key and Timestamp

- **WHEN** `new KeyPressedEvent('a', t)` is constructed
- **THEN** `event.Key == 'a'` and `event.Timestamp == t`

---

### Requirement: Context carries jam threshold configuration

`TypewriterContext` SHALL expose `TimeSpan JamThresholdMs` — the maximum inter-key interval that triggers a jam. It SHALL default to `TimeSpan.FromMilliseconds(100)`. The jam guard reads this value from `x.Context`, demonstrating that guards have full access to context alongside state and event.

`TypewriterContext` SHALL also carry `List<string> Lines` — the submitted lines shown in the paper zone.

#### Scenario: Default threshold is 100 ms

- **WHEN** a `TypewriterContext` is constructed with no arguments
- **THEN** `context.JamThresholdMs == TimeSpan.FromMilliseconds(100)`

#### Scenario: Custom threshold is respected

- **WHEN** a `TypewriterContext` is constructed with `JamThresholdMs = TimeSpan.FromMilliseconds(50)`
- **AND** a key is pressed 30 ms after the previous one
- **THEN** the machine jams (30 ms < 50 ms threshold)

---

### Requirement: Normal key press accumulates text and updates tick

When in `TypingState` and a `KeyPressedEvent` arrives with `Stopwatch.GetElapsedTime(LastKeyTick) >= context.JamThresholdMs`, the machine SHALL transition to a new `TypingState` with the key appended to `TextSoFar` and `LastKeyTick` updated to `event.Timestamp`.

The jam threshold is read from `context.JamThresholdMs` — a configurable value on `TypewriterContext`. This demonstrates that guards have access to context, not just state and event.

#### Scenario: First key press from default state never jams

- **WHEN** the machine is in `TypingState("", 0)`
- **AND** a `KeyPressedEvent('a', Stopwatch.GetTimestamp())` arrives
- **THEN** elapsed since tick 0 is seconds/minutes → far above any threshold → machine transitions to `TypingState("a", event.Timestamp)` without jamming

#### Scenario: Slow successive keys accumulate

- **WHEN** the machine is in `TypingState("h", tick0)`
- **AND** a `KeyPressedEvent('i', tick0 + ~50ms worth of ticks)` arrives
- **THEN** the machine transitions to `TypingState("hi", event.Timestamp)`

---

### Requirement: Rapid successive key press jams the machine

When in `TypingState` and a `KeyPressedEvent` arrives with `Stopwatch.GetElapsedTime(LastKeyTick) < context.JamThresholdMs`, the machine SHALL transition to `JammedState(Parent: currentTypingState)`. The current `TypingState` (with its accumulated text) is stored as `Parent`.

#### Scenario: Jam triggered by rapid successive keys

- **WHEN** the machine is in `TypingState("h", tick0)`
- **AND** a `KeyPressedEvent('i', tick0 + ~5ms worth of ticks)` arrives with elapsed < threshold
- **THEN** the machine transitions to `JammedState(Parent: TypingState("h", tick0))`

#### Scenario: Text at time of jam is preserved in Parent

- **WHEN** the machine jams from `TypingState("hello", t)`
- **THEN** `executor.State` is `JammedState` with `Parent.TextSoFar == "hello"`

---

### Requirement: Backspace removes last character without updating LastKeyAt

When in `TypingState` with non-empty `TextSoFar`, a `BackspaceEvent` SHALL transition to `TypingState { TextSoFar = TextSoFar[..^1] }` leaving `LastKeyAt` unchanged. When `TextSoFar` is empty, `BackspaceEvent` SHALL result in `Stay()`.

#### Scenario: Backspace removes last character

- **WHEN** the machine is in `TypingState("hi", t0)`
- **AND** a `BackspaceEvent` arrives
- **THEN** the machine transitions to `TypingState("h", t0)` (LastKeyAt unchanged)

#### Scenario: Backspace on empty text is a no-op

- **WHEN** the machine is in `TypingState("", t0)`
- **AND** a `BackspaceEvent` arrives
- **THEN** the machine remains in `TypingState("", t0)` (same reference, no entry/exit)

#### Scenario: Backspace does not reset jam clock (Case B)

- **WHEN** the machine is in `TypingState("a", tick0)`
- **AND** a `BackspaceEvent` arrives (LastKeyTick stays tick0)
- **AND** then a `KeyPressedEvent('b', tick0 + ~8ms)` arrives
- **THEN** elapsed since tick0 ≈ 8ms < threshold → machine jams

---

### Requirement: Enter submits text and resets to empty TypingState

When in `TypingState` with non-empty `TextSoFar`, `EnterPressedEvent` SHALL add `TextSoFar` to `context.Lines` and transition to `TypingState("", default)`. When `TextSoFar` is empty, `EnterPressedEvent` SHALL result in `Stay()`.

#### Scenario: Enter submits non-empty text

- **WHEN** the machine is in `TypingState("hello", t)`
- **AND** an `EnterPressedEvent` arrives
- **THEN** `context.Lines` contains `"hello"` and machine transitions to `TypingState("", default)`

#### Scenario: Enter on empty text is a no-op

- **WHEN** the machine is in `TypingState("", t)`
- **AND** an `EnterPressedEvent` arrives
- **THEN** `context.Lines` is unchanged and machine stays in `TypingState("", t)`

---

### Requirement: UnjamEvent while typing is a no-op

When in `TypingState`, `UnjamEvent` SHALL result in `Stay()` — the machine is not jammed, so unjam is meaningless. This keeps the input loop state-unaware: ESC always sends `UnjamEvent`; the state machine decides the consequence.

#### Scenario: UnjamEvent while typing does nothing

- **WHEN** the machine is in `TypingState("hello", t)`
- **AND** a `UnjamEvent` arrives
- **THEN** the machine remains in `TypingState("hello", t)` unchanged (same reference)

---

### Requirement: UnjamEvent restores prior TypingState

When in `JammedState`, `UnjamEvent` SHALL transition to `state.Parent`, restoring the exact `TypingState` (including `TextSoFar` and `LastKeyAt`) that was active at the moment of jamming.

#### Scenario: Unjam restores text and timestamp

- **WHEN** the machine is in `JammedState(Parent: TypingState("hello", t0))`
- **AND** a `UnjamEvent` arrives
- **THEN** machine transitions to `TypingState("hello", t0)` (text and timestamp restored)

---

### Requirement: All other events while jammed are silently ignored

When in `JammedState`, any `ITypewriterEvent` other than `UnjamEvent` SHALL result in `Stay()` via base-type catch-all dispatch. The machine SHALL remain in `JammedState`.

#### Scenario: Key press while jammed is ignored

- **WHEN** the machine is in `JammedState(...)`
- **AND** a `KeyPressedEvent` arrives
- **THEN** machine remains in `JammedState` (same reference)

#### Scenario: Backspace while jammed is ignored

- **WHEN** the machine is in `JammedState(...)`
- **AND** a `BackspaceEvent` arrives
- **THEN** machine remains in `JammedState` (same reference)
