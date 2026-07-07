// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_003.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass): a conditional [When Attacking]
// draw with a once-per-turn cap — a "draw @ trigger" shape that had NO dedicated factory before.
//   [When Attacking][Once Per Turn] If your opponent has a Digimon with no digivolution cards in play,
//   trigger <Draw 1>.
//   -> ActivatedEffect(OnAllyAttack, CanUse=CanTriggerOnAttack [self-scope], CanActivate=opponent no-source
//      Digimon, body=DrawBody(1), maxCountPerTurn=1 [AS-IS order]).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_003 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanActivate() => CardEffectCommons.HasMatchConditionOpponentsPermanent(
                card, id => CardEffectCommons.IsBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id));

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: CanActivate,
                body: new DrawBody(1),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[When Attacking][Once Per Turn] If your opponent has a Digimon with no digivolution cards in play, trigger <Draw 1>."));
        }

        return cardEffects;
    }
}
