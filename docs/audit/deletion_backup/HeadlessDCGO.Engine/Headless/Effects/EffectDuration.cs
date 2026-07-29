// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/ICardEffect.cs::EffectDuration (enum)@ICardEffect.cs:953
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

/// <summary>
/// (CV-A1 / F-1) How long a temporary effect or modifier lasts before it is cleaned up. Mirrors the
/// original DCGO <c>EffectDuration</c> enum (<c>ICardEffect.cs</c>). A continuous <see cref="EffectBinding"/>
/// carries an optional <see cref="EffectBinding.Duration"/>; a null duration means the effect is permanent
/// (static) and never auto-expires. <see cref="EffectDurationExpiry"/> removes bindings at the matching
/// expiry point (turn end, battle end, attack end, next unsuspend, fixed-cost calc).
/// </summary>
public enum EffectDuration
{
    /// <summary>Until the end of the current/next turn, whoever's turn it is.</summary>
    UntilEachTurnEnd = 0,

    /// <summary>Until the end of the controller's (owner's) turn.</summary>
    UntilOwnerTurnEnd = 1,

    /// <summary>Until the end of the opponent's turn.</summary>
    UntilOpponentTurnEnd = 2,

    /// <summary>Until the current attack finishes resolving.</summary>
    UntilEndAttack = 3,

    /// <summary>Until the current battle finishes resolving.</summary>
    UntilEndBattle = 4,

    /// <summary>Until the controller's next active (unsuspend) phase.</summary>
    UntilOwnerActivePhase = 5,

    /// <summary>Until the affected permanent next unsuspends.</summary>
    UntilNextUntap = 6,

    /// <summary>Until the fixed-cost calculation completes (cost-modifier scoped).</summary>
    UntilCalculateFixedCost = 7,
}
