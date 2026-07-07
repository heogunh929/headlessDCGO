// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_091.cs — an Option (single timing, no [Security]).
// 1:1 mirror of the AS-IS BT1_091 [Main] (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect,
//   ORDER=-1, ISOPTIONAL=false). ActivateCoroutine (guarded by HasMatchConditionPermanent): SelectPermanentEffect
//   .SetUp(selectPlayer: owner, canTargetCondition = IsPermanentExistsOnOwnerBattleAreaDigimon,
//   maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false, mode: Custom,
//   selectPermanentCoroutine: SelectPermanentCoroutine). SelectPermanentCoroutine(permanent):
//   CardEffectCommons.GainPierce(targetPermanent: permanent, EffectDuration.UntilEachTurnEnd, activateClass).
// Headless mirror: uniform ActivatedEffect (= AS-IS ActivateClass) with SelectBody(Mode.Custom) + the AS-IS
//   SelectPermanentCoroutine per-selected-permanent follow-up wired via SelectBody.onEachSelected -> GainPierce on
//   the picked id (UntilEachTurnEnd), scoped to exactly the chosen Digimon.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_091 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

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
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[Main] 1 of your Digimon gains <Piercing> (When this Digimon attacks, if it deletes your opponent's Digimon, it stays unsuspended) for the turn.",
                    onEachSelected: id => CardEffectCommons.GainPierce(
                        new Permanent(card.Context, id, card.Owner), EffectDuration.UntilEachTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] 1 of your Digimon gains <Piercing> for the turn."));
        }

        return cardEffects;
    }
}
