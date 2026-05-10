## Why

States sometimes need to route themselves to a follow-on state immediately after being entered, without waiting for an external event — a completion transition. Without this, callers must fire a synthetic event after every transition just to advance through routing or initialisation states, adding ceremony and coupling.

## What Changes

- New `.Auto()` method on `IStateConfig<TCurrentState>` registers a completion handler for a state
- After `OnEnter` completes for a state, the executor checks for an `Auto` handler and fires it if present
- If `Auto` returns a different state reference, a full transition executes (OnExit → state update → OnEnter → Auto check again)
- If `Auto` returns the same reference, the chain stops (predicate suppresses transition)
- `Create()` does **not** trigger `Auto` — consistent with `OnEnter` not firing on `Start`
- `FireAsync` chains through as many Auto transitions as needed before returning to the caller

## Capabilities

### New Capabilities

- `auto-transitions`: The `.Auto()` DSL method and the executor loop that evaluates and chains automatic completion transitions after `OnEnter`

### Modified Capabilities

- `machine-executor`: Execution pipeline extended — after OnEnter, check for and run an Auto handler in a loop until no transition occurs
- `dsl-builder`: `IStateConfig<TCurrentState>` gains `.Auto()` and `StateHandlerConfig` gains an auto-handler accumulator; `StateHandler` gains an `Auto` delegate field

## Impact

- `Configuration/StateMachine.Interfaces.cs` — add `.Auto()` to `IStateConfig<TCurrentState>`
- `Configuration/StateConfigBuilder.cs` — implement `.Auto()`
- `Configuration/StateHandlerConfig.cs` — add `AutoHandler` accumulator
- `Runtime/StateHandler.cs` — add `Auto` delegate field
- `Runtime/MachineExecutor.cs` — extend `TryFireCoreAsync` with Auto loop after `OnEnter`
- `K4os.Stateful.Tests/ExecutorTests.cs` — new test cases for Auto behaviour
