## Context

The new `K4os.Stateful` library requires a type-distance algorithm to rank state/event handlers. Rules registered for `In<ClosedState>()` must rank closer than those registered for `In<IDoorState>()` when the actual state is `ClosedState`. The same algorithm drives entry/exit firing order (Layer 5) and rule ranking (Layer 2).

The legacy `ReflectionExtender.DistanceFrom` is buggy: it uses recursive intermediate-parent traversal but omits the `+1` increment, collapsing all class-chain distances to 0. It cannot be ported.

## Goals / Non-Goals

**Goals:**
- Correct distance integer for any `(actualType, registeredType)` pair where `registeredType` is assignable from `actualType`
- `null` (no distance) for unrelated types
- Per-pair cache: each unique `(child, parent)` pair is computed once and reused
- Correct interface semantics: an interface gets the same numeric distance as the class that introduces it; the class/interface distinction is a secondary sort key handled by callers (Layer 2)

**Non-Goals:**
- Pre-computing distances to *all* ancestors at once — that is a future executor-level concern when building sorted handler lists
- Class-vs-interface secondary sort (belongs in Layer 2 — `registeredType.IsInterface` is cheap at call site)
- Rule evaluation, guard execution, or entry/exit ordering (Layers 2–5)
- Public API surface — `TypeExtensions` is `internal static`

## Decisions

### D1 — Cache per `(actualType, registeredType)` pair

**Chosen:** `ConcurrentDictionary<(Type child, Type parent), int?>`; one entry per pair, populated lazily on first lookup via `child.DistanceFrom(parent)`.

**Alternative considered:** `ConcurrentDictionary<Type, IReadOnlyDictionary<Type, int>>` — one inner map per child type, built eagerly to contain all ancestors at once.

**Rationale:** The inner-map approach is over-engineered for this layer. It introduces a nested dictionary where the inner value is not thread-safe during construction, and requires building distances to every ancestor even when only one is needed. The per-pair `ConcurrentDictionary` is flat, thread-safe without extra locks, and sufficient: after warmup every unique `(child, parent)` pair is O(1). The executor-level "sorted handlers by distance" cache (a future layer) is the right place to pre-group distances across all registered types for a given child type.

### D2 — Interface distance = same as introducing class; `GetInterfaces()` transitivity eliminates explicit forking

**Chosen:** At each class `C` at distance `d`, "newly introduced" interfaces = `C.GetInterfaces() − C.BaseType.GetInterfaces()`. If `registeredType` is a member of that set, return `d` (same as the introducing class).

**Why same distance, not `d+1`:** Using `d+1` would collapse the interface and the *next* class in the chain to the same numeric distance. For a chain `A : B : C (IC) : object`, `d+1` gives IC=3 and object=3 — the "class beats interface" tiebreaker would then place `object` before `IC`, violating the required ordering `dist(IC) < dist(object)`. Same distance avoids this: IC and C share numeric distance 2, and the class/interface distinction is a secondary sort key for Layer 2 callers — not encoded in the distance integer itself.

**Why no explicit forking:** `.GetInterfaces()` on a class in .NET returns the **full transitive closure** of all interfaces, including those inherited through other interfaces. This means:

- **No explicit forking needed.** A class that declares `IChild : IParent` will have both `IChild` and `IParent` in `GetInterfaces()`. No need to walk the interface graph separately.
- **Unrelated interfaces are eliminated for free.** The check is a single membership test: `registeredType ∈ newInterfaces`. An interface that is not `registeredType` is simply skipped.
- **Set-difference finds the right class level.** If `IBase` is in both `A.GetInterfaces()` and `B.GetInterfaces()` (B extends A), the diff `B − A` excludes `IBase` from B's level; it is found at A's level and returns A's distance.

Distance table for actual type `C : B : A` (A : IBase, B : IMid, C : ILeaf):

| Distance | Type | Kind |
|----------|------|------|
| 0 | C | class (actual type) |
| 0 | ILeaf | interface introduced at C |
| 1 | B | class |
| 1 | IMid | interface introduced at B |
| 2 | A | class |
| 2 | IBase | interface introduced at A |
| 3 | object | class |

Within the same distance, class has priority over interface — but that is a Layer 2 concern. This layer returns the same integer for both.

### D3 — Iterative linear walk on the class chain only

**Chosen:** A simple `while (current != null)` loop walking `current.BaseType`. At each step: check if `current == parent` (class match at distance `d`) or `parent ∈ (current.GetInterfaces() − current.BaseType.GetInterfaces())` (interface match, also at distance `d`). Exit as soon as either match is found.

**Alternative considered:** Recursive `DistanceFrom` as in the legacy code, which forks on all intermediate parent types.

**Rationale:** The class chain is strictly linear — no forking. Interface forking is fully absorbed by `GetInterfaces()` as described in D2. The result is a clean O(depth) linear walk with early exit, no recursion, no interface graph traversal.

## Risks / Trade-offs

**[Risk] Compute function called twice under race** → `ConcurrentDictionary.GetOrAdd` may invoke the factory on two threads concurrently before either result is stored. The computation is pure and cheap (O(depth) walk), so duplicate work is harmless and bounded.

**[Risk] `object` reachable as registered type** → The walk reaches `object` at the maximum class-chain depth. Registering a handler for `object` is legal, if unusual. No special-casing needed.

**[Risk] Interface-reintroduction ambiguity** → If a derived class re-declares an interface already present in a base class, `GetInterfaces()` on both classes includes it. The set-difference correctly assigns it the lower (base-class) level, returning that class's distance. No special case needed.
