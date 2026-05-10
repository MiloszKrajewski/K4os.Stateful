## 1. Data Model

- [x] 1.1 Add `Auto` delegate field (`Func<Activation<TContext, TState>, ValueTask<TState>>?`) to `StateHandler<TContext, TState>`
- [x] 1.2 Add `AutoHandler` accumulator field to `StateHandlerConfig` (last-write-wins; replaces on repeated calls)

## 2. DSL

- [x] 2.1 Add `.Auto(Func<Activation<TContext, TCurrentState>, ValueTask<TState>> handler)` to `IStateConfig<TCurrentState>` in `StateMachine.Interfaces.cs`; returns `IStateConfig<TCurrentState>`
- [x] 2.2 Add default interface overloads for sync (`TState`) and `Task<TState>` forms
- [x] 2.3 Implement `.Auto()` in `StateConfigBuilder<TCurrentState>` — stores handler on the current `StateHandlerConfig`
- [x] 2.4 Wire `StateHandlerConfig.AutoHandler` into the frozen `StateHandler` produced by `Flush()`

## 3. Executor

- [x] 3.1 Add `RunAutoChainAsync(TState state, CancellationToken ct)` private method to `MachineExecutor` — finds the most-derived `StateHandler` with a non-null `Auto`, invokes it, and loops through full transition (OnExit → update → OnEnter → recurse) until predicate returns false or no Auto found
- [x] 3.2 Call `RunAutoChainAsync` at the end of `TryFireCoreAsync`, after `OnEnter` completes (both in the state-changed and state-unchanged branches only need it on state-changed path)

## 4. Tests

- [x] 4.1 Auto handler is registered via DSL and stored on `StateHandler` (unit test in `DslBuilderTests`)
- [x] 4.2 Multiple `.Auto()` calls on same state type — last wins (DSL unit test)
- [x] 4.3 `Create()` does not trigger Auto
- [x] 4.4 Auto fires after OnEnter on a triggered transition, before `FireAsync` returns
- [x] 4.5 Auto returning same reference — no-op; chain stops
- [x] 4.6 Auto returning different state fires full OnExit → OnEnter cycle for each hop
- [x] 4.7 Chain of three Auto hops — all intermediate lifecycle hooks fire; final state is correct
- [x] 4.8 Auto on final state with no further Auto — terminates cleanly
- [x] 4.9 Exception in Auto propagates; `executor.State` is the last successfully-entered state
- [x] 4.10 `CancellationToken` flows through to the Auto lambda
