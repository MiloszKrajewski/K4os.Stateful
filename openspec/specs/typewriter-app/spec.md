# Spec: Typewriter App

## Purpose

Defines the project structure, input loop, key mapping, and application lifecycle for the typewriter demo console application. The app wires together the state machine and renderer into a runnable executable.

## Requirements

### Requirement: Project is a standalone console application

The demo SHALL be a separate `K4os.Stateful.TypewriterDemo` project with `OutputType = Exe` targeting `net8.0`. It SHALL reference `K4os.Stateful` via project reference and `Spectre.Console` via NuGet. It SHALL be added to `src/K4os.Stateful.sln`.

#### Scenario: Project builds as executable

- **WHEN** `dotnet build src/K4os.Stateful.sln` is run
- **THEN** the build succeeds and `K4os.Stateful.TypewriterDemo` produces an executable

---

### Requirement: Application reads individual keypresses without echo

The input loop SHALL use `Console.ReadKey(intercept: true)` so pressed keys are not echoed to the terminal. The application SHALL hide the cursor during operation (`Console.CursorVisible = false`).

#### Scenario: Characters are not echoed

- **WHEN** the user presses a key
- **THEN** the character does not appear at the default console cursor position (only in the rendered prompt)

---

### Requirement: Key-to-event mapping

The input loop SHALL map `ConsoleKey` values to `ITypewriterEvent` as follows:

| Key | State | Event sent |
|-----|-------|-----------|
| `Escape` | any | `UnjamEvent` |
| `Backspace` | any | `BackspaceEvent` |
| `Enter` | any | `EnterPressedEvent` |
| printable char | `TypingState` | `KeyPressedEvent(ch, DateTimeOffset.UtcNow)` |
| printable char | `JammedState` | ignored (no event sent) |
| `Ctrl+C` | any | exits application (not an event) |
| any other key | any | ignored (no event sent) |

The input loop SHALL NOT inspect `executor.State` when mapping keys to events. ESC always produces `UnjamEvent` regardless of current state. The state machine is responsible for deciding what `UnjamEvent` means in each state.

#### Scenario: ESC always sends UnjamEvent

- **WHEN** the user presses `Escape` in any state
- **THEN** `UnjamEvent` is dispatched to the executor

#### Scenario: Printable char while jammed is ignored

- **WHEN** `executor.State` is `JammedState` and user presses a letter key
- **THEN** no event is dispatched and the machine remains in `JammedState`

#### Scenario: Printable char while typing accumulates text

- **WHEN** `executor.State` is `TypingState` and user presses `'a'`
- **THEN** `KeyPressedEvent('a', now)` is dispatched with a fresh `DateTimeOffset.UtcNow`

---

### Requirement: Ctrl+C exits the application cleanly

The application SHALL register `Console.CancelKeyPress` to exit the `ReadKey` loop gracefully. On exit, cursor visibility SHALL be restored.

#### Scenario: Ctrl+C terminates the application

- **WHEN** the user presses `Ctrl+C`
- **THEN** the application exits and the terminal cursor is restored to visible

---

### Requirement: Render is called after every event dispatch

After each `TryFireAsync` call (regardless of whether an event was matched), the renderer SHALL be called to refresh the display. This ensures the display always reflects the current machine state.

#### Scenario: Display updates after each key

- **WHEN** the user presses any mapped key
- **THEN** the terminal display is redrawn before the next `ReadKey` call
