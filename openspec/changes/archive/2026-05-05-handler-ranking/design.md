## Context

Layer 1 (`type-distance`) already provides `TypeExtensions.DistanceFrom(child, parent) → int?`, which answers "how many inheritance steps from this concrete type to a registered type?" returning `null` when the registered type is not an ancestor.

The legacy implementation (`StateMachine.Executor.cs`) puts the ranked-handler cache in the **executor** — meaning every new executor (one per web request) rebuilds it from scratch. This is the key performance problem this layer fixes by moving the cache into the frozen definition.

The legacy sort key has three fields (`stateDistance, eventDistance, isFallback`) and misses class/interface distinction and declaration order, leading to non-deterministic tie-breaking.

## Goals / Non-Goals

**Goals:**
- Define `TransitionRule` — the minimal metadata needed for ranking (state type, event type, guard flag, declaration order). Serves as the base that Layer 4 will extend with callbacks.
- Define `HandlerRanker` — a pure function: `(rules, actualStateType, actualEventType) → TransitionRule[]` sorted by priority, incompatible rules excluded.
- Define `MachineDefinition<TContext, TState, TEvent>` — the frozen definition object with a lazy, thread-safe cache of ranked handler arrays.

**Non-Goals:**
- Guard evaluation (done by executor at fire time — guards are runtime predicates).
- Callbacks / action delegates (Layer 4 — DSL builder).
- Entry/exit handler ordering (similar algorithm, no event axis, separate concern).
- The configurator and `Build()` method (Layer 4).
- The executor itself (Layer 7+).

## Decisions

### D1 — Six-field sort key, not four

The design spec's rule-ranking table shows four fields (state distance, class/interface, hasGuard, declarationOrder). This is sufficient when all rules use exact event types, but event polymorphism is a real feature: `On<IDoorEvent>()` as a catch-all alongside `On<OpenEvent>()` as a specific handler.

Without event distance in the sort key, both rules would have identical state-level keys and fall through to declaration order — a catch-all registered before a specific handler would silently shadow it.

**Decision:** six fields — `stateDistance, stateIsInterface, eventDistance, eventIsInterface, hasNoGuard, declarationOrder` — strictly additive over the spec's four; no existing priority relationship changes.

### D2 — Class beats interface at the same distance

Both a class and any interfaces it introduces share the same distance value (the distance algorithm assigns interfaces the depth of their introducing class, not a derived depth). Within a rank, a rule registered on the concrete class (`In<ClosedState>()`) expresses more specific intent than one on an interface (`In<IDoorState>()`), even though the distance numbers are equal.

**Decision:** `IsInterface` is field 2 (state axis) and field 4 (event axis), both false < true (class = higher priority). This applies symmetrically to both axes.

### D3 — Guarded rules rank after class/interface, not before

A developer who registers `In<ClosedState>()` without a guard is making a deliberate catch-all for that specific state. A developer who registers `In<IDoorState>().When(...)` is making a conditional rule for any door state. State specificity should override guard presence — otherwise a guarded base-type rule could shadow an unguarded concrete-type rule.

**Decision:** priority order is specificity first (distance + class/interface), filtering second (guard), tie-break last (declaration order). `HasNoGuard` is field 5: 0 = has guard (fires first), 1 = unguarded fallback.

### D4 — Cache in MachineDefinition, not Executor

Executors are created per web request and are short-lived. Caching in the executor means rebuilding the sorted handler list on every request for every `(actualStateType, actualEventType)` pair encountered. The machine definition is created once at startup and shared across all executors.

**Decision:** `MachineDefinition` holds a `ConcurrentDictionary<(Type, Type), TransitionRule[]>` that starts empty and grows as new type pairs are encountered at runtime. Thread-safe because `ConcurrentDictionary.GetOrAdd` is used with a static factory (no closure allocation). Multiple `Build()` calls on the same configurator produce independent definitions with independent caches.

### D5 — Return TransitionRule[] directly, not indices

Indices into a parallel array add an extra layer of indirection with no benefit when the handler type is a reference type. The executor walks the returned array directly.

**Decision:** `HandlerRanker.RankedCandidates` returns `TransitionRule[]`.

### D6 — Sort key is a throwaway value type

The sort key is only needed during the one-time ranking computation for a given type pair. Storing it alongside the sorted results would waste memory in the cache. A private `readonly struct RuleSortKey : IComparable<RuleSortKey>` inside `HandlerRanker` keeps the computation self-contained.

**Decision:** `RuleSortKey` is a private struct, not exposed. Only `TransitionRule[]` is cached.

### D7 — TransitionRule is immutable; Layer 4 introduces a mutable twin

`TransitionRule` is frozen once created — all fields set in the constructor, no setters. This is safe because rules only live in `MachineDefinition` after `Build()` and are never modified afterward.

Layer 4 (DSL builder) will introduce a **mutable twin** class (e.g. `RuleConfig`) that the configurator accumulates while the chain is being built (`.In().On().When().GoTo()`). At `Build()` time the configurator converts each mutable config into an immutable rule (either an extended `TransitionRule` subclass or a sibling class — that is a Layer 4 design decision). The ranking algorithm only ever sees the immutable form.

**Decision:** `TransitionRule` at this layer has 4 fields, all constructor-set, no mutation. The mutable builder pattern is a Layer 4 concern.

## Risks / Trade-offs

- **Cache growth**: The cache in `MachineDefinition` is unbounded. In practice, the set of concrete state/event types is defined by the developer and finite (not truly runtime-generated in normal use). For pathological cases with many generated types this could be a memory concern, but no mitigation is planned at this layer.
- **First-request cost**: The first executor to encounter a new type pair pays the `O(n)` ranking cost (n = number of registered rules). All subsequent requests get O(1). Acceptable trade-off; machines with many rules and many state/event type combinations may see a warm-up period.
