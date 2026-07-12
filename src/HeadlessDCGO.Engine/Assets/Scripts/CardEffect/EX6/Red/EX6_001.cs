// 1:1 mirror of the original EX6_001 (EX6/Red) — an F1-Tier2 OnAddDigivolutionCards INHERITED (digivolution-source)
// witness (the card's ONLY effect).
//
// Ported effect (AS-IS EX6_001.cs:14-58, timing OnAddDigivolutionCards):
//   * [Your Turn][Once Per Turn] "When an effect places a card with the [Legend-Arms] trait in this Digimon's
//     digivolution cards, gain 1 memory." — AS-IS `new ActivateClass()` with SetIsInheritedEffect(true) +
//     SetHashString("Gain1Memory_EX6_001") + SetUpActivateClass(..., 1, false, ...) = maxActivationCount 1 (ONCE PER
//     TURN), isOptional FALSE. INHERITED: exposed only while THIS card is a NON-TOP digivolution source; ported as a
//     uniform ActivatedEffect with isInheritedEffect:true — ScanZones collects it from under the receiving host. The
//     OnAddDigivolutionCards analogue of BT22_003 (WhenLinked inherited).
//     CanUse (AS-IS :30-48) = IsExistOnBattleArea(card) && IsOwnerTurn(card) && CanTriggerOnAddDigivolutionCard(
//       permanent == card.PermanentOfThisCard(), cardEffectCondition:EffectSourceCard != null,
//       cardCondition:ContainsTraits("Legend-Arms")). The AS-IS `EffectSourceCard != null` is subsumed by the headless
//       gate's mandatory causeSourceId (a non-empty cause is required), so cardEffectSourceCondition is null.
//     CanActivate (AS-IS :50-53) = IsExistOnBattleArea(card) (NOT ...Digimon).
//     Body (AS-IS :55-58) = AddMemory(1).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX6.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class EX6_001 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [Your Turn][Once Per Turn] gain 1 memory when a [Legend-Arms] card is placed under by an effect (OnAddDigivolutionCards, INHERITED)
        if (timing == EffectTiming.OnAddDigivolutionCards)
        {
            const string desc =
                "[Your Turn] [Once Per Turn] When an effect places a card with the [Legend-Arms] trait in this Digimon's digivolution cards, gain 1 memory.";

            // AS-IS permanentCondition (:38): permanent == card.PermanentOfThisCard(). INHERITED source → compare to
            // the HOST id via PermanentOfThisCard().TopInstanceId (BT22_003 pattern).
            bool IsThisPermanent(Permanent permanent) =>
                permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAddDigivolutionCards,
                canUse: ctx => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerOnAddDigivolutionCard(
                        ctx, card, IsThisPermanent, null, cs => cs.ContainsTraits("Legend-Arms")),
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new MemoryBody(1),
                maxCountPerTurn: 1,       // AS-IS ORDER=1 — [Once Per Turn]
                isOptional: false,
                description: desc,
                capHash: "Gain1Memory_EX6_001", // AS-IS SetHashString("Gain1Memory_EX6_001")
                isInheritedEffect: true));       // AS-IS SetIsInheritedEffect(true)
        }
        #endregion

        return cardEffects;
    }
}
