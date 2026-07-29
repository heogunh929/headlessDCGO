// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/CardController.cs::CardController play-card cleanup — Owner.UntilCalculateFixedCostEffect 리셋@CardController.cs:961
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (③-B) The EffectRegistry duration-sweep entry points (ExpireTurnEnd / ExpireBattleEnd / ExpireAttackEnd /
/// ExpireUnsuspend + the registry-only ExpireFixedCostCalc(registry)) are RETIRED: with the continuous-binding
/// producer at 0 every registry sweep was a dead write against a permanently-empty store. The live AS-IS duration
/// expiry is the per-carrier BUCKET reset at each choke (HeadlessEndTurnCleanupFlow player/permanent Until*TurnEnd
/// resets + TSM:256/259; BattleResolver UntilEndBattle reset; AttackProcess UntilEndAttack reset).
///
/// The sole surviving member is the fixed-cost-calc bucket reset (<see cref="ExpireFixedCostCalc"/>): AS-IS
/// <c>CardController.cs:961</c> clears <c>Player.UntilCalculateFixedCostEffect</c> on each play, so a bucket-form
/// BeforePayCost effect cannot leak into the next play of the same turn. Called at every pay-completion choke.
/// </summary>
public static class EffectDurationExpiry
{
    /// <summary>(R2-C) Expire the fixed-cost-calc duration on <paramref name="payer"/>'s per-play
    /// <c>Player.UntilCalculateFixedCostEffect</c> bucket (AS-IS <c>CardController.cs:961</c> clears the bucket on
    /// each play). Called at every pay-completion choke so a bucket-form BeforePayCost effect cannot leak into
    /// the next play of the same turn.</summary>
    public static void ExpireFixedCostCalc(HeadlessDCGO.Engine.Headless.Bridge.EngineContext context, HeadlessPlayerId payer)
    {
        ArgumentNullException.ThrowIfNull(context);
        new Assets.Scripts.Script.CardEffectCommons.Player(context, payer).UntilCalculateFixedCostEffect =
            new List<Func<Assets.Scripts.Script.CardEffectCommons.EffectTiming, Assets.Scripts.Script.CardEffectCommons.ICardEffect>>();
    }
}
