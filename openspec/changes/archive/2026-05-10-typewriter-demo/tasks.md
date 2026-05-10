## 1. Project Scaffold

- [x] 1.1 Create `src/K4os.Stateful.TypewriterDemo/K4os.Stateful.TypewriterDemo.csproj` (OutputType=Exe, net8.0, refs K4os.Stateful + Spectre.Console)
- [x] 1.2 Add `K4os.Stateful.TypewriterDemo` to `src/K4os.Stateful.sln`
- [x] 1.3 Verify `dotnet build src/K4os.Stateful.sln` succeeds with empty `Program.cs`

## 2. State Machine (`Typewriter.cs`)

- [x] 2.1 Define `ITypewriterState` marker interface and `ITypewriterEvent` marker interface
- [x] 2.2 Define `TypingState(string TextSoFar, long LastKeyTick = 0)` record (`long` is `Stopwatch.GetTimestamp()` tick; default 0 → first key never jams)
- [x] 2.3 Define `JammedState(ITypewriterState Parent)` record
- [x] 2.4 Define all event records: `KeyPressedEvent(char Key, long Timestamp)`, `BackspaceEvent`, `EnterPressedEvent`, `UnjamEvent` (no `EscapeEvent`)
- [x] 2.5 Define `TypewriterContext` with `List<string> Lines` and `double JamThresholdMs { get; init; } = 20.0`
- [x] 2.6 Implement `BuildDefinition()` — `TypingState` transitions: jam guard (`Stopwatch.GetElapsedTime(x.State.LastKeyTick) < TimeSpan.FromMilliseconds(x.Context.JamThresholdMs)` → JammedState), normal key (elapsed ≥ threshold → append + update LastKeyTick), Backspace (len > 0 → delete leaving LastKeyTick unchanged, len == 0 → Stay), Enter (len > 0 → submit + reset, len == 0 → Stay), UnjamEvent → Stay()
- [x] 2.7 Implement `BuildDefinition()` — `JammedState` transitions: `UnjamEvent` → `GoTo(x => x.State.Parent)`, `ITypewriterEvent` catch-all → `Stay()`

## 3. Renderer (`Renderer.cs`)

- [x] 3.1 Create `BuildRenderable(ITypewriterState state, TypewriterContext ctx)` returning `Rows` — compose paper Markup items + Rule + prompt Markup + Rule + status Markup
- [x] 3.2 Implement paper zone items: last N submitted lines with `📜` icon and `[grey]` markup; N = `Console.WindowHeight - 5`
- [x] 3.3 Implement prompt Markup: normal `⌨  {text}_` in `[white bold]`; jammed `⚠  {text}   *** JAM ***` in `[red bold]`; Rules use `.RuleStyle("grey")`
- [x] 3.4 Implement status Markup with `[dim]` — state name, char count, keybinding hints (ESC: clear/unjam depending on state; ENTER: submit; BACKSPACE: delete)
- [x] 3.5 Escape all user-supplied text through `Markup.Escape(...)` before embedding in any markup string

## 4. Entry Point (`Program.cs`)

- [x] 4.1 Wire `TypewriterContext`, `BuildDefinition()`, create executor at `TypingState("", default)`
- [x] 4.2 Set `Console.CursorVisible = false`; register `Console.CancelKeyPress` to set a cancellation flag and restore cursor on exit
- [x] 4.3 Wrap input loop in `AnsiConsole.Live(BuildRenderable(executor.State, ctx)).StartAsync(async liveCtx => ...)`
- [x] 4.4 Implement `ReadKey` loop inside Live: map `ConsoleKey` to `ITypewriterEvent?` — no state inspection (ESC→UnjamEvent; ENTER→EnterPressedEvent; BACKSPACE→BackspaceEvent; printable char→`new KeyPressedEvent(ch, Stopwatch.GetTimestamp())`; else null)
- [x] 4.5 Call `await executor.TryFireAsync(evt)` when event is non-null, then always call `liveCtx.UpdateTarget(BuildRenderable(executor.State, ctx))`

## 5. Verification

- [x] 5.1 `dotnet build src/K4os.Stateful.sln` passes with no warnings
- [x] 5.2 Run `dotnet run --project src/K4os.Stateful.TypewriterDemo` — type slowly, verify text accumulates
- [x] 5.3 Trigger a jam by pressing two keys in rapid succession — verify `⚠ JAM` display and text preserved
- [x] 5.4 Press ESC while jammed — verify unjam and text restored
- [x] 5.5 Submit a line (ENTER on non-empty text) — verify line appears in paper zone and prompt clears
- [x] 5.6 Press ESC while typing — verify Stay() (no state change, no crash; input loop sends UnjamEvent unconditionally)
- [x] 5.7 Backspace on empty text — verify no crash, no change
- [x] 5.8 Press Ctrl+C — verify clean exit and cursor restored
