namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Text.Json;

// TODO: Replace JSONL DTOs with the final trainer interchange format after schema stabilizes.
public sealed class RlTrainingDatasetJsonlExporter(
    RlTrainingDatasetJsonlExportOptions? options = null)
{
    private readonly RlTrainingDatasetJsonlExportOptions _options =
        options ?? RlTrainingDatasetJsonlExportOptions.Default;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RlTrainingDatasetJsonlExportResult Export(RlTrainingDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        IReadOnlyList<string> lines = ExportLines(dataset);
        return new RlTrainingDatasetJsonlExportResult(
            lines,
            string.Join(Environment.NewLine, lines));
    }

    public IReadOnlyList<string> ExportLines(RlTrainingDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        List<string> lines = new();

        if (_options.IncludeSchemaLine)
        {
            lines.Add(Serialize(new RlDatasetSchemaJsonlLine(
                Type: "schema",
                Name: dataset.Name,
                ExecutionMode: dataset.ExecutionMode,
                HasConsistentSchema: dataset.HasConsistentSchema,
                ObservationFeatureNames: dataset.Schema?.ObservationFeatureNames ?? Array.Empty<string>(),
                ActionSlotNames: dataset.Schema?.ActionSlotNames ?? Array.Empty<string>())));
        }

        if (_options.IncludeEpisodeLines)
        {
            foreach (RlTrainingDatasetEpisode episode in dataset.Episodes)
            {
                lines.Add(Serialize(new RlDatasetEpisodeJsonlLine(
                    Type: "episode",
                    Name: dataset.Name,
                    ExecutionMode: dataset.ExecutionMode,
                    RunIndex: episode.RunIndex,
                    RandomSeed: episode.RandomSeed,
                    Fingerprint: episode.Fingerprint,
                    StepCount: episode.StepCount,
                    SampleCount: episode.SampleCount,
                    TotalReward: episode.TotalReward,
                    IsTerminal: episode.IsTerminal,
                    StopReason: episode.StopReason.ToString())));
            }
        }

        if (_options.IncludeSampleLines)
        {
            foreach (RlTrainingDatasetSample sample in dataset.Samples)
            {
                lines.Add(Serialize(new RlDatasetSampleJsonlLine(
                    Type: "sample",
                    Name: dataset.Name,
                    ExecutionMode: dataset.ExecutionMode,
                    RunIndex: sample.RunIndex,
                    RandomSeed: sample.RandomSeed,
                    EpisodeFingerprint: sample.EpisodeFingerprint,
                    Sample: sample.Sample)));
            }
        }

        return lines.ToArray();
    }

    public async Task WriteAsync(
        TextWriter writer,
        RlTrainingDataset dataset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(dataset);

        foreach (string line in ExportLines(dataset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, _jsonOptions);
    }
}

public sealed record RlTrainingDatasetJsonlExportOptions
{
    public static RlTrainingDatasetJsonlExportOptions Default { get; } = new();

    public bool IncludeSchemaLine { get; init; } = true;

    public bool IncludeEpisodeLines { get; init; } = true;

    public bool IncludeSampleLines { get; init; } = true;
}

public sealed record RlTrainingDatasetJsonlExportResult(
    IReadOnlyList<string> Lines,
    string Content)
{
    public int LineCount => Lines.Count;
}

public sealed record RlDatasetSchemaJsonlLine(
    string Type,
    string Name,
    string ExecutionMode,
    bool HasConsistentSchema,
    IReadOnlyList<string> ObservationFeatureNames,
    IReadOnlyList<string> ActionSlotNames);

public sealed record RlDatasetEpisodeJsonlLine(
    string Type,
    string Name,
    string ExecutionMode,
    int RunIndex,
    int RandomSeed,
    string Fingerprint,
    int StepCount,
    int SampleCount,
    double TotalReward,
    bool IsTerminal,
    string StopReason);

public sealed record RlDatasetSampleJsonlLine(
    string Type,
    string Name,
    string ExecutionMode,
    int RunIndex,
    int RandomSeed,
    string EpisodeFingerprint,
    RlTransitionSample Sample);

public sealed class RlTrainingDatasetJsonlValidator
{
    public RlTrainingDatasetJsonlValidationResult Validate(
        RlTrainingDataset dataset,
        IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(lines);

        List<RlTrainingDatasetJsonlValidationFailure> failures = new();
        int schemaLineCount = 0;
        int episodeLineCount = 0;
        int sampleLineCount = 0;

        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                failures.Add(new RlTrainingDatasetJsonlValidationFailure(
                    index + 1,
                    "Line",
                    "non-empty JSON object",
                    "<empty>"));
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    failures.Add(new RlTrainingDatasetJsonlValidationFailure(
                        index + 1,
                        "Json",
                        "object",
                        document.RootElement.ValueKind.ToString()));
                    continue;
                }

                string? type = ReadString(document.RootElement, "type");
                switch (type)
                {
                    case "schema":
                        schemaLineCount++;
                        break;
                    case "episode":
                        episodeLineCount++;
                        ValidateRequiredString(
                            document.RootElement,
                            "stopReason",
                            index + 1,
                            failures);
                        break;
                    case "sample":
                        sampleLineCount++;
                        break;
                    default:
                        failures.Add(new RlTrainingDatasetJsonlValidationFailure(
                            index + 1,
                            "type",
                            "schema|episode|sample",
                            type ?? "<missing>"));
                        break;
                }
            }
            catch (JsonException exception)
            {
                failures.Add(new RlTrainingDatasetJsonlValidationFailure(
                    index + 1,
                    "Json",
                    "valid JSON",
                    exception.Message));
            }
        }

        ValidateCount("schema lines", expected: 1, schemaLineCount, failures);
        ValidateCount("episode lines", dataset.EpisodeCount, episodeLineCount, failures);
        ValidateCount("sample lines", dataset.SampleCount, sampleLineCount, failures);

        return new RlTrainingDatasetJsonlValidationResult(
            lines.Count,
            schemaLineCount,
            episodeLineCount,
            sampleLineCount,
            failures.ToArray());
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void ValidateRequiredString(
        JsonElement root,
        string propertyName,
        int lineNumber,
        List<RlTrainingDatasetJsonlValidationFailure> failures)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            failures.Add(new RlTrainingDatasetJsonlValidationFailure(
                lineNumber,
                propertyName,
                "non-empty string",
                "<missing>"));
            return;
        }

        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            failures.Add(new RlTrainingDatasetJsonlValidationFailure(
                lineNumber,
                propertyName,
                "non-empty string",
                value.ValueKind.ToString()));
        }
    }

    private static void ValidateCount(
        string field,
        int expected,
        int actual,
        List<RlTrainingDatasetJsonlValidationFailure> failures)
    {
        if (expected != actual)
        {
            failures.Add(new RlTrainingDatasetJsonlValidationFailure(
                LineNumber: 0,
                Field: field,
                Expected: expected.ToString(),
                Actual: actual.ToString()));
        }
    }
}

public sealed record RlTrainingDatasetJsonlValidationResult(
    int LineCount,
    int SchemaLineCount,
    int EpisodeLineCount,
    int SampleLineCount,
    IReadOnlyList<RlTrainingDatasetJsonlValidationFailure> Failures)
{
    public bool Passed => Failures.Count == 0;
}

public sealed record RlTrainingDatasetJsonlValidationFailure(
    int LineNumber,
    string Field,
    string Expected,
    string Actual);
