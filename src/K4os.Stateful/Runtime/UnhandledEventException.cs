namespace K4os.Stateful.Runtime;

public sealed class UnhandledEventException: Exception
{
    public object Event { get; }
    public Type StateType { get; }

    public UnhandledEventException(object @event, Type stateType)
        : base($"No handler matched event '{@event.GetType().Name}' in state '{stateType.Name}'.")
    {
        Event = @event;
        StateType = stateType;
    }
}
