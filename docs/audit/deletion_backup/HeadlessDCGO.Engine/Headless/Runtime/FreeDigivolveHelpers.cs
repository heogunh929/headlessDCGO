// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/CardController.cs::진화 정상 아암 (payCost:false Blast/Arts): permanent=_targetPermanent; permanent.AddCardSource(card) — Permanent 객체 지속(top swap)@1365
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (D-6 Blast / Arts Digivolve) A costless single-target digivolve. Both AS-IS keywords digivolve a card
/// onto a Digimon with <c>payCost: false</c>: Blast Digivolve is a triggered (opponent-turn) reaction,
/// Arts Digivolve is an option that selects a target — the digivolve mechanic is identical, only the cost
/// is skipped and the trigger/selection differs (authored at porting time).
///
/// This reuses the fusion primitive (<see cref="FusionDigivolveHelpers.FuseAsync"/>) with a single
/// material — which produces the same source ordering as a normal digivolve (target, then its sources,
/// then the card's) — but, unlike a Jogress, the result INHERITS the target's summoning-sickness state
/// (a normal digivolve keeps the same permanent's field time). No memory is paid.
/// </summary>
public static class FreeDigivolveHelpers
{
    public const string EnteredThisTurnKey = "enteredThisTurn";

    /// <summary>
    /// Digivolve <paramref name="cardId"/> (from <paramref name="fromZone"/>, default Hand) onto
    /// <paramref name="targetCardId"/> for free. Returns true when performed.
    /// </summary>
    public static async Task<bool> DigivolveFreeAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        HeadlessEntityId targetCardId,
        ChoiceZone fromZone = ChoiceZone.Hand,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(targetCardId, out CardInstanceRecord? target) || target is null ||
            !repository.TryGetInstance(cardId, out CardInstanceRecord? _))
        {
            return false;
        }

        // A normal digivolve keeps the permanent's field time: inherit the target's entered-this-turn.
        bool inheritedSick = ReadFlag(target.Metadata, EnteredThisTurnKey);

        IReadOnlyList<HeadlessEntityId> merged = await FusionDigivolveHelpers.FuseAsync(
            repository,
            zoneMover,
            cardId,
            fromZone,
            new[] { targetCardId },
            materialFromZone: ChoiceZone.BattleArea,
            gameEventQueue: gameEventQueue,
            enteredThisTurnOverride: inheritedSick,
            cancellationToken: cancellationToken,
            // (RD-R3-02) AS-IS Blast/Arts run the NORMAL evolution arm (`permanent = _targetPermanent;
            // permanent.AddCardSource(card)`, CardController.cs:1372-1376, payCost:false) — the AS-IS
            // Permanent object PERSISTS across the top swap, so the fuse must ReKey the just-after
            // bookkeeping instead of letting the chokepoint Reset it.
            permanentContinuity: true).ConfigureAwait(false);

        return merged.Count > 0;
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool b && b;
}
