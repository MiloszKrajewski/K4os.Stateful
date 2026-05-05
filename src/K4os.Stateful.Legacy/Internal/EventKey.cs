using System;

namespace K4os.Stateful.Legacy.Internal;

internal readonly struct EventKey: IEquatable<EventKey>
{
	private readonly Type _stateType;
	private readonly Type _eventType;

	public EventKey(Type stateType, Type eventType)
	{
		_stateType = stateType.EnsureNotNull("stateType");
		_eventType = eventType.EnsureNotNull("eventType");
	}

	public Type StateType => _stateType;
	public Type EventType => _eventType;

	/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
	/// <returns>true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.</returns>
	/// <param name="other">An object to compare with this object.</param>
	public bool Equals(EventKey other) => 
		_stateType == other._stateType && _eventType == other._eventType;

	/// <summary>Indicates whether this instance and a specified object are equal.</summary>
	/// <param name="other">Another object to compare to.</param>
	/// <returns>true if <paramref name="other" /> and this instance are the same type and represent the same value; otherwise, false.</returns>
	public override bool Equals(object? other) => 
		other is EventKey key && Equals(key);

	/// <summary>Returns the hash code for this instance.</summary>
	/// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
	public override int GetHashCode() =>
		HashCode.Combine(_stateType, _eventType);

	/// <summary>Returns the fully qualified type name of this instance.</summary>
	/// <returns>A <see cref="T:System.String"/> containing a fully qualified type name.</returns>
	public override string ToString() => $"EventKey({_stateType.Name}, {_eventType.Name})";
}