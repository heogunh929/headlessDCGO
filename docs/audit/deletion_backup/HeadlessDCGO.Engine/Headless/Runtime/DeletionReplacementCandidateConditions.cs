// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Scapegoat.cs::키워드 effect가 받는 Func<Permanent,bool> permanentCondition / CanSelectPermanentCondition 클로저 (Decoy.cs·Scapegoat.cs·Save.cs)@Scape
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (#3 porting-readiness seam) The card-specific candidate condition for a deletion-replacement
/// sub-selection. AS-IS keyword effects (Decoy/Scapegoat/Save …) take a <c>Func&lt;Permanent,bool&gt;
/// permanentCondition</c> closure supplied by the individual card (e.g. "only red allies", "only a
/// Tamer"); when null the candidate set is generic (any eligible owner battle-area card). The headless
/// candidate enumerators (<see cref="DeletionReplacementGate"/>) mirror that with an optional
/// <c>Func&lt;CardInstanceRecord,bool&gt;</c> predicate, and <see cref="DeletionReplacementTiming"/>
/// resolves the per-card predicate through this service.
///
/// No card ports a conditional deletion-replacement keyword yet, so the default
/// <see cref="NoDeletionReplacementCandidateConditions"/> returns null (generic) and the engine
/// behaves exactly as before. When porting such a card, register a card-aware implementation via
/// <c>EngineContext.RegisterService&lt;IDeletionReplacementCandidateConditions&gt;(…)</c> — the
/// enumeration seam is already in place, so no engine refactor is needed.
/// </summary>
public interface IDeletionReplacementCandidateConditions
{
    /// <summary>The candidate predicate the holder's <paramref name="option"/> imposes on each
    /// candidate record (matching the <c>DeletionReplacementTiming.*Option</c> constants), or null
    /// for the generic (unconstrained) candidate set.</summary>
    Func<CardInstanceRecord, bool>? Resolve(CardInstanceRecord holder, string option);
}

/// <summary>The default: every deletion-replacement candidate set is generic (no card-specific
/// condition). Used whenever no card-aware resolver is registered on the context.</summary>
public sealed class NoDeletionReplacementCandidateConditions : IDeletionReplacementCandidateConditions
{
    public static NoDeletionReplacementCandidateConditions Instance { get; } = new();

    private NoDeletionReplacementCandidateConditions()
    {
    }

    public Func<CardInstanceRecord, bool>? Resolve(CardInstanceRecord holder, string option) => null;
}

/// <summary>A delegate-backed resolver — the convenient way for a ported card (or a test) to register
/// per-(holder, option) candidate conditions without a bespoke class.</summary>
public sealed class DelegateDeletionReplacementCandidateConditions : IDeletionReplacementCandidateConditions
{
    private readonly Func<CardInstanceRecord, string, Func<CardInstanceRecord, bool>?> _resolver;

    public DelegateDeletionReplacementCandidateConditions(
        Func<CardInstanceRecord, string, Func<CardInstanceRecord, bool>?> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public Func<CardInstanceRecord, bool>? Resolve(CardInstanceRecord holder, string option)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(option);
        return _resolver(holder, option);
    }
}
