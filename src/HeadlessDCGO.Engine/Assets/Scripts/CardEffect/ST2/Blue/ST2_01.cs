// 1:1 mirror of the original ST2_01 (ST2/Blue).
//   [Inherited][Your Turn] While the Digimon this is battling has no digivolution cards, this Digimon gets
//   +1000 DP.  -> ChangeSelfDPStaticEffect (inherited, conditional)
// Battle-pairing restored (G10-006): the condition keys off the SPECIFIC enemy this card's permanent is
// battling (CurrentBattleOpponent, read from AttackController.Current), exactly as the original
// card.PermanentOfThisCard().battle.enemyPermanent(...).

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
                if (!CardEffectCommons.IsExistOnBattleArea(card) || !CardEffectCommons.IsOwnerTurn(card))
                {
                    return false;
                }

                HeadlessEntityId enemy = CardEffectCommons.CurrentBattleOpponent(card);
                return !enemy.IsEmpty
                    && CardEffectCommons.IsOpponentOwnedDigimon(card, enemy)
                    && CardEffectCommons.HasNoDigivolutionCards(card, enemy);
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 1000, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
