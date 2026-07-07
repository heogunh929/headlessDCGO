// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_101.cs — an Option.
// 1:1 mirror of the AS-IS BT1_101.
//   [Main] (OptionSkill) "Trash all of the digivolution cards under all of your opponent's Digimon."
//     ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false). ActivateCoroutine:
//     a plain `foreach (Permanent p in card.Owner.Enemy.GetBattleAreaDigimons())` — every opponent battle-area
//     Digimon, unconditionally, NO CanSelectPermanentCondition / NO player selection — then per host
//     TrashDigivolutionCardsFromTopOrBottom(trashCount: p.DigivolutionCards.Count, isFromTop: true).
//   [Security] (use the Main effect) -> AddActivateMainOptionSecurityEffect (ReuseMainOptionEffect re-resolves
//     CardEffects(OptionSkill)); now that [Main] is registered it reuses it faithfully.
// Headless mirror: uniform ActivatedEffect whose body is ApplyToAllMatchingBody — the no-select "apply a mutation
//   to EVERY matching permanent" body (AS-IS foreach loop), evaluated live at resolve time. Per opponent
//   battle-area Digimon it stages TrashAllDigivolutionCards (count = int.MaxValue -> the sink clamps to the
//   host's actual source count; fromBottom:false = AS-IS isFromTop:true). The sink's centralised immunity gate
//   filters immune hosts. CanActivate is null (AS-IS CanActivateCondition = null): registers unconditionally,
//   a no-op when the opponent has no Digimon.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_101 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool Match(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: null,
                body: new ApplyToAllMatchingBody(
                    match: Match,
                    perTarget: (c, sink, id) => CardEffectCommons.TrashAllDigivolutionCards(sink, c, id, fromBottom: false)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Trash all of the digivolution cards under all of your opponent's Digimon."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card, cardEffects: ref cardEffects, effectName: "Trash all digivolution cards under all of your opponent's Digimon");
        }

        return cardEffects;
    }
}
