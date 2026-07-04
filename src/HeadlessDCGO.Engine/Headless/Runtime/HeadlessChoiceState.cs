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
    // (M2, 설계 §5-2) Choice candidate instance ids, in the same order the choice request presented
    // them (= the ResolveChoice factored-lane candidate order). Exposed so the chooser's observation
    // can carry candidate identity; the game loop strips this for non-chooser perspectives.
    public IReadOnlyList<HeadlessEntityId> CandidateIds { get; init; } = Array.Empty<HeadlessEntityId>();

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
