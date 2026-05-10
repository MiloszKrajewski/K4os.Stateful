using Spectre.Console;
using Spectre.Console.Rendering;

namespace K4os.Stateful.TypewriterDemo;

static class Renderer
{
    private static readonly Style GreyStyle = new(Color.Grey);

    public static IRenderable BuildRenderable(ITypewriterState state, TypewriterContext ctx)
    {
        var items = new List<IRenderable>();

        // ── Paper zone ───────────────────────────────────────────────────────
        // Fill from top with blank lines, then show the most recent submissions.
        var paperHeight = Math.Max(1, Console.WindowHeight - 5);
        var lines = ctx.Lines.TakeLast(paperHeight).ToList();

        for (var i = lines.Count; i < paperHeight; i++)
            items.Add(new Markup(""));

        foreach (var line in lines)
            items.Add(new Markup($"  [grey]📜  {Markup.Escape(line)}[/]"));

        // ── Top rule ─────────────────────────────────────────────────────────
        items.Add(new Rule { Style = GreyStyle });

        // ── Typing prompt ─────────────────────────────────────────────────────
        items.Add(BuildPrompt(state));

        // ── Bottom rule ───────────────────────────────────────────────────────
        items.Add(new Rule { Style = GreyStyle });

        // ── Status bar ────────────────────────────────────────────────────────
        items.Add(BuildStatus(state));

        return new Rows(items);
    }

    private static Markup BuildPrompt(ITypewriterState state) =>
        state switch {
            TypingState t => new Markup($"  [white bold]⌨  {Markup.Escape(t.TextSoFar)}[/][white]_[/]"),
            JammedState { Parent: TypingState p } => new Markup(
                $"  [red bold]⚠  {Markup.Escape(p.TextSoFar)}   *** JAM ***[/]"),
            _ => new Markup(""),
        };

    private static Markup BuildStatus(ITypewriterState state)
    {
        var (label, count, hints) = state switch {
            TypingState t => ("TYPING", t.TextSoFar.Length, "[dim]ENTER[/] submit  [dim]BACKSPACE[/] delete"),
            JammedState { Parent: TypingState p } => ("JAMMED", p.TextSoFar.Length, "[dim]ESC[/] to unjam"),
            _ => ("?", 0, ""),
        };

        var stateMarkup = state is JammedState ? "[red bold]" : "[green bold]";
        return new Markup($"  {stateMarkup}[[{label}]][/]  [dim]{count} chars[/]  [grey]│[/]  {hints}");
    }
}
