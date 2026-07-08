// Source: Assets/Scripts/CardEffect/BT2/Blue/BT2_070.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass): a conditional
// [On Deletion] draw gated on trash presence and a non-empty deck.
//   [On Deletion] Trigger <Draw 1>. (Draw 1 card from your deck.)
//   -> ActivatedEffect(OnDestroyedAnyone, CanUse=CanTriggerOnDeletion [self-scope],
//      CanActivate=on trash && library >= 1, body=DrawBody(1),
//      maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false [AS-IS ISOPTIONAL=false]).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_070 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool CanActivate()
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDestroyedAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerOnDeletion(ctx, card),
                canActivate: CanActivate,
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[On Deletion] Trigger <Draw 1>. (Draw 1 card from your deck.)"));
        }

        return cardEffects;
    }
}