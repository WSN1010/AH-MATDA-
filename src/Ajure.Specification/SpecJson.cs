using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ajure.Specification;

/// <summary>Shared JSON contract for every persisted specification payload.</summary>
public static class SpecJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize<TValue>(TValue value) => JsonSerializer.Serialize(value, Options);

    public static TValue Deserialize<TValue>(string json) =>
        JsonSerializer.Deserialize<TValue>(json, Options)
        ?? throw new JsonException("The payload deserialized to null.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

/// <summary>
/// Canonical JSON: object members sorted by ordinal name, no insignificant whitespace, UTF-8.
/// Two structurally equal payloads always produce the same bytes and therefore the same hash.
/// </summary>
public static class CanonicalJson
{
    public static string Serialize<TValue>(TValue value) =>
        Encoding.UTF8.GetString(SerializeToUtf8Bytes(value));

    public static byte[] SerializeToUtf8Bytes<TValue>(TValue value)
    {
        var node = JsonSerializer.SerializeToNode(value, SpecJson.Options);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            WriteCanonical(node, writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Lowercase hexadecimal SHA-256 of the canonical JSON form.</summary>
    public static string ComputeHash<TValue>(TValue value) => ContentHash.OfBytes(SerializeToUtf8Bytes(value));

    private static void WriteCanonical(JsonNode? node, Utf8JsonWriter writer)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var property in obj.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            default:
                node.WriteTo(writer, SpecJson.Options);
                break;
        }
    }
}

/// <summary>SHA-256 content hashes used by artifacts, manifests and stale detection.</summary>
public static class ContentHash
{
    public static string OfBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string OfText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return OfBytes(Encoding.UTF8.GetBytes(text));
    }
}
