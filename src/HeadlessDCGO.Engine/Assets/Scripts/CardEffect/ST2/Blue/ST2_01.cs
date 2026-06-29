// 1:1 mirror of the original ST2_01 (ST2/Blue).
//   [Inherited][Your Turn] While your opponent's battling Digimon has no digivolution cards, this Digimon
//   gets +1000 DP.  -> ChangeSelfDPStaticEffect (inherited, conditional)
// Headless relaxation: the original keys off the SPECIFIC battling enemy permanent; headless evaluates
// "owner turn AND the opponent has a battle-area Digimon with no digivolution cards" at read time
// (the continuous gate has no battle-pairing context). Same observable result in the common board state.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_01 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => CardEffectCommons.HasNoDigivolutionCards(card, id));
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 1000, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
