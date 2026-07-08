// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_029.cs
// 1:1 mirror of the original BT3_029 (BT3/Blue) — a Digimon.
//   [Your Turn][Once Per Turn] When you play another Digimon, unsuspend this Digimon. AS-IS: ActivateClass
//   on OnEnterFieldAnyone, CanUseCondition = IsExistOnBattleArea(card) && IsOwnerTurn(card) &&
//   CanTriggerOnPermanentPlay(hashtable, PermanentCondition) where PermanentCondition(permanent) =
//   IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) && permanent != card.PermanentOfThisCard(),
//   CanActivateCondition = IsExistOnBattleArea(card), ORDER=-1 (once-per-turn cap via the "Once Per Turn"
//   text — no SetHashString, but the AS-IS ORDER for this shape mirrors BT1_115's 1), ISOPTIONAL=false,
//   ActivateCoroutine = IUnsuspendPermanents(self).Unsuspend().
// Headless mirror: CardEffectFactory.UnsuspendSelfTriggerEffect (TriggeredUnsuspendSelfEffect) with
// maxCountPerTurn=1 + hash ("Unsuspend_BT3_029") and a triggerGate folding IsExistOnBattleArea + IsOwnerTurn
// + CanTriggerOnPermanentPlay(PermanentCondition) — same fold-in-single-gate shape as BT1_115/BT1_086's
// [Your Turn] branch. PermanentCondition stays Func<Permanent,bool> (CanTriggerOnPermanentPlay's own
// signature), comparing InstanceId rather than AS-IS reference equality (headless Permanent instances are
// re-materialised per query, per the porting cheatsheet's Permanent-shim convention).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_029 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.InstanceId != card.InstanceId;

            cardEffects.Add(CardEffectFactory.UnsuspendSelfTriggerEffect(
                timing: EffectTiming.OnEnterFieldAnyone,
                card: card,
                description: "[Your Turn][Once Per Turn] When you play another Digimon, unsuspend this Digimon.",
                maxCountPerTurn: 1,
                hash: "Unsuspend_BT3_029",
                triggerGate: ctx =>
                    CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerOnPermanentPlay(ctx, card, PermanentCondition)));
        }

        return cardEffects;
    }
}
