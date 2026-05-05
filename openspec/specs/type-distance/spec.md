## Purpose

Defines the contract for `TypeDistance` — the utility that computes the inheritance-chain distance between two .NET `Type` objects. Distance drives handler resolution in the state machine: the handler registered for the nearest ancestor in the type hierarchy wins.

## Requirements

### Requirement: Self distance is zero
`child.DistanceFrom(parent)` SHALL return `0` when `child` and `parent` are the same type.

#### Scenario: Exact match returns zero
- **WHEN** `typeof(ClosedState).DistanceFrom(typeof(ClosedState))` is called
- **THEN** the result is `0`

### Requirement: Class chain distances increment by one per hop
For each step up the base-class chain, the distance SHALL increase by exactly `1`.

#### Scenario: Direct base class is distance one
- **WHEN** `child` directly extends `parent`
- **THEN** `child.DistanceFrom(parent)` returns `1`

#### Scenario: Grandparent class is distance two
- **WHEN** `child` extends an intermediate class which extends `parent`
- **THEN** `child.DistanceFrom(parent)` returns `2`

#### Scenario: Multi-level hierarchy returns correct distance at each level
- **WHEN** a three-level chain `C : B : A` exists
- **THEN** `typeof(C).DistanceFrom(typeof(C))` = `0`, `typeof(C).DistanceFrom(typeof(B))` = `1`, `typeof(C).DistanceFrom(typeof(A))` = `2`

### Requirement: Interface gets the same distance as the class that introduces it
An interface first appearing in `C.GetInterfaces()` but not in `C.BaseType.GetInterfaces()` (introduced at class `C`, distance `d`) SHALL have distance `d` — the same as the introducing class.

#### Scenario: Interface declared on child type has distance zero
- **WHEN** `child` directly implements `ILeaf` and its base class does not
- **THEN** `typeof(child).DistanceFrom(typeof(ILeaf))` returns `0`

#### Scenario: Interface on direct base class has distance one
- **WHEN** `child` extends `B` which directly implements `IMid`, and `child` does not directly implement `IMid`
- **THEN** `typeof(child).DistanceFrom(typeof(IMid))` returns `1`

#### Scenario: Interface on grandparent class has distance two
- **WHEN** `child` extends `B` extends `A`, and `A` directly implements `IBase` while `B` and `child` do not
- **THEN** `typeof(child).DistanceFrom(typeof(IBase))` returns `2`

#### Scenario: Interface distance is strictly less than next class in chain
- **WHEN** chain is `A : B : C (implements IC) : object`
- **THEN** `typeof(A).DistanceFrom(typeof(IC))` returns `2` and `typeof(A).DistanceFrom(typeof(object))` returns `3`

### Requirement: Multiple interfaces on the same class all share that class's distance
If several interfaces are all first introduced at the same class (distance `d`), each SHALL return `d`.

#### Scenario: Two interfaces declared on child type both return distance zero
- **WHEN** `child` implements `IAlpha` and `IBeta` and its base class implements neither
- **THEN** `typeof(child).DistanceFrom(typeof(IAlpha))` and `typeof(child).DistanceFrom(typeof(IBeta))` both return `0`

### Requirement: Transitive interface inheritance uses set-difference introduction
When `IChild : IParent`, both are assigned the distance of the class that first introduces the group.

#### Scenario: Interface-on-interface both introduced at same class have distance zero
- **WHEN** `child` implements `IChild : IParent`, and neither appears in any base class of `child`
- **THEN** `typeof(child).DistanceFrom(typeof(IChild))` and `typeof(child).DistanceFrom(typeof(IParent))` both return `0`

#### Scenario: Base interface already present via base class retains its distance
- **WHEN** base class `E` introduces `IParent` (distance `1`), and `child` introduces `IChild : IParent` (distance `0`)
- **THEN** `typeof(child).DistanceFrom(typeof(IParent))` returns `1`, not `0`

### Requirement: Interface child always returns null
When `child` is an interface type, `child.DistanceFrom(parent)` SHALL return `null` regardless of `parent`. Runtime types are always concrete classes; interface-to-interface distance is reserved for future extension and currently throws `InvalidOperationException`.

#### Scenario: Interface child with class parent returns null
- **WHEN** `child` is an interface type and `parent` is a concrete class
- **THEN** `typeof(child).DistanceFrom(typeof(parent))` returns `null`

#### Scenario: Interface-to-interface distance throws NotImplementedException
- **WHEN** both `child` and `parent` are interface types
- **THEN** `typeof(child).DistanceFrom(typeof(parent))` throws `InvalidOperationException` (BFS on the interface DAG is reserved for future implementation)

### Requirement: Unrelated type returns null
When `parent` is not assignable from `child`, `child.DistanceFrom(parent)` SHALL return `null`.

#### Scenario: Completely unrelated class returns null
- **WHEN** `child` and `parent` are unrelated types
- **THEN** `typeof(child).DistanceFrom(typeof(parent))` returns `null`

#### Scenario: Sibling class returns null
- **WHEN** both `ClosedState` and `OpenState` implement `IDoorState` but neither extends the other
- **THEN** `typeof(ClosedState).DistanceFrom(typeof(OpenState))` returns `null`

### Requirement: Distance computation is cached per type pair
`DistanceFrom` SHALL compute the distance at most once per unique `(child, parent)` pair; subsequent calls SHALL return the cached result without recomputation.

#### Scenario: Repeated calls return the same value without recomputation
- **WHEN** `typeof(C).DistanceFrom(typeof(A))` is called twice
- **THEN** both calls return the same result and the second call does not traverse the type hierarchy

### Requirement: `object` is reachable at the maximum class-chain depth
`child.DistanceFrom(typeof(object))` SHALL return the number of class hops from `child` to `object`.

#### Scenario: Object distance equals class chain depth
- **WHEN** `child` has `n` hops to `object` in its class chain
- **THEN** `typeof(child).DistanceFrom(typeof(object))` returns `n`
