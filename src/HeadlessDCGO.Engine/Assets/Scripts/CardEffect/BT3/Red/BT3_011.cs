// 1:1 mirror of the original BT3_011 (BT3/Red).
//   [Security] Play this Digimon.
//   -> PlaySelfDigimonAfterBattleSecurityEffect (verbatim factory match, mirrors ST1_12/ST4_14 kind pattern
//   for Digimon security cards).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_011 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(card: card));
        }

        return cardEffects;
    }
}
