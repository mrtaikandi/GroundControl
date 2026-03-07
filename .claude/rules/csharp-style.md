---
paths:
  - "**/*.cs"
---

# C# Style Guide

These rules apply to all C# files in this project. Follow them exactly when writing or modifying code.

## Formatting Basics

- **Indent**: 4 spaces (no tabs)
- **Line endings**: CRLF
- **Charset**: UTF-8
- **No final newline** at end of file. **Do not end files with any whitespace or blank lines.**
- **Trim trailing whitespace**
- **Max line length**: 180 characters

## Braces and New Lines (Allman Style)

- Opening braces go on a **new line** for all constructs (classes, methods, properties, control flow, etc.)
- `else`, `catch`, `finally` each go on a **new line**
- Braces are **always required** for `if`, `else`, `for`, `foreach`, `while`, `do-while`, `lock`, `using`, `fixed`

## `var` Usage

- **Always use `var`** everywhere, including built-in types unless when using collection expressions.

```csharp
// CORRECT
var count = 5;
var name = "hello";
var items = new List<string>();
List<string> items2 = ["a", "b", "c"]; // collection expression allows explicit type

// WRONG
int count = 5;
string name = "hello";
List<string> items = new List<string>();
```

## `this.` Qualification

- **Never use `this.`** qualifier for fields, properties, methods, or events.

## Type Keywords vs Framework Types

- **Always use language keywords** instead of framework type names (`int` not `Int32`, `string` not `String`, `object` not `Object`).

## Naming Conventions

| Symbol | Style | Example |
|---|---|---|
| Namespaces, classes, structs, enums, delegates | PascalCase | `MyClass`, `OrderStatus` |
| Interfaces | `I` + PascalCase | `IOrderService` |
| Type parameters | `T` + PascalCase | `TResult`, `TKey` |
| Methods, properties, events, constants (public) | PascalCase | `GetOrder()`, `IsActive` |
| Local functions | PascalCase | `ValidateInput()` |
| Parameters, locals | camelCase | `orderId`, `itemCount` |
| Private instance fields | `_` + camelCase | `_orderService`, `_count` |
| Private static fields | `_` + camelCase | `_instance`, `_logger` |
| Private static readonly fields | PascalCase | `DefaultTimeout` |
| Private constants | PascalCase | `MaxRetries` |
| Local constants | PascalCase | `MaxItems` |
| Enum members | PascalCase | `Active`, `Pending` |

## Accessibility Modifiers

- **Always specify accessibility modifiers** on non-interface members (e.g., always write `private`, `public`, `internal`).
- Preferred modifier order: `public, private, protected, internal, new, abstract, virtual, sealed, override, static, readonly, extern, unsafe, volatile, async`

## Expression-Bodied Members

- Use expression-bodied syntax **when on a single line** for methods, properties, accessors, constructors, indexers, lambdas, operators, and local functions.
- Constructors use **block body** style.

```csharp
// CORRECT - single line expression body
public string Name => _name;
public int GetCount() => _items.Count;

// CORRECT - constructor uses block body
public MyClass(string name)
{
    _name = name;
}
```

## Usings

- Sort `System` directives first
- Place `using` directives **outside** the namespace
- Remove unused `using` directives

## Null Checking and Pattern Matching

- Use `is null` / `is not null` instead of `== null` / `!= null`
- Prefer pattern matching over `as` with null check
- Prefer pattern matching over `is` with cast check
- Use null propagation (`?.`) and null coalescing (`??`)
- Use conditional delegate calls (`?.Invoke`)
- Use throw expressions: `_name = name ?? throw new ArgumentNullException(nameof(name))`
- Prefer inlined variable declarations: `if (int.TryParse(s, out var result))`

## Expression Preferences

- Prefer object/collection initializers
- Prefer auto-properties
- Prefer compound assignment (`+=`, `??=`)
- Prefer conditional expressions over simple if/else assignments
- Prefer inferred tuple and anonymous type member names
- Prefer `default` over `default(T)`
- Prefer index operator (`^1`) and range operator (`..`)
- Use discard variables (`_`) for unused values

## Spacing

- **No space** after cast: `(int)value`
- **Space** after keywords in control flow: `if (`, `for (`, `while (`
- **Space** around binary operators: `a + b`, `x == y`
- **No space** between method name and parenthesis: `Method()`
- **Space** before and after colon in inheritance: `class Foo : Bar`
- **No space** inside parentheses: `Method(arg1, arg2)` not `Method( arg1, arg2 )`

## Blank Lines

- 1 blank line around types, methods, properties, auto-properties, fields
- 0 blank lines for single-line fields
- 1 blank line after `using` statements
- 1 blank line after block statements and control transfer statements

## Wrapping

- Chop arguments **if long** (not always)
- Chop parameters **if long**
- Wrap chained method calls **if long**
- Wrap before ternary operator signs
- Do not wrap before binary operator signs

## Fields

- Mark fields as `readonly` when possible

## Attributes

- Place attributes on a **separate line** from the member they decorate (not on same line)

```csharp
// CORRECT
[HttpGet]
[Route("/api/orders")]
public IActionResult GetOrders() => Ok();

// WRONG
[HttpGet] [Route("/api/orders")] public IActionResult GetOrders() => Ok();
```

## Documentation

- Do not include XML file headers
- Always add XML docs for `public` members and interfaces
- For `private` and `internal` members, only add XML docs if they add clarity beyond what the name conveys
- Readonly properties: summary starts with "Gets"
- Read-write properties: summary starts with "Gets or sets"
- Additional property documentation goes in `<remarks>`

```csharp
/// <summary>
/// Gets the user's display name.
/// </summary>
public string DisplayName { get; }

/// <summary>
/// Gets or sets the retry count.
/// </summary>
/// <remarks>Defaults to 3. Set to 0 to disable retries.</remarks>
public int RetryCount { get; set; }
```

## Comments

- Only write comments that explain **why**, not **what**. The code itself should be clear enough to convey what it does.
- Do not add comments that restate the code: `// increment counter` above `counter++` adds nothing.
- Do add comments for non-obvious business logic, workarounds, or intentional trade-offs.
- Do not add `// removed`, `// deprecated`, or change-log style comments. Use version control instead.
- Prefer renaming variables or extracting methods to make code self-documenting over adding explanatory comments.

## Test Code

- Structure tests with `// Arrange`, `// Act`, and `// Assert` comments
- Only add additional comments in tests if absolutely necessary to explain non-obvious setup or assertions

## Code Quality

- Use `async`/`await` correctly; avoid async void except for event handlers
- Be mindful of common performance pitfalls (unnecessary allocations, LINQ in hot paths)
- Avoid security issues (SQL injection, improper exception handling, secrets in code)