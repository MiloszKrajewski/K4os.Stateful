## Why

The new `K4os.Stateful` library ranks state/event handlers by type-inheritance distance — closer (more specific) types win. No correct distance algorithm exists yet in the new project; the legacy `ReflectionExtender.DistanceFrom` has a known bug that collapses multi-hop class chains to distance 0, making it unsuitable to port. This is Layer 1 of the implementation plan and is a prerequisite for every subsequent layer.

## What Changes

- New internal utility class `TypeExtensions` added to `K4os.Stateful/Internal/`
- Extension method `child.DistanceFrom(parent)` on `Type` returning `int?`; `null` means the types are unrelated (not assignable)
- Distance rules: class chain gets `+1` per hop; interfaces get the same distance as the class that first introduces them (set-difference approach)
- Per-pair result cached in a `ConcurrentDictionary<(Type, Type), int?>` — computed once per unique pair
- Full test suite in `K4os.Stateful.Tests/`

## Capabilities

### New Capabilities

- `type-distance`: Extension method `child.DistanceFrom(parent)` on `Type`; returns inheritance distance or `null` for unrelated types; supports class chains and interface introduction semantics; cached per pair.

### Modified Capabilities

_(none — this is net-new code with no existing specs)_

## Impact

- New file: `src/K4os.Stateful/Internal/TypeExtensions.cs`
- New file: `src/K4os.Stateful.Tests/TypeExtensionsTests.cs`
- No public API changes; `TypeExtensions` is `internal`
- No dependencies beyond the standard library (`System.Collections.Concurrent`)
- Consumed by Layer 2 (Rule Ranking) and Layer 5 (Entry/Exit Firing Order) in subsequent changes
