namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Replace this in-memory dataset with streaming export once large rollouts are supported.
public sealed record RlTrainingDataset(
    string Name,
    string ExecutionMode,
    RlVectorSchema? Schema,
    IReadOnlyList<RlTrainingDatasetSample> Samples,
    IReadOnlyList<RlTrainingDatasetEpisode> Episodes)
{
    public int SampleCount => Samples.Count;

    public int EpisodeCount => Episodes.Count;

    public bool HasConsistentSchema => Schema is not null;

    public double TotalReward => Episodes.Sum(episode => episode.TotalReward);

    public IReadOnlyDictionary<HeadlessEpisodeStopReason, int> StopReasonCounts => Episodes
        .GroupBy(episode => episode.StopReason)
        .ToDictionary(group => group.Key, group => group.Count());

    public int CountByStopReason(HeadlessEpisodeStopReason stopReason)
    {
        return Episodes.Count(episode => episode.StopReason == stopReason);
    }

    public RlTrainingDatasetJsonlExportResult ToJsonlExport(
        RlTrainingDatasetJsonlExportOptions? options = null)
    {
        return new RlTrainingDatasetJsonlExporter(options).Export(this);
    }

    public IReadOnlyList<string> ToJsonLines(
        RlTrainingDatasetJsonlExportOptions? options = null)
    {
        return new RlTrainingDatasetJsonlExporter(options).ExportLines(this);
    }

    public RlTrainingDatasetJsonlValidationResult ValidateJsonLines(
        RlTrainingDatasetJsonlExportOptions? options = null)
    {
        IReadOnlyList<string> lines = ToJsonLines(options);
        return new RlTrainingDatasetJsonlValidator().Validate(this, lines);
    }

    public static RlTrainingDataset FromBatch(HeadlessEpisodeBatchResult batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        RlVectorSchema? schema = batch.HasConsistentSchema && batch.Episodes.Count > 0
            ? batch.Episodes[0].Episode.ToSampleBatch().Schema
            : null;

        List<RlTrainingDatasetSample> samples = new();
        List<RlTrainingDatasetEpisode> episodes = new();

        foreach (HeadlessEpisodeBatchItemResult item in batch.Episodes)
        {
            RlEpisodeSampleBatch sampleBatch = item.Episode.ToSampleBatch();
            episodes.Add(new RlTrainingDatasetEpisode(
                item.RunIndex,
                item.RandomSeed,
                item.Fingerprint.Value,
                item.Episode.StepCount,
                sampleBatch.Count,
                item.Episode.TotalReward,
                item.Episode.IsTerminal,
                item.Episode.StopReason));

            foreach (RlTransitionSample sample in sampleBatch.Samples)
            {
                samples.Add(new RlTrainingDatasetSample(
                    item.RunIndex,
                    item.RandomSeed,
                    item.Fingerprint.Value,
                    sample));
            }
        }

        return new RlTrainingDataset(
            batch.Name,
            batch.ExecutionMode,
            schema,
            samples.ToArray(),
            episodes.ToArray());
    }
}

public sealed record RlTrainingDatasetSample(
    int RunIndex,
    int RandomSeed,
    string EpisodeFingerprint,
    RlTransitionSample Sample);

public sealed record RlTrainingDatasetEpisode(
    int RunIndex,
    int RandomSeed,
    string Fingerprint,
    int StepCount,
    int SampleCount,
    double TotalReward,
    bool IsTerminal,
    HeadlessEpisodeStopReason StopReason);
