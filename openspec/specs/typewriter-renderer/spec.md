# Spec: Typewriter Renderer

## Purpose

Defines the terminal rendering layout and update strategy for the typewriter demo. Uses Spectre.Console's `AnsiConsole.Live` for flicker-free in-place updates without screen clearing.

## Requirements

### Requirement: Display is divided into three zones

The renderer SHALL divide the terminal into three zones rendered top-to-bottom:
1. **Paper zone** — submitted lines, scrolling; fills available height
2. **Typing zone** — a horizontal rule, the typing prompt line, a horizontal rule
3. **Status zone** — one line with state name, character count, and keybinding hints

No box-drawing borders or panels SHALL wrap the layout. The two `Rule` dividers around the typing prompt are the only structural chrome.

#### Scenario: Layout zones are rendered in correct order

- **WHEN** the renderer draws any state
- **THEN** submitted lines appear above the top Rule, the prompt appears between the two Rules, and the status line appears below the bottom Rule

---

### Requirement: Paper zone shows last N submitted lines

The paper zone height SHALL be `Console.WindowHeight - 5` lines (Rule + prompt + Rule + status + 1 margin). Only the most recent submitted lines SHALL be shown; older lines scroll off the top. Each line SHALL be rendered with a paper icon prefix (e.g. `📜`) and dimmed styling.

#### Scenario: Submitted lines appear in order

- **WHEN** three lines have been submitted: "Hello", "World", "!"
- **THEN** they appear top-to-bottom in that order in the paper zone

#### Scenario: Paper zone truncates to last N lines

- **WHEN** more lines are submitted than the paper zone height
- **THEN** only the most recent N lines are shown; older lines are no longer visible

---

### Requirement: Typing zone shows current input with cursor

The typing prompt SHALL show the icon `⌨` followed by `TextSoFar` and a cursor character (`_`). When in `JammedState`, the prompt SHALL show `⚠` followed by the parent's `TextSoFar` (text is preserved), followed by a `*** JAM ***` label. Both prompt variants SHALL use distinct colors:
- Normal typing: white/bold text
- Jammed: red/bold text with warning styling

#### Scenario: Normal typing prompt shows current text

- **WHEN** the machine is in `TypingState("hello", t)`
- **THEN** the prompt line shows `⌨  hello_` (or equivalent with icon and cursor)

#### Scenario: Jammed prompt shows preserved text with jam indicator

- **WHEN** the machine is in `JammedState(Parent: TypingState("hello", t))`
- **THEN** the prompt shows `⚠  hello   *** JAM ***` in red

---

### Requirement: Status zone reflects current machine state

The status line SHALL display:
- Current state name (`TYPING` or `JAMMED`)
- Character count of current text
- Relevant keybinding hints (differ by state)

In `TypingState`: `ENTER` to submit, `BACKSPACE` to delete.
In `JammedState`: `ESC` to unjam.

The status line SHALL use dimmed styling to recede visually.

#### Scenario: Status shows correct state name and char count

- **WHEN** the machine is in `TypingState("hello", t)`
- **THEN** status shows `TYPING` and `5 chars`

#### Scenario: Status shows jammed hints when jammed

- **WHEN** the machine is in `JammedState(...)`
- **THEN** status shows `JAMMED` and hint for unjamming (ESC)

---

### Requirement: Flicker-free updates via AnsiConsole.Live

The renderer SHALL use `AnsiConsole.Live(...).StartAsync(ctx => ...)` to wrap the input loop. After each keypress, `ctx.UpdateTarget(BuildRenderable(state, ctx))` SHALL replace the displayed renderable in place using cursor-up ANSI sequences — no screen clear, no flicker.

`BuildRenderable` SHALL return a composed `Rows` renderable stacking: paper `Markup` items → `Rule` → prompt `Markup` → `Rule` → status `Markup`.

#### Scenario: Display updates without clearing the screen

- **WHEN** the user presses a key and the state changes
- **THEN** the display is updated in place without a visible flash or blank frame

#### Scenario: BuildRenderable is pure

- **WHEN** `BuildRenderable` is called twice with identical state and context
- **THEN** the returned renderables produce identical output
