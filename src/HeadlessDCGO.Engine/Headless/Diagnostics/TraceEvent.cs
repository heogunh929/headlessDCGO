namespace HeadlessDCGO.Engine.Headless.Diagnostics;

using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

public sealed record TraceEvent
{
    public TraceEvent(
        long sequence,
        string category,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Trace sequence must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);

        Sequence = sequence;
        Category = category.Trim();
        Message = message;
        Metadata = CopyMetadata(metadata ?? ReadOnlyDictionary<string, object?>.Empty);
    }

    public long Sequence { get; init; }

    public string Category { get; init; }

    public string Message { get; init; }

    public IReadOnlyDictionary<string, object?> Metadata { get; init; }

    internal void AppendFingerprintData(StringBuilder builder)
    {
        builder
            .Append(Sequence.ToString(CultureInfo.InvariantCulture))
            .Append('\u001f')
            .Append(Category)
            .Append('\u001f')
            .Append(Message)
            .Append('\u001f');

        AppendMetadata(builder, Metadata);
        builder.Append('\n');
    }

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
    }

    private static void AppendMetadata(StringBuilder builder, IReadOnlyDictionary<string, object?> metadata)
    {
        builder.Append('{');
        bool first = true;
        foreach ((string key, object? value) in metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(Escape(key)).Append(':');
            AppendValue(builder, value);
        }

        builder.Append('}');
    }

    private static void AppendValue(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case string stringValue:
                builder.Append(Escape(stringValue));
                break;
            case bool boolValue:
                builder.Append(boolValue ? "true" : "false");
                break;
            case IReadOnlyDictionary<string, object?> dictionary:
                AppendMetadata(builder, dictionary);
                break;
            case IDictionary rawDictionary:
                AppendRawDictionary(builder, rawDictionary);
                break;
            case IEnumerable enumerable when value is not string:
                AppendEnumerable(builder, enumerable);
                break;
            case IFormattable formattable:
                builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                builder.Append(Escape(value.ToString() ?? string.Empty));
                break;
        }
    }

    private static void AppendRawDictionary(StringBuilder builder, IDictionary dictionary)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            values[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = entry.Value;
        }

        AppendMetadata(builder, values);
    }

    private static void AppendEnumerable(StringBuilder builder, IEnumerable enumerable)
    {
        builder.Append('[');
        bool first = true;
        foreach (object? item in enumerable)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            AppendValue(builder, item);
        }

        builder.Append(']');
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\u001f", "\\u001f", StringComparison.Ordinal);
    }
}
