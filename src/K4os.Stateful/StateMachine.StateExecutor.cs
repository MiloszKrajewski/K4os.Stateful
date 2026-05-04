using System;
using K4os.Stateful.Internal;

namespace K4os.Stateful;

public static partial class StateMachine<TContext, TState, TEvent>
{
    private class StateExecutor: IComparable<StateExecutor>
    {
        private readonly StateConfiguration _configuration;
        private readonly Type _type;
        private readonly int _distance;

        public StateExecutor(Type stateType, StateConfiguration data)
        {
            _configuration = data;
            _type = stateType;
            _distance = stateType.DistanceFrom(data.StateType);
        }

        public void Enter(TContext context, TState state)
        {
            var enter = _configuration.OnEnter;
            enter?.Invoke(context, state);
        }

        public void Exit(TContext context, TState state)
        {
            var exit = _configuration.OnExit;
            exit?.Invoke(context, state);
        }

        public int CompareTo(StateExecutor? other) =>
            other is null ? 1 : _distance.CompareTo(other._distance);

        public override string ToString() =>
            $"StateExecutor(Type:{_type.Name}({_distance}), {_configuration})";
    }
}
