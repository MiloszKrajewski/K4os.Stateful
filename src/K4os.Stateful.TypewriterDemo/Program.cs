using System.Diagnostics;
using K4os.Stateful.TypewriterDemo;
using Spectre.Console;

var ctx = new TypewriterContext();
var executor = TypewriterMachine.BuildDefinition().Create(ctx, new TypingState(""));

Console.CursorVisible = false;
// TreatControlCAsInput makes ReadKey return on Ctrl+C (ConsoleKey.C + Control modifier)
// rather than raising CancelKeyPress, so the loop can exit cleanly without polling.
Console.TreatControlCAsInput = true;

try
{
    await AnsiConsole.Live(Renderer.BuildRenderable(executor.State, ctx))
        .StartAsync(async liveCtx => {
            liveCtx.UpdateTarget(Renderer.BuildRenderable(executor.State, ctx));

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    break;

                ITypewriterEvent? evt = key.Key switch {
                    ConsoleKey.Escape => new UnjamEvent(),
                    ConsoleKey.Backspace => new BackspaceEvent(),
                    ConsoleKey.Enter => new EnterPressedEvent(),
                    _ when !char.IsControl(key.KeyChar) => new KeyPressedEvent(key.KeyChar, Stopwatch.GetTimestamp()),
                    _ => null,
                };

                if (evt is not null)
                    await executor.TryFireAsync(evt);

                liveCtx.UpdateTarget(Renderer.BuildRenderable(executor.State, ctx));
            }
        });
}
finally
{
    Console.TreatControlCAsInput = false;
    Console.CursorVisible = true;
}
