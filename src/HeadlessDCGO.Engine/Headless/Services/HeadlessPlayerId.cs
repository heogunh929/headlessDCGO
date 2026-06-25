namespace HeadlessDCGO.Engine.Headless.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonConverter(typeof(HeadlessPlayerIdJsonConverter))]
public readonly record struct HeadlessPlayerId
{
    public HeadlessPlayerId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Player id must be positive.");
        }

        Value = value;
    }

    public int Value { get; }

    public bool IsEmpty => Value == 0;

    public static HeadlessPlayerId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return int.TryParse(value, out int parsed)
            ? new HeadlessPlayerId(parsed)
            : throw new FormatException("Player id must be an integer.");
    }

    public static bool TryParse(string? value, out HeadlessPlayerId id)
    {
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            id = default;
            return false;
        }

        id = new HeadlessPlayerId(parsed);
        return true;
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}

public sealed class HeadlessPlayerIdJsonConverter : JsonConverter<HeadlessPlayerId>
{
    public override HeadlessPlayerId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intValue))
        {
            return new HeadlessPlayerId(intValue);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return HeadlessPlayerId.Parse(reader.GetString()!);
        }

        throw new JsonException("HeadlessPlayerId must be encoded as an integer or integer string.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        HeadlessPlayerId value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
