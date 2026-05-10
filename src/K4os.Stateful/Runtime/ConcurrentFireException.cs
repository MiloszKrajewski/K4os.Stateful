namespace K4os.Stateful.Runtime;

public sealed class ConcurrentFireException: Exception
{
    public ConcurrentFireException():
        base("A FireAsync call is already in progress on this executor.") { }
}
