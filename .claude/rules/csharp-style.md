---
paths:
  - "**/*.cs"
---

# C# Style — Project Overrides

Follow standard C# / .NET conventions (Allman braces, PascalCase public, camelCase locals, `_camelCase` private fields, `is null`, pattern matching, expression-bodied members, etc.) with these project-specific overrides:

## Formatting

- **No final newline** — do not end files with blank lines or trailing whitespace
- **Max line length**: 180 characters
- **Braces always required** on `if`, `else`, `for`, `foreach`, `while`, `do-while`, `lock`, `using`, `fixed`

## `var` Usage

- **Always use `var`** everywhere, including built-in types, **unless** using a collection expression (which requires an explicit type)

```csharp
var count = 5;           // correct
var name = "hello";      // correct
List<string> items = ["a", "b"];  // collection expression — explicit type
```

## Naming

- **Private static readonly fields**: PascalCase (not `_camelCase`)

## Expression-Bodied Members

- Use expression body when single-line for methods, properties, accessors, lambdas, operators, local functions
- **Constructors always use block body**

## Wrapping

- Chop arguments/parameters **only when exceeding max line length** (not always)
- Wrap chained calls if long; wrap before ternary, not before binary operators

## Documentation

- **No XML file headers**
- Public members and interfaces: always add XML docs
- Private/internal: only if the name alone isn't clear enough
- Readonly properties: summary starts with **"Gets"**
- Read-write properties: summary starts with **"Gets or sets"**

## Comments

- Only explain **why**, not what — the code should be self-documenting
- No `// removed`, `// deprecated`, or changelog-style comments
- Tests: use `// Arrange`, `// Act`, `// Assert`