// 1:1 mirror of BT2_087 — a Tamer.
//   [Start of Your Turn] If owner has ≤3 security cards, gain 1 memory.
//   [Security] Play this Tamer.
// SecurityCount(card) ≤ 3 gate evaluated at OnStartTurn invocation (start-of-turn bridge timing).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_087 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn
            && CardEffectCommons.IsOwnerTurn(card)
            && CardEffectCommons.SecurityCount(card) <= 3)
        {
            cardEffects.Add(CardEffectFactory.GainMemoryActivatedEffect(
                card,
                amount: 1,
                description: "[Start of Your Turn] If you have 3 or fewer security cards, gain 1 memory."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}