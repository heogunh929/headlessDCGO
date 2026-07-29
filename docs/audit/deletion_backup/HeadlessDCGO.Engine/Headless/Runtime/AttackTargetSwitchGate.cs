// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/Permanent.cs::Permanent.CanSwitchAttackTarget@3746
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (AD1-S) Mirror of the AS-IS chokepoint <c>Permanent.CanSwitchAttackTarget</c> (Permanent.cs:3745-3792):
/// scans every active <c>ICanNotSwitchAttackTargetEffect</c> and, when one's predicate matches the
/// ATTACKING permanent, the attack target is LOCKED. AS-IS gates exactly two actions with it — both are
/// wired here: block eligibility (Permanent.cs:2156 → <see cref="BlockTiming"/> offers no blocker
/// candidates) and <c>AttackProcess.SwitchDefender</c> (AttackProcess.cs:519, shared by blocker-redirect
/// AND effect retargets → the printed-Raid retarget (<c>RaidProcess</c>) and any future retarget effect must
/// consult this gate before switching).
/// </summary>
public static class AttackTargetSwitchGate
{
    public static bool IsLocked(EngineContext context, HeadlessEntityId attackerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (attackerId.IsEmpty ||
            !context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) ||
            attacker is null)
        {
            return false;
        }

        // (P6C3, stage-B preview) Re-anchored to the ported kind-class surface: AS-IS
        // Permanent.CanSwitchAttackTarget (Permanent.cs:3745-3792) scans every field permanent's and every
        // player's EffectList(EffectTiming.None) for ICanNotSwitchAttackTargetEffect, gates each with
        // CanUse(null), and asks CanNotBeSwitchAttackTarget(attacker). IsLocked == !CanSwitchAttackTarget.
        // (The pre-flip registry-key read is dead: no producer writes those binding keys since the kind-class
        // 1:1 rebuild.)
        var attackerView = new Permanent(context, attackerId, attacker.OwnerId);
        foreach (Player player in new GameContext(context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotSwitchAttackTargetEffect gate && cardEffect.CanUse(null) &&
                        gate.CanNotBeSwitchAttackTarget(attackerView))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotSwitchAttackTargetEffect gate && cardEffect.CanUse(null) &&
                    gate.CanNotBeSwitchAttackTarget(attackerView))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
