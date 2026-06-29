// 1:1 mirror of the original ST3_01 (ST3/Yellow).
//   [Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, this Digimon
//   gets +1000 DP for the turn.  -> SelfDpBuffTriggerEffect (OnDestroyedAnyone, +1000, UntilEachTurnEnd)
// The original's [Once Per Turn] and 0-DP-delete-of-opponent gates are trigger-emission details (relaxed
// here, like ST2_11); the headless condition keeps the [Your Turn] gate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class ST3_01 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);
            }

            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                changeValue: 1000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: Condition,
                description: "[Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, this Digimon gets +1000 DP for the turn."));
        }

        return cardEffects;
    }
}
