---
name: xml-docs-write
description: Add comprehensive XML documentation comments to all public and protected members in the provided C# class(es).
---
# Add XML Documentation Comments to C# Classes

## Instructions

Add comprehensive XML documentation comments to all public and protected members in the provided C# class(es). Follow these guidelines:

### General Rules
- Use triple slash comments (`///`) for XML documentation
- Add documentation for all public and protected members (classes, methods, properties, fields, events)
- Write clear, concise descriptions that explain the purpose and behavior
- Prefer terse, noun-phrase descriptions over full sentences (e.g. "Cancellation token." not "Token to cancel the operation.")
- Avoid restating what is already obvious from the name — if a reader can derive the meaning from the identifier
  alone, the doc adds no value; focus on what the name does *not* tell you
- Do not focus on implementation details, as they will change from time to time; however try to include
  non-obvious behavior, side effects, performance considerations, or usage examples

### Required XML Tags

#### For Classes and Interfaces
```csharp
/// <summary>Brief description of the class purpose and responsibility.</summary>
/// <remarks>Optional: Additional details, usage notes, or implementation details.</remarks>
```

#### For Methods
```csharp
/// <summary>Brief description of what the method does.</summary>
/// <param name="parameterName">Description of the parameter.</param>
/// <returns>Description of what the method returns.</returns>
/// <exception cref="ExceptionType">Conditions under which this exception is thrown.</exception>
/// <remarks>Optional: Additional usage notes, performance considerations, or examples.</remarks>
```

#### For Properties
```csharp
/// <summary>Brief description of what the property represents.</summary>
/// <value>Description of the property's value and any constraints.</value>
/// <remarks>Optional: Additional notes about the property behavior.</remarks>
```

> **Note on `<value>` and `<remarks>`**: Only include these tags if they add information not already covered by
> `<summary>`. Ask: *does this tag say something the summary doesn't?* If not, omit it.

#### For Fields and Constants
```csharp
/// <summary>Brief description of the field's purpose.</summary>
```

#### For Events
```csharp
/// <summary>Brief description of when the event is raised.</summary>
/// <remarks>Optional: Details about event arguments or usage patterns.</remarks>
```

### Style Guidelines

1. **Summary**: Write whatever reads most naturally — no required verb form. However, when a member has
   observable side effects (triggers events, modifies external state, starts/stops something), make that
   clear — either through an active verb in the summary (`"Sends..."`, `"Triggers..."`, `"Registers..."`)
   or in `<remarks>`. Pure reads and simple writes don't need special treatment.
2. **Parameters**: Always add `<param>` tags for all parameters. Because params are all-or-nothing per method
   (if one is present, all must be), omitting them to avoid padding trivial entries creates a perverse incentive
   to skip the one genuinely useful description. Keep trivial params brief (noun phrases: `"Cancellation token."`,
   `"Stream name."`) — the overhead is negligible.
3. **Returns**: Describe what is returned and under what conditions
4. **Exceptions**: Document all exceptions that can be thrown, including parameter validation exceptions
5. **Generic Types**: Use `<typeparam>` tags for generic type parameters
6. **Cross-references**: Use `<see cref=""/>` for references to other types or members

### Example Documentation Patterns

#### Async Methods
```csharp
/// <summary>Asynchronously uploads data to the specified endpoint.</summary>
/// <param name="data">The data to upload.</param>
/// <param name="endpoint">Target endpoint URL.</param>
/// <param name="token">Cancellation token.</param>
/// <returns>A task representing the asynchronous upload operation.</returns>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="data"/> or <paramref name="endpoint"/> is null.
/// </exception>
/// <exception cref="HttpRequestException">Thrown when the upload request fails.</exception>
```

#### Constructors
```csharp
/// <summary>Initializes a new instance of the <see cref="ClassName"/> class.</summary>
/// <param name="parameter">Description of the constructor parameter.</param>
/// <exception cref="ArgumentException">Thrown when parameter validation fails.</exception>
```

#### Generic Classes/Methods
```csharp
/// <summary>Generic description of the class functionality.</summary>
/// <typeparam name="T">The type parameter constraint and usage description.</typeparam>
```

### What NOT to Document fully
- Private members (unless they have complex logic requiring internal documentation)
- Use `/// <inheritdoc/>` for overrides and obvious members where the name says it all

When something *smells* like it needs more — non-obvious behavior, a constraint, a side effect, a gotcha —
write actual documentation instead. Use judgment on a case-by-case basis.

### Special Considerations
- For dependency injection, document the expected service lifetime and dependencies
- For performance-critical code, include performance characteristics in `<remarks>`
- For thread-safe classes, document thread safety guarantees

## Output Requirements
- Preserve all existing code functionality
- Add XML documentation without changing any logic
- Ensure all public/protected members are documented
- Use consistent terminology throughout the class
- Validate that all referenced types in `<see cref="">` tags exist and are accessible

