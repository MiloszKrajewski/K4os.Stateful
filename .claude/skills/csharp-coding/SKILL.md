---
name: csharp-coding
description: Reference for C# / .NET coding style, naming conventions, SOLID principles, TDD workflow, and XML documentation standards. Use when writing, refactoring, or reviewing C# code.
---

# C# Coding Standards

Priorities: correctness → readability → performance. Never over-engineer.

## Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Types, methods, properties, constants | PascalCase | `OrderService`, `GetById`, `MaxRetry` |
| Private/protected fields | `_camelCase` | `_logger`, `_cts` |
| Locals, parameters | camelCase | `orderId`, `token` |
| Interfaces | `I` prefix | `IOrderService` |
| Generic parameters | `T` alone, or `T{Role}` | `T`, `TRequest`, `TResponse` |

**Readonly static**: In most cases are just `const`s but of composite type, uses `const` naming rules.

**Async suffix**: Only when a class exposes both sync and async versions of the same operation — then ALL async methods in that class get the suffix for consistency. Async-only classes: no suffix.

**Variable name length proportional to scope**: fields → descriptive; method locals → shorter; loop/lambda → very short (`i`, `x`).

**Names express intent**, not type. `userList` → `activeUsers`.

---

## Code Layout

- **Line length**: 120 characters max.
- **Indentation**: 4 spaces. Semantic — reflects logical structure, not cosmetic alignment.
- **Cosmetic alignment**: Never.
- **Braces**:
  - Code blocks (classes, methods, control flow): **Allman** (opening brace on new line)
  - Data initialization (object/collection initializers): **same line**
  - Lambdas and switch expressions: **same line**
- **Flow control** (`if` body is just `return`, `throw`, `break`, `continue`): braces optional
  - Simple: same line — `if (x is null) throw new ArgumentNullException(nameof(x));`
  - Complex expression: next line, no braces — `if (state == State.Invalid)\n    throw new InvalidOperationException(...);`
- **Expression-bodied members**: use for any single-expression method or property.
- **Blank lines**: one between methods; one before a major block (e.g., before `try`).

**Parameter lists** — "one finger pointing" principle, never mixed:
```csharp
// Compact
void Method(int a, int b, string c);

// Expanded — each on own line, 4-space indent, semantically grouped
void Point(
    string label,
    double x, double y, double z,   // coordinate triple stays together
    int size);
```

**Ternary patterns** — three forms, pick by complexity:
```csharp
// 1. Single-line
var result = condition ? valueA : valueB;

// 2. Multi-line
var result = someComplexCondition
    ? ProcessAndTransform(input)
    : GetDefault(fallback);

// 3. Switch-like chain
var result =
    value == 0 ? "zero" :
    value == 1 ? "one" :
    value < 10 ? "small" :
    "large";
```

Avoid reevaluation — when condition is a method call, prefer switch expression:
```csharp
// Bad — evaluates GetMetadata() twice
var result = GetMetadata() != null ? Process(GetMetadata()) : null;

// Good — evaluates once
var result = GetMetadata() switch { null => null, var md => Process(md) };
```

---

## Key Patterns & Idioms

- **Records** for immutable value objects and DTOs.
- **Pattern matching** and switch expressions over `if/else` chains.
- **Guard clauses with early returns** over deeply nested conditionals.
- **LINQ** for collection operations unless performance-critical; never double-enumerate — materialize with `.ToList()` / `.ToArray()` when needed.
- **`IReadOnlyList<T>`** / **`IReadOnlyCollection<T>`** in public APIs.
- Nullable annotations make intent explicit: `IReadOnlyList<T>` = always present (possibly empty), `IReadOnlyList<T>?` = may not exist. Don't conflate the two — an empty list and an absent list mean different things.
- **Parameter guards** — use modern throw helpers:
  ```csharp
  ArgumentNullException.ThrowIfNull(accessor);
  ArgumentException.ThrowIfNullOrEmpty(name);
  ArgumentException.ThrowIfNullOrWhiteSpace(text);
  ```
- **Nullable reference types**: always enabled. No `!` suppressions without an explanatory comment.
- **`var`**: preferred by default. Use explicit type only when it meaningfully aids readability or documents a deliberate type choice.

---

## SOLID

**S** — One reason to change per class. Avoid `Manager`, `Helper`, `Utils` in names — they're a smell.

**O** — Extend via new types, not by modifying existing ones. Prefer strategy pattern over growing switch statements.

**L** — Subtypes fully substitutable. No overrides that throw `NotSupportedException`. Prefer interface composition over deep inheritance.

**I** — Narrow interfaces. `IReader<T>`, `IWriter<T>` over one 15-method `IRepository<T>`.

**D** — Depend on abstractions. All dependencies via constructor injection. No service locator. `new` only for domain value objects.

---

## Async

- Every I/O-bound operation is async end-to-end. No `.Result` or `.Wait()`.
- All async public methods accept `CancellationToken`.
- `ConfigureAwait(false)` in library code; not required in ASP.NET Core app code.
- Prefer `Task<T>` generally; `ValueTask<T>` on hot paths.

---

## Error Handling

- Exceptions for exceptional conditions — not control flow.
- Custom exception types for domain errors (`OrderNotFoundException`).
- Swallowing exceptions requires explicit justification — if you can't articulate why discarding is correct here, don't discard. `Try...` pattern methods are a canonical valid case; document others with a comment.
- Always log with structured context before re-throwing.

---

## XML Documentation

Document all **publicly visible members** (public/protected in public/protected types). Focus on what's non-obvious — edge cases, failure modes, special values, thread-safety. The happy path is usually clear from the signature.

**Single-line summary** when it fits: `/// <summary>Get user by ID.</summary>`

| Tag | Use when |
|-----|----------|
| `<summary>` | Always — 1-2 sentences max |
| `<param>` | Special values, constraints (`-1 = unlimited`, `null = use default`) |
| `<returns>` | Failure cases — returns null? -1? throws? |
| `<exception>` | Non-obvious exceptions (skip `ArgumentNullException` if nullable annotations cover it) |
| `<remarks>` | Thread-safety, performance warnings, caching recommendations |
| `<seealso>` | Related members — use liberally |

```csharp
// Bad — states the obvious
/// <summary>
/// This method takes a user ID and returns the user from the database.
/// </summary>

// Good — documents the edge case
/// <summary>Retrieves a user by ID.</summary>
/// <returns>User if found; null if not found or ID &lt;= 0</returns>
public async Task<User?> GetUserAsync(int userId)
```

```csharp
// Good — non-obvious failure modes and thread-safety
/// <summary>Connects to the remote server.</summary>
/// <exception cref="TimeoutException">Timeout after 30s</exception>
/// <exception cref="InvalidOperationException">Already connected</exception>
/// <remarks>Blocks until connected or timed out. Not thread-safe.</remarks>
public void Connect(string endpoint)
```

---

## TDD Workflow

**Red → Green → Refactor**. Write the test before the implementation. Running the failing test first is good practice but not mandatory when the failure is obvious.

### Cycle
1. Write one test
2. Write minimum code to pass
3. Run tests — confirm green
4. Refactor while green
5. Repeat

### Conventions
- Test project: `{ProjectName}.Tests`
- Test class: `{ClassName}Tests`, location mirrors source structure (`Services/UserService.cs` → `Services/UserServiceTests.cs`)
- Test method: `MethodName_Scenario_ExpectedResult` — or just `Scenario_ExpectedResult` when class name provides enough context
- Pattern: Arrange / Act / Assert
- Stack: **xUnit + Moq**
- EF integration tests: **Testcontainers** (real DB, not in-memory)
- Mark slow tests: `[Trait("Category", "Stress")]`; exclude by default with `--filter "Category!=Stress"`

### Project setup (once per project)
```bash
dotnet new xunit -n {ProjectName}.Tests -f net10.0
dotnet add {ProjectName}.Tests reference {ProjectName}/{ProjectName}.csproj
dotnet add {ProjectName}.Tests package Moq
dotnet add {ProjectName}.Tests package coverlet.collector
```
Add to main project's `.csproj`:
```xml
<InternalsVisibleTo Include="$(AssemblyName).Tests" />
```
### When to mock
- Yes: external services, DB, file system, network, testing failure paths
- No: pure functions, simple value objects

### Commands
```bash
dotnet test --filter "Category!=Stress"
dotnet test --filter "FullyQualifiedName~MyClassName"
# Before commit — validate in Release
dotnet build --configuration Release
dotnet test --no-build --configuration Release --filter "Category!=Stress"
```

### Debugging test output
`Console.WriteLine` is silent in xUnit. Use `ITestOutputHelper` (injected via constructor) for test-level output. For capturing `Trace.WriteLine` from production code,
add `<ItemGroup><AssemblyAttribute Include="Xunit.CaptureTraceAttribute"/></ItemGroup>` added to `*.Test.csproj` (works with xUnit 3+).

### Decision rules
- **New test class?** Yes for each new class under test; no for adding cases to existing coverage.
- **Refactor?** Only when tests are green — never during red.

---

## Pre-commit Checklist

- [ ] All public members have XML doc comments
- [ ] Nullable annotations satisfied — no unexplained `!` suppressions
- [ ] All async methods accept `CancellationToken`
- [ ] No infrastructure `new` — injected via constructor
- [ ] No unjustified silent `catch` blocks
- [ ] Tests written and green

**Guidelines** (not hard requirements — use judgment):
- Methods are focused and short; if a method needs a comment to explain its sections, consider splitting
- Parameter lists are kept small; long lists suggest a missing abstraction
