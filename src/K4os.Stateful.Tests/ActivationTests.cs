namespace K4os.Stateful.Tests;

public class ActivationTests
{
    private class Ctx;
    private class State;
    private class Event;

    [Fact]
    public void Activation2_Properties_RoundTrip()
    {
        var ctx = new Ctx();
        var state = new State();
        var ct = new CancellationTokenSource().Token;

        var a = new Activation<Ctx, State>(ctx, state, ct);

        Assert.Same(ctx, a.Context);
        Assert.Same(state, a.State);
        Assert.Equal(ct, a.CancellationToken);
    }

    [Fact]
    public void Activation3_Properties_RoundTrip()
    {
        var ctx = new Ctx();
        var state = new State();
        var evt = new Event();
        var ct = new CancellationTokenSource().Token;

        var a = new Activation<Ctx, State, Event>(ctx, state, evt, ct);

        Assert.Same(ctx, a.Context);
        Assert.Same(state, a.State);
        Assert.Same(evt, a.Event);
        Assert.Equal(ct, a.CancellationToken);
    }

    [Fact]
    public void Activation2_DefaultCancellationToken_IsNone()
    {
        var a = new Activation<Ctx, State>(new Ctx(), new State(), CancellationToken.None);
        Assert.Equal(CancellationToken.None, a.CancellationToken);
    }
}
