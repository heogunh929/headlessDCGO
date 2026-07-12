// 1:1 mirror of the original ST16_14 (ST16/Purple) OnDiscardHand block — the F1-Tier1 OnDiscardHand witness.
//
// Ported effect (AS-IS ST16_14.cs, timing OnDiscardHand):
//   * [All Turns] "When one of YOUR effects trashes a card in YOUR hand, by suspending this Tamer, gain 1 memory."
//     -> uniform ActivatedEffect (uncapped = AS-IS SetUpActivateClass useCount -1, isOptional TRUE = "by suspending"
//     is a you-may cost). CanUse = IsExistOnBattleArea + CanTriggerOnTrashHand with BOTH halves of the AS-IS gate:
//       - cardEffectSourceCondition (AS-IS SkillCondition): the CAUSING effect's EffectSourceCard.Owner == card.Owner
//         (self effect). This is the AS-IS `CardEffect != null` + owner check — exercises the F1-Tier1 cause-effect
//         threading (event.discardCauseEffectId), so a discard by the OPPONENT'S effect does NOT fire.
//       - cardCondition: the discarded card's Owner == card.Owner (a card in YOUR hand).
//     CanActivate = IsExistOnBattleArea + CanActivateSuspendCostEffect (the suspend cost is payable). Body =
//     SuspendSelfAndGainMemoryBody(1) — AS-IS ActivateCoroutine suspends this Tamer then gains 1 memory.
//     OnDiscardHand is an F1-Tier1 EventBroadcast bridge timing: headless derives it from the discarded hand card's
//     CardMoved (Hand->Trash, TriggerTimingMap), threads that as the driving event (subject = discarded card, plus
//     the cause effect id), and the gate any-matches the discarded card and checks the cause.
//
// The AS-IS OnStartTurn (SetMemoryTo3TamerEffect) and SecuritySkill (PlaySelfTamerSecurityEffect) blocks are
// intentionally OMITTED (this witness exercises only the OnDiscardHand bridge — same scoping as the BT24_018
// OnLoseSecurity witness, which omitted the WhenRemoveField block).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST16.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class ST16_14 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region All Turns (timing OnDiscardHand)
        if (timing == EffectTiming.OnDiscardHand)
        {
            const string description =
                "[All Turns] When one of your effects trashes a card in your hand, by suspending this Tamer, gain 1 memory.";

            // AS-IS CanUseCondition: IsExistOnBattleArea && CanTriggerOnTrashHand(SkillCondition, cardCondition).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnTrashHand(
                    ctx, card,
                    cardEffectSourceCondition: cause => cause.Owner == card.Owner,
                    cardCondition: discarded => discarded.Owner == card.Owner);

            // AS-IS CanActivateCondition: IsExistOnBattleArea && CanActivateSuspendCostEffect (suspend cost payable).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanActivateSuspendCostEffect(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDiscardHand,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SuspendSelfAndGainMemoryBody(1),
                maxCountPerTurn: null,   // AS-IS useCount -1 (uncapped; the suspend cost is the natural limiter).
                isOptional: true,        // AS-IS isOptional true ("by suspending …" — a you-may cost).
                description: description));
        }
        #endregion

        return cardEffects;
    }
}
