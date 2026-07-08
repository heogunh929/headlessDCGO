// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_104.cs — an Option (two timing blocks).
// 1:1 mirror of the original BT3_104.
//   [Main] (OptionSkill) Up to 2 of your opponent's Digimon can't attack or block until the end of your
//   opponent's next turn. Then, if you have a blue Digimon in play, return 1 of your opponent's suspended
//   Digimon to its owner's hand.
//   AS-IS: ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//   ActivateCoroutine: (guarded by HasMatchConditionPermanent) SelectPermanentEffect(Mode.Custom, maxCount =
//   Min(2,count), canNoSelect:false, canEndSelectCondition disallows 0 picks, canEndNotMax:true) — mandatory
//   pick of 1 or 2 opponent Digimon; per selected permanent, BOTH GainCanNotAttack and GainCanNotBlock
//   (UntilOpponentTurnEnd). THEN (independent, unconditional check): if the owner has a blue Digimon in play
//   AND a legal (suspended opponent Digimon) target exists, SelectPermanentEffect(Mode.Bounce, maxCount=1,
//   canNoSelect:false) — mandatory pick of 1 suspended opponent Digimon, returned to hand.
//   [Security] (SecuritySkill) same two-part shape, but the restrict-select only applies GainCanNotAttack
//   (NOT GainCanNotBlock) for UntilEachTurnEnd (the Security text omits "or block" and "for the turn" instead
//   of "until end of opponent's next turn").
// Headless mirror: CardEffectFactory.SelectAndRestrictEffect (ActivatedTargetRestrictionEffect — the exact
// BT1_113 shape, canNoSelect:false hardcoded, canEndNotMax auto-true for maxCount>1) for the restrict-select,
// followed by CardEffectFactory.SelectAndBounceEffect for the conditional blue-Digimon bounce follow-up (its
// own "select up to maxCount matching permanents" no-op-when-nothing-matches behaviour subsumes the AS-IS
// HasMatchConditionPermanent(CanSelectPermanentCondition1) gate, BT1_092/BT1_108 convention) — two independent
// registrations in AS-IS order, gated on the blue-Digimon condition exactly as AS-IS.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_104 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        bool HasBlueDigimonInPlay() =>
            CardEffectCommons.HasMatchConditionOwnersPermanent(card, p => p.IsDigimon && p.TopCard.HasCardColor("Blue"));

        bool CanSelectRestrictTarget(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

        bool CanSelectBounceTarget(HeadlessEntityId id) =>
            CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.IsSuspended(card, id);

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndRestrictEffect(
                card: card,
                canTarget: CanSelectRestrictTarget,
                maxCount: 2,
                duration: EffectDuration.UntilOpponentTurnEnd,
                cannotAttack: true,
                cannotBlock: true,
                description: "[Main] Up to 2 of your opponent's Digimon can't attack or block until the end of your opponent's next turn."));

            if (HasBlueDigimonInPlay())
            {
                cardEffects.Add(CardEffectFactory.SelectAndBounceEffect(
                    card: card,
                    canTarget: CanSelectBounceTarget,
                    maxCount: 1,
                    description: "Then, if you have a blue Digimon in play, return 1 of your opponent's suspended Digimon to its owner's hand."));
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndRestrictEffect(
                card: card,
                canTarget: CanSelectRestrictTarget,
                maxCount: 2,
                duration: EffectDuration.UntilEachTurnEnd,
                cannotAttack: true,
                cannotBlock: false,
                description: "[Security] Up to 2 of your opponent's Digimon can't attack for the turn."));

            if (HasBlueDigimonInPlay())
            {
                cardEffects.Add(CardEffectFactory.SelectAndBounceEffect(
                    card: card,
                    canTarget: CanSelectBounceTarget,
                    maxCount: 1,
                    description: "Then, if you have a blue Digimon in play, return 1 of your opponent's suspended Digimon to its owner's hand."));
            }
        }

        return cardEffects;
    }
}
