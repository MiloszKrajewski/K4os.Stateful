---
name: stackalloc-pooled-fallback
description: "Introduces a stackalloc fallback to ArrayPool<T>.Shared for hot-path C# methods that need variable-size stack buffers. Use this skill whenever a method uses a naked stackalloc that should gracefully fall back to a pooled array for larger inputs, or when adding this pattern from scratch to a method that processes variable-length data. Triggers on: stackalloc, ArrayPool, Span<T> buffer allocation, avoiding stack overflow for variable buffers, or any request to make a stack allocation safe for large inputs."
---
# stackalloc → pooled array fallback

In C#, `stackalloc` allocates memory on the stack — fast but size-limited. For variable-size buffers, fall back to `ArrayPool<T>` when the required size exceeds the stack limit.

`finally` blocks carry a runtime cost. For methods with a low probability of throwing, skip`try/finally` for the pool return — unreturned arrays will eventually be garbage collected. The performance gain in the common case outweighs the occasional array leak on failure. Do not add `try/finally` solely for this purpose; if one already exists, use it.

---
## Step 1 — locate or add `StackallocHelpers`

First, search the project for an existing `StackallocHelpers` class:

```
rg "class StackallocHelpers" --type cs -l
```

If found, skip to Step 2 — the helpers are already available project-wide.

If not found, add the following at the bottom of the current file (file-scoped, no namespace pollution):

```csharp
file static class StackallocHelpers
{
    private const int MAX_STACKALLOC_IN_BYTES = 8192; // adjust as needed

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryRentAbove<T>(
        int count, [NotNullWhen(true)] out T[]? rented, 
        int thresholdBytes = MAX_STACKALLOC_IN_BYTES)
    {
        var totalBytes = count * Unsafe.SizeOf<T>();
        rented = totalBytes <= thresholdBytes ? null : ArrayPool<T>.Shared.Rent(count);
        return rented is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TryReturn<T>(T[]? rented)
    {
        if (rented is not null)
            ArrayPool<T>.Shared.Return(rented);
    }
}
```

Add `using System.Buffers` and `using System.Runtime.CompilerServices` if missing.

- `file` scope keeps the class local to this file — no namespace pollution, no conflicts with other files that may define their own copy.
- `ArrayPool<T>.Shared` is used implicitly — callers never deal with the pool directly.
- `TryRentAbove` returns `true` when rented (caller uses the array), `false` when the count fits on the stack (caller uses `stackalloc`).
- `TryReturn` is a no-op when `rented` is null — the name is honest: it tries, but there may be nothing to do.
- The helpers are generic; `T` stays as-is in their signatures. The concrete type appears only in the `stackalloc` fallback — e.g. `stackalloc char[charsNeeded]` — which the compiler requires anyway.

---
## Step 2 — replace naked `stackalloc`

Before:

```csharp
var chars = stackalloc char[text.Length];
```

After:

```csharp
var charsNeeded = text.Length;
var chars = StackallocHelpers.TryRentAbove<char>(charsNeeded, out var charsRented)
    ? charsRented.AsSpan(0, charsNeeded)
    : stackalloc char[charsNeeded];
```

Notes:
- Precalculating `charsNeeded` into a local is **mandatory** — not optional.
- The type of `chars` is `Span<T>` (`Span<char>` here) — don't be concerned if that seems unexpected.
- `charsRented` is kept solely to return to the pool — do not use it directly.
- No post-allocation slice needed: the `true` branch uses `AsSpan(0, charsNeeded)` (exact size), and the `false` branch stackallocs exactly `charsNeeded` elements.

---
## Step 3 — return to pool at end of method

```csharp
StackallocHelpers.TryReturn(charsRented);
```

Do **not** add a `try/finally` block for this purpose. If one already exists, place the return inside it.

---
## Step 4 — verification

Neither rented array nor anything pointing to it (a span on top of it) can be used after returning to the pool. In this example neither `chars` nor `charsRented` can be used after `StackallocHelpers.TryReturn(charsRented)`. 
If method already has `try` statement then solution is to put `TryReturn` inside `finally` section, if there is no `try` block then it should materialize result before returning buffer, for example, instead of:

```csharp
StackallocHelpers.TryReturn(charsRented);
return new string(chars);
```

it should use:

```csharp
var result = new string(chars);
StackallocHelpers.TryReturn(charsRented);
return result;
```

If no solution can be found then this is a serious problem as needs to be marked with `#error buffer use after return` directive.

---
## Naming convention

Variable names are derived from the original buffer name. Given a buffer named `chars`:

| Variable     | Suffix       | Example       | Purpose                                          |
| ------------ | ------------ | ------------- | ------------------------------------------------ |
| Working span | *(original)* | `chars`       | Buffer to work with                              |
| Size / count | `Needed`     | `charsNeeded` | Exact element count required                     |
| Rented array | `Rented`     | `charsRented` | For returning to pool only — do not use directly |
