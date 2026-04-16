---
paths:
  - "src/GroundControl.Persistence.Abstractions/**/*.cs"
  - "src/GroundControl.Persistence.MongoDb/**/*.cs"
---

# Data Access — Store Pattern

One specific store interface per entity (no generic base). Methods capture business semantics (e.g., `GetByDimensionAsync`, `HasDependentsAsync`). Optimistic concurrency via `expectedVersion` parameter — updates/deletes filter on both ID and expected version, increment version on success. List operations use `ListQuery` with cursor-based pagination via `MongoCursorPagination` helpers.

MongoDB used directly via `IMongoCollection<T>` with LINQ/Builders — no ORM layer. Per-collection index setup via `DocumentConfiguration<T>` (implements `IDocumentConfiguration`), registered as singleton via `TryAddEnumerable` and run on startup by `MongoIndexSetupService`. Case-insensitive collation from `IMongoDbContext.DefaultCollation` applied to string sorts and unique indexes.

**ETag/Concurrency flow:** GET returns `ETag` header (version). PUT/DELETE require `If-Match` header, parsed via `EntityTagHeaders.TryParseIfMatch()`. Version mismatch -> 409 Conflict. Missing header -> 428 Precondition Required.