namespace HeadlessDCGO.Engine.Headless.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonConverter(typeof(HeadlessEntityIdJsonConverter))]
public readonly record struct HeadlessEntityId
{
    public HeadlessEntityId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static HeadlessEntityId Parse(string value)
    {
        return new HeadlessEntityId(value);
    }

    public static bool TryParse(string? value, out HeadlessEntityId id)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            id = default;
            return false;
        }

        id = new HeadlessEntityId(value);
        return true;
    }

    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}

public sealed class HeadlessEntityIdJsonConverter : JsonConverter<HeadlessEntityId>
{
    public override HeadlessEntityId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("HeadlessEntityId must be encoded as a string.");
        }

        return new HeadlessEntityId(reader.GetString()!);
    }

    public override void Write(
        Utf8JsonWriter writer,
        HeadlessEntityId value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
