// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_111.cs — a Digimon (three timing blocks).
// AS-IS:
//   (timing None) "While this card is in your hand, the digivolution cost of your Paildramon or Dinobeemon
//   is reduced by 2." ActivateClass-free: CardEffectFactory.ChangeDigivolutionCostStaticEffect(changeValue:-2,
//   permanentCondition: targetPermanent is an owner battle-area permanent whose TopCard.CardNames contains
//   "Paildramon" or "Dinobeemon", cardCondition: cardSource==this card && this card is in the owner's hand,
//   rootCondition: root==Hand, condition: this card is in the owner's hand, setFixedCost:false).
//
// STOP (timing None — genuine structural primitive gap, not a per-card shortcut; grepped 2x+ per rule 4, same
// finding the BT1_109 sibling documents and this porting unit re-confirmed): this needs a digivolution-cost
// reduction gated on the DIGIVOLVING-FROM permanent's identity (an existing battle-area Paildramon/Dinobeemon
// permanent, digivolving further using THIS card as material from hand) — but the headless
// ChangeDigivolutionCostStaticEffect overload that actually exists (CardPortingFramework.cs ~4653) is
// SELF-scoped only (`int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition` — no
// permanentCondition/cardCondition/rootCondition parameters at all), and the underlying resolution pipeline
// (ContinuousModifierGate.ResolveDigivolutionCost(context, cardId, baseCost) — Headless/Runtime/
// ContinuousModifierGate.cs:48, called from DigivolveAction.cs:549 with `cardId` = the NEW post-digivolve
// card only) has no parameter through which a registered cost modifier's predicate could ever see the
// FROM-permanent's card name. No composed primitive exists for "reduce the digivolution cost of a
// name-matched existing permanent when THIS specific hand card is the digivolution material". Per rule 4
// this is a primitive gap requiring new engine-layer work (threading the FROM-permanent id through the
// digivolution-cost query), out of scope for a single-card porting pass. No cardEffects registered for
// timing None (OnDetermineDoSecurityCheck and OnEndBattle are unaffected and fully ported below). — 강모델
// if (timing == EffectTiming.None) { ... }
//
//   <Security Attack +1>. AS-IS: CardEffectFactory.PierceSelfEffect on OnDetermineDoSecurityCheck — verbatim
//   factory match.
//
//   [Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives,
//   unsuspend this Digimon.
//   AS-IS: ActivateClass on EffectTiming.OnEndBattle, CanUseCondition = IsExistOnBattleArea(card) &&
//   IsOwnerTurn(card) && CanTriggerWhenDeleteOpponentDigimonByBattle(winnerCondition: permanent.cardSources.
//   Contains(card), loserCondition: IsOpponentPermanent, isOnlyWinnerSurvive:true). CanActivateCondition =
//   IsExistOnBattleArea(card). ORDER=1 (maxCountPerTurn:1 + SetHashString -> [Once Per Turn]), ISOPTIONAL=false.
//   ActivateCoroutine: unconditional self-unsuspend.
// Headless mirror: CardEffectFactory.UnsuspendSelfTriggerEffect with the same winner/loser trigger gate as
// the BT3_050/ST4_11/BT1_112 siblings — the exact "delete-and-survive, self-scoped, once per turn" shape.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT3_111 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEndBattle)
        {
            bool WinnerCondition(Permanent permanent) =>
                card.PermanentOfThisCard() is { TopInstanceId: var top } && !top.IsEmpty && permanent.InstanceId == top;

            bool LoserCondition(Permanent permanent) => CardEffectCommons.IsOpponentPermanent(permanent, card);

            bool TriggerGate(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(
                        ctx, card, WinnerCondition, LoserCondition, isOnlyWinnerSurvive: true);

            cardEffects.Add(CardEffectFactory.UnsuspendSelfTriggerEffect(
                timing: EffectTiming.OnEndBattle,
                card: card,
                description: "[Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives, unsuspend this Digimon.",
                maxCountPerTurn: 1,
                hash: "Unsuspend_BT3_111",
                triggerGate: TriggerGate));
        }

        return cardEffects;
    }
}
