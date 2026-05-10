namespace K4os.Stateful.Runtime;

public sealed class IncompleteEventHandlerException: Exception
{
    public Type StateType { get; }
    public Type EventType { get; }

    public IncompleteEventHandlerException(Type stateType, Type eventType): 
        base($"Event handler for '{stateType.Name}' on '{eventType.Name}' has no transition — call GoTo() or Stay() to complete it.")
    {
        StateType = stateType;
        EventType = eventType;
    }
}
