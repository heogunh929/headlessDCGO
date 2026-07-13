// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_095.cs — a Red Option (two independent timings).
// 1:1 mirror of the AS-IS BT1_095.
//   [Main] (OptionSkill) "Unsuspend 1 of your Digimon. Until the end of your opponent's next turn, that Digimon
//     gains <Blocker>." ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//     CanSelectPermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon. ActivateCoroutine (guarded by
//     HasMatchConditionPermanent): SelectPermanentEffect.SetUp(mode: UnTap — unsuspends the pick, maxCount =
//     Min(1, count), canNoSelect:false, canEndNotMax:false, afterSelectPermanentCoroutine): per selected permanent
//     CardEffectCommons.GainBlocker(targetPermanent: permanent, EffectDuration.UntilOpponentTurnEnd, activateClass).
//   [Security] (SecuritySkill) "Unsuspend 1 of your Digimon. That Digimon gains <Blocker> for the turn." An
//     INDEPENDENT ActivateClass (CanUseCondition = CanTriggerSecurityEffect) — structurally identical select
//     (Mode.UnTap) but GainBlocker uses a DIFFERENT duration: EffectDuration.UntilEachTurnEnd ("for the turn").
//     NOT an AddActivateMainOptionSecurityEffect reuse-Main case (the durations genuinely diverge).
// Headless mirror: two uniform ActivatedEffects (one per timing) with SelectBody(Mode.UnTap) — the UnTap mode
//   applies the AS-IS unsuspend mutation to the pick, and the AS-IS afterSelectPermanentCoroutine follow-up is
//   wired via SelectBody.onEachSelected -> GainBlocker on the picked id with the branch-specific duration.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_095 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.UnTap,
                    description: "[Main] Unsuspend 1 of your Digimon. Until the end of your opponent's next turn, that Digimon gains <Blocker>.",
                    onEachSelected: id => CardEffectCommons.GainBlocker(
                        new Permanent(card.Context, id, card.Owner), EffectDuration.UntilOpponentTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Unsuspend 1 of your Digimon. Until the end of your opponent's next turn, that Digimon gains <Blocker>."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.SecuritySkill,
                canUse: ctx => CardEffectCommons.CanTriggerSecurityEffect(ctx, card),
                canActivate: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.UnTap,
                    description: "[Security] Unsuspend 1 of your Digimon. That Digimon gains <Blocker> for the turn.",
                    onEachSelected: id => CardEffectCommons.GainBlocker(
                        new Permanent(card.Context, id, card.Owner), EffectDuration.UntilEachTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Security] Unsuspend 1 of your Digimon. That Digimon gains <Blocker> for the turn."));
        }

        return cardEffects;
    }
}
