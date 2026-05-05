## 1. Core Implementation

- [x] 1.1 Create `src/K4os.Stateful/Internal/TypeExtensions.cs` with `internal static class TypeExtensions`
- [x] 1.2 Add `ConcurrentDictionary<(Type child, Type parent), int?> _distanceCache` as the flat per-pair cache
- [x] 1.3 Implement `private static int? Compute(Type child, Type parent)`: iterative walk up the base-class chain; class match at step `d` returns `d`; interface match via set-difference (`current.GetInterfaces() − current.BaseType.GetInterfaces()`) also returns `d`; return `null` if `parent` is not found in the chain
- [x] 1.4 Implement `public static int? DistanceFrom(this Type child, Type parent)` using `_distanceCache.GetOrAdd` to wrap `Compute`

## 2. Tests

- [x] 2.1 Create `src/K4os.Stateful.Tests/TypeExtensionsTests.cs` with test type hierarchy: `IBase`, `IMid`, `ILeaf`, `A : IBase`, `B : A, IMid`, `C : B, ILeaf`
- [x] 2.2 Test: `typeof(C).DistanceFrom(typeof(C))` → `0` (self)
- [x] 2.3 Test: `typeof(C).DistanceFrom(typeof(B))` → `1`, `typeof(C).DistanceFrom(typeof(A))` → `2` (class chain)
- [x] 2.4 Test: `typeof(C).DistanceFrom(typeof(ILeaf))` → `0`, `typeof(C).DistanceFrom(typeof(IMid))` → `1`, `typeof(C).DistanceFrom(typeof(IBase))` → `2` (interface = same distance as introducing class)
- [x] 2.5 Test: `typeof(C).DistanceFrom(typeof(object))` → `3` (strictly greater than IBase at 2)
- [x] 2.6 Test: multiple interfaces at same class — `D : IAlpha, IBeta` → both return `0`
- [x] 2.7 Test: interface-on-interface — `E : IChild` where `IChild : IParent`, neither in base → both return `0`
- [x] 2.8 Test: base interface retained — `F : E, IChild` where `E : IParent` → `typeof(F).DistanceFrom(typeof(IParent))` = `1`, `typeof(F).DistanceFrom(typeof(IChild))` = `0`
- [x] 2.9 Test: unrelated class → `null`
- [x] 2.10 Test: sibling class (shares base, not in chain) → `null`
- [x] 2.11 Test: calling `DistanceFrom` twice for same pair returns same value (cache hit)
- [x] 2.12 Run `dotnet test` and confirm all tests pass
