// 1:1 mirror of the original ST2_12 (ST2/Blue) — a Tamer.
//   [Start of Your Turn] If your opponent has a Digimon with no digivolution cards, gain 1 memory.
//     -> AddMemoryTriggerEffect (OnStartTurn, conditional)
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (security-skill flow, Wave 3 — deferred)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_12 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => CardEffectCommons.HasNoDigivolutionCards(card, id));
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnStartTurn,
                amount: 1,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                description: "[Start of Your Turn] If your opponent has a Digimon with no digivolution cards, gain 1 memory."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
