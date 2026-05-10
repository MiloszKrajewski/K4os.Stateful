## 1. New Runtime Type

- [x] 1.1 Create `src/K4os.Stateful/Runtime/IncompleteEventHandlerException.cs` with `StateType` and `EventType` properties

## 2. Rework StateHandlerConfig

- [x] 2.1 Add `List<EventHandlerConfig> EventHandlers` property to `StateHandlerConfig`
- [x] 2.2 Move `Combine()` static helper from `StateConfigBuilder` into `StateHandlerConfig` as an instance method `AddOnEnter` / `AddOnExit`
- [x] 2.3 Remove `ToFrozen()` from `StateHandlerConfig` (freezing now happens at `Build()` time in `MachineConfigBuilder`)

## 3. Rework MachineConfigBuilder

- [x] 3.1 Replace `List<StateHandler>` and `List<EventHandler>` fields with `Dictionary<Type, StateHandlerConfig> _stateConfigs`
- [x] 3.2 Add `GetOrCreateStateConfig(Type)` internal method that returns the existing entry or inserts a new one
- [x] 3.3 Remove `AddStateHandler` and `AddEventHandler` methods
- [x] 3.4 Update `Build()` to iterate `_stateConfigs`, call `ToFrozen()` on each entry to produce `StateHandler` and `EventHandler` arrays, validate no incomplete `EventHandlerConfig` entries (throw `IncompleteEventHandlerException`), then pass arrays to `MachineDefinition` constructor

## 4. Rework StateConfigBuilder

- [x] 4.1 Replace private `StateHandlerConfig _config = new()` field with `StateHandlerConfig _config` received from `MachineConfigBuilder.GetOrCreateStateConfig()`
- [x] 4.2 Remove `Flush()` method entirely
- [x] 4.3 Update `OnEnter` to call `_config.AddOnEnter(callback)` directly (no deferred accumulation)
- [x] 4.4 Update `OnExit` to call `_config.AddOnExit(callback)` directly
- [x] 4.5 Update `In<TNextState>()` to call `_machine.GetOrCreateStateConfig(typeof(TNextState))` without calling `Flush()` first
- [x] 4.6 Update `Build()` to call `_machine.Build()` directly without calling `Flush()` first

## 5. Rework EventConfigBuilder

- [x] 5.1 In the constructor, create a new `EventHandlerConfig`, set `IsComplete = false`, and append it to `_state._config.EventHandlers` immediately
- [x] 5.2 Update `Commit()` (called by `GoTo`/`Stay`) to set `_config.IsComplete = true` and set the action on the config entry — remove the `_machine.AddEventHandler(...)` call
- [x] 5.3 Add `IsComplete` flag to `EventHandlerConfig`

## 6. Update EventHandlerConfig.ToFrozen

- [x] 6.1 Move `ToFrozen()` call-site from `EventConfigBuilder.Commit()` to `MachineConfigBuilder.Build()` (ToFrozen is still called once per entry, just later)
- [x] 6.2 Verify `BuildCombinedGuard()` already snapshots via `.ToArray()` — no change needed, just confirm

## 7. Update Tests

- [x] 7.1 Add test: `OnEnter` registered across two `In<S>()` blocks fires in declaration order (regression for ordering bug)
- [x] 7.2 Add test: `OnExit` registered across two `In<S>()` blocks fires in declaration order
- [x] 7.3 Add test: disconnected-style `config.In<S>().OnEnter(X)` as statement — handler is NOT silently dropped
- [x] 7.4 Add test: `Build()` throws `IncompleteEventHandlerException` when `On<E>()` has no terminal
- [x] 7.5 Add test: `Build()` throws `IncompleteEventHandlerException` when `On<E>().When(guard)` has no terminal
- [x] 7.6 Add test: `Build()` succeeds after incomplete handler is completed (re-callable `Build()`)
- [x] 7.7 Add test: two `Build()` calls on same config produce independent definitions (second contains handlers added after first build)
- [x] 7.8 Fix existing test `OnEnter_MultipleRegistrationsForSameType_AllFireInSomeOrder` — strengthen assertion to verify declaration order (`["first", "second"]`), not just presence
- [x] 7.9 Fix existing test `Start_SetsState_WithoutFiringOnEnter` — use chained style so handler is actually registered; confirm `OnEnter` does not fire on `Create()`
