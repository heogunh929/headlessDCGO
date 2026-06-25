namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record HeadlessChoiceState(
    HeadlessEntityId? RequestId,
    ChoiceType Type,
    HeadlessPlayerId? PlayerId,
    string Message,
    int MinCount,
    int MaxCount,
    bool CanSkip,
    ChoiceZone SourceZone,
    int CandidateCount,
    bool IsPending,
    bool IsResolved,
    bool IsSkipped,
    int? SelectedCount,
    IReadOnlyList<HeadlessEntityId> SelectedIds)
{
    public static HeadlessChoiceState Empty { get; } = new(
        RequestId: null,
        Type: ChoiceType.Unknown,
        PlayerId: null,
        Message: string.Empty,
        MinCount: 0,
        MaxCount: 0,
        CanSkip: false,
        SourceZone: ChoiceZone.None,
        CandidateCount: 0,
        IsPending: false,
        IsResolved: false,
        IsSkipped: false,
        SelectedCount: null,
        SelectedIds: Array.Empty<HeadlessEntityId>());
}
