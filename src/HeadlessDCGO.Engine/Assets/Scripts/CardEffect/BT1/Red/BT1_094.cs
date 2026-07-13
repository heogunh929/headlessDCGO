// 1:1 mirror of the original BT1_094 (BT1/Red) — an Option.
//   [Main]     Delete 1 of your opponent's Digimon with <Blocker>.
//   AS-IS: ActivateClass on EffectTiming.OptionSkill, CanUseCondition = CanTriggerOptionMainEffect,
//   CanActivateCondition = HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) && permanent.HasBlocker,
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, ActivateCoroutine = SelectPermanentEffect(Mode.Destroy)
//   with maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false.
//   [Security] AddActivateMainOptionSecurityEffect (reuse the Main effect).
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with explicit CanUse/CanActivate gates
//   (CanTriggerOptionMainEffect / HasMatchConditionPermanent, not folded away) and body=ActivatedSelectEffect
//   (AS-IS SelectPermanentEffect Mode.Destroy) — same shape as BT1_023's [On Play] Blocker-delete, ported to
//   the OptionSkill/SecuritySkill activation flow (same as ST1_15/ST1_16). AS-IS permanent.HasBlocker is
//   mirrored via the self-static keyword gate. [Security] unchanged: AddActivateMainOptionSecurityEffect
//   reuses the [Main] skill.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_094 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            const string description = "[Main] Delete 1 of your opponent's Digimon with <Blocker>.";

            // AS-IS CanSelectPermanentCondition: IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
            // && permanent.HasBlocker.
            bool CanSelect(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                && ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker);

            // AS-IS CanUseCondition: CanTriggerOptionMainEffect(hashtable, card).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card);

            // AS-IS CanActivateCondition: HasMatchConditionPermanent(CanSelect).
            bool CanActivate() => CardEffectCommons.HasMatchConditionPermanent(card, CanSelect);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new ActivatedSelectEffect(
                    card, CanSelect, maxCount: 1, canNoSelect: false, canEndNotMax: false,
                    SelectPermanentEffect.Mode.Destroy, description),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Delete 1 Digimon with Blocker");
        }

        return cardEffects;
    }
}
