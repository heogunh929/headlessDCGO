// 1:1 mirror of the original ST2_08 (ST2/Blue).
//   [Inherited][Your Turn] While your opponent has a Digimon with no digivolution cards, this Digimon gets
//   +1 to its security-stack check (Security Attack +1).  -> ChangeSelfSAttackStaticEffect (inherited, conditional)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_08 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOpponentsPermanent(card, permanent => permanent.IsDigimon && CardEffectCommons.HasNoDigivolutionCards(card, permanent.InstanceId));
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
