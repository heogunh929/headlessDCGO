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

    // (B5-1, 설계 §B5.4) Partial-selection session scratchpad: the candidates the chooser has toggled ON
    // so far, in PICK ORDER (the substrate translation of the AS-IS selection session's local accumulator
    // `List<T> PreSelectedPermanents` / `PreSelectedHandCards` — SelectPermanentEffect.cs:362,
    // SelectHandEffect.cs:253; pick order is semantic there: SetSelectedIndexText numbers picks 1..n and
    // order-sensitive modes such as PutLibraryBottom consume the list in pick order). May be non-empty only
    // while IsPending; Resolve/Clear always empties it. NOT yet exposed on any observation/serialization
    // surface (that is B5-3); the dispatcher does not populate it until B5-2 — in B5-1 every real
    // trajectory keeps it empty, so existing behavior is bit-identical.
    public IReadOnlyList<HeadlessEntityId> PendingSelectedIds { get; init; } = Array.Empty<HeadlessEntityId>();

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
