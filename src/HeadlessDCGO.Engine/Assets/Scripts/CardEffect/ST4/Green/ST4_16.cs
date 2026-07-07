// 1:1 mirror of the original ST4_16 (ST4/Green) — an Option.
//   [Main] Return 1 of your opponent's SUSPENDED Digimon to its owner's hand. Trash all of the
//          digivolution cards of that Digimon. -> SelectAndBounceWithSourceDiscardEffect
//          (OptionSkill, Bounce + unconditional AS-IS DiscardEvoRoots, target gated by IsSuspended —
//          DB reference ST2_16, which lacks the suspended-only gate).
//   [Security] (use the Main effect) -> AddActivateMainOptionSecurityEffect
// AS-IS HandBounceClaass.Bounce() (CardController.cs:2622) unconditionally calls
// permanent.DiscardEvoRoots() (Permanent.cs:106) right before the top card leaves the field, for EVERY
// hand-bounce — so the "trash all digivolution cards" clause is the bounce mechanic itself, not a
// separate effect. Resolved imperatively until the interactive activation flow is wired.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_16 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.IsSuspended(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBounceWithSourceDiscardEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                description: "[Main] Return 1 of your opponent's suspended Digimon to its owner's hand. Trash all of the digivolution cards of that Digimon."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Return 1 opponent's suspended Digimon to hand");
        }

        return cardEffects;
    }
}
