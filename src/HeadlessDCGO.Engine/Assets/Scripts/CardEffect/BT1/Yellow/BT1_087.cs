// 1:1 mirror of the original BT1_087 (Yellow Tamer, mixed):
//   [Start of Your Turn] Set your memory to 3 (if 2 or less).      -> SetMemoryTo3TamerEffect
//   [On Play] Look at your security stack, then reveal 1 card in it and add it to your hand. If that card
//             is yellow, <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security
//             stack.) Then shuffle your security stack.
//   [Security] Play this Tamer.                                     -> PlaySelfTamerSecurityEffect
//
// [On Play] AS-IS: ActivateClass on OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea && Owner.SecurityCards.Count >= 1, ORDER=-1, ISOPTIONAL=false.
//   ActivateCoroutine: SelectCardEffect(root:Security, mode:AddHand, maxCount:Min(1,count), canNoSelect:()=>
//   false) with an AfterSelect step: if the selected card is yellow, IRecovery(owner,1).Recovery(); then
//   ContinuousController shuffle + Owner.SecurityCards = RandomUtility.ShuffledDeckCards(SecurityCards).
//   Headless mirror: SecuritySelectToHandColorRecoveryShuffleEffect — mandatory select 1 security card -> hand,
//   color-gated <Recovery +1 (Deck)> keyed off the SPECIFIC selected card, then a deterministic security
//   shuffle (ShuffleSecurity sink mutation / IZoneMover.ShuffleSecurityAsync). The three steps stage on the
//   sink so they flush in AS-IS order (the recovered card is shuffled in). Self-guards on security>=1 (CanActivate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_087 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(new SecuritySelectToHandColorRecoveryShuffleEffect(
                card,
                recoveryColor: "Yellow",
                description: "[On Play] Look at your security stack, then reveal 1 card in it and add it to your hand. If that card is yellow, <Recovery +1 (Deck)>. Then shuffle your security stack."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
