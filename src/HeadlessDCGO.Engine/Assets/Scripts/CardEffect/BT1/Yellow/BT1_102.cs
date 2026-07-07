// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_102.cs
// 1:1 mirror of the original BT1_102 (BT1/Yellow) — an Option.
//   [Main] Trigger <Draw 1> (Draw 1 card from your deck) for every 2 security cards you have.
//     -> ActivatedEffect(OptionSkill, CanUse=CanTriggerOptionMainEffect, CanActivate=null [AS-IS
//        CanActivateCondition is literally `null` — no extra gate beyond CanUse],
//        body=DrawBody(SecurityCount(card) / 2) — the count is recomputed fresh each time CardEffects()
//        is invoked at resolution (ActivatedEffectResolver.ResolveCardEffectsAsync calls
//        effect.CardEffects(timing, card) right before resolving), matching the AS-IS
//        ActivateCoroutine reading `card.Owner.SecurityCards.Count / 2` at activation time.
//        maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false [AS-IS ISOPTIONAL=false]).
//   [Security] (use the Main effect) -> AddActivateMainOptionSecurityEffect
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_102 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: null,
                body: new DrawBody(CardEffectCommons.SecurityCount(card) / 2),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Trigger <Draw 1> (Draw 1 card from your deck) for every 2 security cards you have."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Draw cards");
        }

        return cardEffects;
    }
}
