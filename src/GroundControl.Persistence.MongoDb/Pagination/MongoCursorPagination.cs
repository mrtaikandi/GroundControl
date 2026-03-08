using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using GroundControl.Persistence.Contracts;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Pagination;

internal static class MongoCursorPagination
{
    private const int SupportedCursorVersion = 1;

    public static string Encode(PagingCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        ValidateCursorMetadata(cursor.SortField, cursor.SortOrder, cursor.Version);
        EnsureSortValueSupported(cursor.SortValue);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteNumber("version", cursor.Version);
        writer.WriteString("id", cursor.Id);
        writer.WriteString("sortField", cursor.SortField);
        writer.WriteString("sortOrder", NormalizeSortOrder(cursor.SortOrder));
        writer.WritePropertyName("sortValue");
        writer.WriteStartObject();
        writer.WriteString("type", GetScalarTypeCode(cursor.SortValue));
        writer.WritePropertyName("value");
        WriteScalarValue(writer, cursor.SortValue);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();

        return Convert.ToBase64String(stream.ToArray());
    }

    public static PagingCursor Decode(string encodedCursor)
    {
        if (!TryDecode(encodedCursor, out var cursor, out var errorMessage))
        {
            throw new ValidationException(errorMessage);
        }

        return cursor;
    }

    public static bool TryDecode(string encodedCursor, [NotNullWhen(true)] out PagingCursor? cursor, out string? errorMessage)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(encodedCursor))
        {
            errorMessage = "Cursor is required.";
            return false;
        }

        byte[] buffer;
        try
        {
            buffer = Convert.FromBase64String(encodedCursor);
        }
        catch (FormatException)
        {
            errorMessage = "Cursor is not valid Base64.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(buffer);
            var root = document.RootElement;

            if (!root.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.Number || !versionElement.TryGetInt32(out var version))
            {
                errorMessage = "Cursor version is missing or invalid.";
                return false;
            }

            if (version != SupportedCursorVersion)
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture, "Cursor version '{0}' is not supported.", version);
                return false;
            }

            if (!root.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String || !idElement.TryGetGuid(out var id))
            {
                errorMessage = "Cursor id is missing or invalid.";
                return false;
            }

            if (!root.TryGetProperty("sortField", out var sortFieldElement) || sortFieldElement.ValueKind != JsonValueKind.String)
            {
                errorMessage = "Cursor sortField is missing or invalid.";
                return false;
            }

            if (!root.TryGetProperty("sortOrder", out var sortOrderElement) || sortOrderElement.ValueKind != JsonValueKind.String)
            {
                errorMessage = "Cursor sortOrder is missing or invalid.";
                return false;
            }

            if (!root.TryGetProperty("sortValue", out var sortValueElement) || sortValueElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Cursor sortValue is missing or invalid.";
                return false;
            }

            if (!sortValueElement.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                errorMessage = "Cursor sortValue type is missing or invalid.";
                return false;
            }

            if (!sortValueElement.TryGetProperty("value", out var valueElement))
            {
                errorMessage = "Cursor sortValue payload is missing.";
                return false;
            }

            var sortField = sortFieldElement.GetString();
            var sortOrder = sortOrderElement.GetString();
            if (string.IsNullOrWhiteSpace(sortField) || string.IsNullOrWhiteSpace(sortOrder))
            {
                errorMessage = "Cursor sort metadata is missing or invalid.";
                return false;
            }

            if (!TryReadScalarValue(typeElement.GetString(), valueElement, out var sortValue, out errorMessage))
            {
                return false;
            }

            cursor = new PagingCursor
            {
                Id = id,
                SortField = sortField,
                SortOrder = NormalizeSortOrder(sortOrder),
                SortValue = sortValue,
                Version = version
            };

            return true;
        }
        catch (JsonException)
        {
            errorMessage = "Cursor is not valid JSON.";
            return false;
        }
    }

    public static PagingCursor DecodeForQuery(ListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        ValidateQuery(query);

        var encodedCursor = GetActiveCursor(query);
        if (encodedCursor is null)
        {
            throw new ValidationException("Cursor is required.");
        }

        var cursor = Decode(encodedCursor);
        if (!string.Equals(cursor.SortField, query.SortField, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Cursor sortField does not match the requested sort field.");
        }

        if (!string.Equals(cursor.SortOrder, NormalizeSortOrder(query.SortOrder), StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Cursor sortOrder does not match the requested sort order.");
        }

        return cursor;
    }

    public static FilterDefinition<TDocument> BuildPageFilter<TDocument>(ListQuery query, string bsonSortField)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(bsonSortField);

        ValidateQuery(query);

        var encodedCursor = GetActiveCursor(query);
        if (encodedCursor is null)
        {
            return FilterDefinition<TDocument>.Empty;
        }

        var cursor = DecodeForQuery(query);
        var comparisonOperator = GetComparisonOperator(query);
        var sortValue = ToBsonValue(cursor.SortValue);
        var idValue = new BsonBinaryData(cursor.Id, GuidRepresentation.Standard);

        if (string.Equals(bsonSortField, "_id", StringComparison.Ordinal))
        {
            return new BsonDocument("_id", new BsonDocument(comparisonOperator, idValue));
        }

        return new BsonDocument("$or", new BsonArray
        {
            new BsonDocument(bsonSortField, new BsonDocument(comparisonOperator, sortValue)),
            new BsonDocument
            {
                { bsonSortField, sortValue },
                { "_id", new BsonDocument(comparisonOperator, idValue) }
            }
        });
    }

    public static SortDefinition<TDocument> BuildSort<TDocument>(ListQuery query, string bsonSortField)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(bsonSortField);

        ValidateQuery(query);

        var builder = Builders<TDocument>.Sort;
        var ascending = IsAscending(query.SortOrder);
        if (HasBefore(query))
        {
            ascending = !ascending;
        }

        if (string.Equals(bsonSortField, "_id", StringComparison.Ordinal))
        {
            return ascending ? builder.Ascending("_id") : builder.Descending("_id");
        }

        return ascending
            ? builder.Ascending(bsonSortField).Ascending("_id")
            : builder.Descending(bsonSortField).Descending("_id");
    }

    public static PagedResult<TItem> MaterializePage<TItem>(
        IReadOnlyList<TItem> items,
        ListQuery query,
        long totalCount,
        Func<TItem, object?> sortValueSelector,
        Func<TItem, Guid> idSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sortValueSelector);
        ArgumentNullException.ThrowIfNull(idSelector);

        ValidateQuery(query);

        var pageItems = items.Take(query.Limit).ToList();
        var hasProbeRow = items.Count > query.Limit;
        var isBeforeRequest = HasBefore(query);

        if (isBeforeRequest)
        {
            pageItems.Reverse();
        }

        var hasNext = isBeforeRequest ? pageItems.Count > 0 : hasProbeRow;
        var hasPrevious = isBeforeRequest ? hasProbeRow : HasAfter(query) && pageItems.Count > 0;

        return new PagedResult<TItem>
        {
            Items = pageItems,
            NextCursor = hasNext ? Encode(CreateCursor(pageItems[^1], query, sortValueSelector, idSelector)) : null,
            PreviousCursor = hasPrevious ? Encode(CreateCursor(pageItems[0], query, sortValueSelector, idSelector)) : null,
            TotalCount = totalCount
        };
    }

    private static PagingCursor CreateCursor<TItem>(TItem item, ListQuery query, Func<TItem, object?> sortValueSelector, Func<TItem, Guid> idSelector)
    {
        return new PagingCursor
        {
            Id = idSelector(item),
            SortField = query.SortField,
            SortOrder = NormalizeSortOrder(query.SortOrder),
            SortValue = sortValueSelector(item),
            Version = SupportedCursorVersion
        };
    }

    private static string? GetActiveCursor(ListQuery query) => HasAfter(query) ? query.After : HasBefore(query) ? query.Before : null;

    private static string GetComparisonOperator(ListQuery query)
    {
        var ascending = IsAscending(query.SortOrder);
        return HasBefore(query)
            ? ascending ? "$lt" : "$gt"
            : ascending ? "$gt" : "$lt";
    }

    private static bool HasAfter(ListQuery query) => !string.IsNullOrWhiteSpace(query.After);

    private static bool HasBefore(ListQuery query) => !string.IsNullOrWhiteSpace(query.Before);

    private static bool IsAscending(string sortOrder)
    {
        var normalizedSortOrder = NormalizeSortOrder(sortOrder);
        return string.Equals(normalizedSortOrder, "asc", StringComparison.Ordinal);
    }

    private static string NormalizeSortOrder(string? sortOrder)
    {
        if (string.IsNullOrWhiteSpace(sortOrder))
        {
            throw new ValidationException("SortOrder is required.");
        }

        var trimmedSortOrder = sortOrder.Trim();
        if (string.Equals(trimmedSortOrder, "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(trimmedSortOrder, "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        throw new ValidationException("SortOrder must be either 'asc' or 'desc'.");
    }

    private static void ValidateCursorMetadata(string sortField, string sortOrder, int version)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            throw new ValidationException("SortField is required.");
        }

        _ = NormalizeSortOrder(sortOrder);

        if (version != SupportedCursorVersion)
        {
            throw new ValidationException(string.Format(CultureInfo.InvariantCulture, "Cursor version '{0}' is not supported.", version));
        }
    }

    private static void ValidateQuery(ListQuery query)
    {
        Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true);

        if (string.IsNullOrWhiteSpace(query.SortField))
        {
            throw new ValidationException("SortField is required.");
        }

        _ = NormalizeSortOrder(query.SortOrder);
    }

    private static string GetScalarTypeCode(object? value)
    {
        return value switch
        {
            null => "null",
            string => "string",
            bool => "bool",
            byte => "byte",
            sbyte => "sbyte",
            short => "short",
            ushort => "ushort",
            int => "int",
            uint => "uint",
            long => "long",
            ulong => "ulong",
            float => "float",
            double => "double",
            decimal => "decimal",
            Guid => "guid",
            DateTime => "datetime",
            DateTimeOffset => "datetimeoffset",
            _ => throw new ValidationException($"Cursor sortValue type '{value.GetType().FullName}' is not supported.")
        };
    }

    private static void EnsureSortValueSupported(object? value)
    {
        _ = GetScalarTypeCode(value);
    }

    private static void WriteScalarValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case byte byteValue:
                writer.WriteNumberValue(byteValue);
                break;
            case sbyte sbyteValue:
                writer.WriteNumberValue(sbyteValue);
                break;
            case short shortValue:
                writer.WriteNumberValue(shortValue);
                break;
            case ushort ushortValue:
                writer.WriteNumberValue(ushortValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case uint uintValue:
                writer.WriteNumberValue(uintValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case ulong ulongValue:
                writer.WriteNumberValue(ulongValue);
                break;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                break;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            case Guid guidValue:
                writer.WriteStringValue(guidValue);
                break;
            case DateTime dateTimeValue:
                writer.WriteStringValue(dateTimeValue);
                break;
            case DateTimeOffset dateTimeOffsetValue:
                writer.WriteStringValue(dateTimeOffsetValue);
                break;
            default:
                throw new ValidationException($"Cursor sortValue type '{value.GetType().FullName}' is not supported.");
        }
    }

    private static bool TryReadScalarValue(string? typeCode, JsonElement valueElement, out object? value, out string? errorMessage)
    {
        value = null;

        switch (typeCode)
        {
            case "null" when valueElement.ValueKind == JsonValueKind.Null:
                errorMessage = null;
                return true;
            case "string" when valueElement.ValueKind == JsonValueKind.String:
                value = valueElement.GetString();
                errorMessage = null;
                return true;
            case "bool" when valueElement.ValueKind is JsonValueKind.True or JsonValueKind.False:
                value = valueElement.GetBoolean();
                errorMessage = null;
                return true;
            case "byte" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetByte(out var byteValue):
                value = byteValue;
                errorMessage = null;
                return true;
            case "sbyte" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetSByte(out var sbyteValue):
                value = sbyteValue;
                errorMessage = null;
                return true;
            case "short" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt16(out var shortValue):
                value = shortValue;
                errorMessage = null;
                return true;
            case "ushort" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetUInt16(out var ushortValue):
                value = ushortValue;
                errorMessage = null;
                return true;
            case "int" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt32(out var intValue):
                value = intValue;
                errorMessage = null;
                return true;
            case "uint" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetUInt32(out var uintValue):
                value = uintValue;
                errorMessage = null;
                return true;
            case "long" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt64(out var longValue):
                value = longValue;
                errorMessage = null;
                return true;
            case "ulong" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetUInt64(out var ulongValue):
                value = ulongValue;
                errorMessage = null;
                return true;
            case "float" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetSingle(out var floatValue):
                value = floatValue;
                errorMessage = null;
                return true;
            case "double" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDouble(out var doubleValue):
                value = doubleValue;
                errorMessage = null;
                return true;
            case "decimal" when valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDecimal(out var decimalValue):
                value = decimalValue;
                errorMessage = null;
                return true;
            case "guid" when valueElement.ValueKind == JsonValueKind.String && valueElement.TryGetGuid(out var guidValue):
                value = guidValue;
                errorMessage = null;
                return true;
            case "datetime" when valueElement.ValueKind == JsonValueKind.String && valueElement.TryGetDateTime(out var dateTimeValue):
                value = dateTimeValue;
                errorMessage = null;
                return true;
            case "datetimeoffset" when valueElement.ValueKind == JsonValueKind.String && valueElement.TryGetDateTimeOffset(out var dateTimeOffsetValue):
                value = dateTimeOffsetValue;
                errorMessage = null;
                return true;
            default:
                errorMessage = "Cursor sortValue type is not supported or the payload is invalid.";
                return false;
        }
    }

    private static BsonValue ToBsonValue(object? value)
    {
        return value switch
        {
            null => BsonNull.Value,
            Guid guidValue => new BsonBinaryData(guidValue, GuidRepresentation.Standard),
            string stringValue => new BsonString(stringValue),
            bool boolValue => BsonBoolean.Create(boolValue),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset => SerializeBsonValue(value),
            _ => throw new ValidationException($"Cursor sortValue type '{value.GetType().FullName}' is not supported.")
        };
    }

    private static BsonValue SerializeBsonValue(object value)
    {
        var document = new BsonDocument();
        using var writer = new BsonDocumentWriter(document);

        writer.WriteStartDocument();
        writer.WriteName("value");
        BsonSerializer.Serialize(writer, value.GetType(), value);
        writer.WriteEndDocument();

        return document["value"];
    }
}