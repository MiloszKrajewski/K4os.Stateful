using System.Diagnostics;
using K4os.Stateful;
using K4os.Stateful.Runtime;

namespace K4os.Stateful.TypewriterDemo;

// ── States ───────────────────────────────────────────────────────────────────

internal interface ITypewriterState;

// "Idle" is simply TypingState("") — no separate idle type.
// LastKeyTick = 0 (default) → Stopwatch.GetElapsedTime(0) ≫ any threshold → first key never jams.
internal record TypingState(string TextSoFar, long LastKeyTick = 0): ITypewriterState;

// Nested state: carries the full TypingState at the moment of jam for restoration.
internal record JammedState(ITypewriterState Parent): ITypewriterState;

// ── Events ───────────────────────────────────────────────────────────────────

internal interface ITypewriterEvent;

// Timestamp = Stopwatch.GetTimestamp() captured at keypress — used by jam guard.
internal record KeyPressedEvent(char Key, long Timestamp): ITypewriterEvent;

// No timestamp: backspace/enter have no arm; cannot contribute to jam detection.
internal record BackspaceEvent: ITypewriterEvent;

internal record EnterPressedEvent: ITypewriterEvent;

internal record UnjamEvent: ITypewriterEvent; // ESC key — sent unconditionally by input loop

// ── Context ──────────────────────────────────────────────────────────────────

internal class TypewriterContext
{
    public List<string> Lines { get; } = new();

    // Jam guard reads this from x.Context — demonstrates context access in guards.
    public TimeSpan JamThresholdMs { get; init; } = TimeSpan.FromMilliseconds(100);
}

// ── Machine definition ───────────────────────────────────────────────────────

internal static class TypewriterMachine
{
    public static MachineDefinition<TypewriterContext, ITypewriterState, ITypewriterEvent> BuildDefinition()
    {
        var sm = StateMachine.Configure<TypewriterContext, ITypewriterState, ITypewriterEvent>();

        // Jam: key arrived faster than threshold — guard reads x.State, x.Event, x.Context
        sm.In<TypingState>().On<KeyPressedEvent>()
            .When(x => Stopwatch.GetElapsedTime(x.State.LastKeyTick) < x.Context.JamThresholdMs)
            .GoTo(x => new JammedState(x.State));

        // slow typing - add next character
        sm.In<TypingState>().On<KeyPressedEvent>()
            .GoTo(x => new TypingState(TextSoFar: x.State.TextSoFar + x.Event.Key, LastKeyTick: x.Event.Timestamp));

        // Backspace with text — remove last char; LastKeyTick intentionally unchanged
        sm.In<TypingState>().On<BackspaceEvent>()
            .When(x => x.State.TextSoFar.Length > 0)
            .GoTo(x => x.State with { TextSoFar = x.State.TextSoFar[..^1] });

        // Entry with text — submit line, reset to empty
        sm.In<TypingState>().On<EnterPressedEvent>()
            .When(x => x.State.TextSoFar.Length > 0)
            .GoTo(x => {
                x.Context.Lines.Add(x.State.TextSoFar);
                return new TypingState("");
            });

        // fallback for "everything else" while in typing state
        sm.In<TypingState>().On<ITypewriterEvent>().Stay();

        // Jammed state: ESC unconditionally unjam
        sm.In<JammedState>()
            .On<UnjamEvent>().GoTo(x => x.State.Parent)
            .On<ITypewriterEvent>().Stay();

        return sm.Build();
    }
}
