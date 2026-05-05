# Spec: Activation Context

## Purpose

Defines the `Activation` bundle types that carry all lambda parameters into a single typed object,
used as the sole argument to all DSL callbacks (`When`, `GoTo`, `Stay`, `OnEnter`, `OnExit`, `Auto`).

## Requirements

### Requirement: Activation bundles carry all lambda parameters

The library SHALL provide two `Activation` types that bundle all lambda parameters into a single
typed object. Lambdas in `When`, `GoTo`, `Stay`, `OnEnter`, `OnExit`, and `Auto` SHALL each
receive exactly one argument of the appropriate `Activation` type.

`Activation<TContext, TCurrentState, TCurrentEvent>` — for event-driven callbacks:
- `TContext Context` — shared service/infrastructure object for the executor instance
- `TCurrentState State` — the concrete current state (narrowed to the registered type)
- `TCurrentEvent Event` — the concrete triggering event (narrowed to the registered type)
- `CancellationToken CancellationToken` — from the `FireAsync` call site

`Activation<TContext, TCurrentState>` — for lifecycle callbacks (no triggering event):
- `TContext Context`
- `TCurrentState State`
- `CancellationToken CancellationToken`

#### Scenario: GoTo lambda receives typed State and Event
- **WHEN** a handler is registered as `.In<ClosedState>().On<LockEvent>().GoTo(x => ...)`
- **THEN** `x.State` is typed as `ClosedState` and `x.Event` is typed as `LockEvent`

#### Scenario: OnEnter lambda receives typed State but no Event
- **WHEN** a handler is registered as `.In<ClosedState>().OnEnter(x => ...)`
- **THEN** `x.State` is typed as `ClosedState` and the activation has no `Event` property

#### Scenario: CancellationToken flows from FireAsync to all lambdas
- **WHEN** `FireAsync(event, ct)` is called with a specific `CancellationToken`
- **THEN** `x.CancellationToken` in `When`, `GoTo`, `Stay`, `OnEnter`, and `OnExit` lambdas equals that token
