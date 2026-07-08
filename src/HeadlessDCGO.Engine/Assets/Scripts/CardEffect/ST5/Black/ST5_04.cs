namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST5;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST5_04 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndTurn)
        {
            bool CanActivate()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    // [End of Opponent's Turn] — gate on opponent turn
                    if (CardEffectCommons.IsOpponentTurn(card))
                    {
                        // Opponent didn't attack with a Digimon this turn
                        // AS-IS: GManager.instance.attackProcess.AttackCount == 0
                        // Headless: no direct primitive for "attack count this turn" — primitive gap
                        // STOP: no factory/commons predicate to read total attack count for the turn
                        return false;
                    }
                }

                return false;
            }

            // STOP: no primitive/commons to query "opponent attack count == 0" for turn-based condition
            // The AS-IS condition relies on GManager.attackProcess.AttackCount which has no headless equivalent.
            // Since the condition cannot be faithfully expressed, this timing is STOPped.
        }

        return cardEffects;
    }
}