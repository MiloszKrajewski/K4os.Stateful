## Why

The `K4os.Stateful` library needs a compelling real-world demo that shows its core value proposition — states as typed data objects, not enum labels — in an immediately tangible way. A typewriter simulation does this: the text being typed lives *in* the state, jam detection is an emergent guard reading timestamps from both state and event, and nested state (`JammedState` carrying its `Parent`) demonstrates data-driven state restoration.

## What Changes

- New console application project `K4os.Stateful.TypewriterDemo` added to the solution
- Uses Spectre.Console for rendering: two horizontal rules frame the typing area, colors and icons for personality, no heavy box-drawing frames
- Implements a two-state typewriter machine (`TypingState`, `JammedState`) with mechanically faithful jam logic: two keys pressed within 20 ms jam the typewriter arms
- Submitted lines scroll in a "paper" zone above the typing area; status bar below shows state name, char count, and keybindings

## Capabilities

### New Capabilities

- `typewriter-state-machine`: The state machine definition — states, events, context, transition rules, and the `BuildDefinition()` factory
- `typewriter-renderer`: Console rendering using Spectre.Console — paper zone, typing prompt between two `Rule` dividers, status bar
- `typewriter-app`: Entry point — `ReadKey` loop, key-to-event mapping, wires machine and renderer together

### Modified Capabilities

_(none — this is a new standalone demo project; no existing specs change)_

## Impact

- New project `src/K4os.Stateful.TypewriterDemo/` added to `src/K4os.Stateful.sln`
- Adds `Spectre.Console` NuGet dependency (demo project only; not in library or tests)
- No changes to `K4os.Stateful`, `K4os.Stateful.Legacy`, or any test project
