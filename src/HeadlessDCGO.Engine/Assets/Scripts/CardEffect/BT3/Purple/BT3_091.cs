// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_091.cs
// 1:1 mirror of the original BT3_091 (BT3/Purple), partial:
//   [When Digivolving] If you have 10 or more cards in your trash, you may return up to 2 purple Option
//   cards from your trash to your hand.
//   -> CardEffectFactory.SelectAndAddToHandFromZoneEffect(card, ChoiceZone.Trash, canTarget, maxCount:2,
//      canEndNotMax:true, description), registered only when the extra "trash >= 10" gate (which is NOT
//      implied by the candidate-pool fold — the pool could be < 10 while still containing 2 purple
//      Options) passes, mirroring AS-IS CanActivateCondition = IsExistOnBattleArea(card) &&
//      HasMatchConditionOwnersCardInTrash(card, cond) && TrashCards.Count >= 10.
//
//   [Your Turn][Once Per Turn] When you use an Option card, gain 2 memory.
//   STOP: AS-IS ActivateClass registers on EffectTiming.OnUseOption (CanTriggerWhenOwnerUseOption). The
//   headless EffectTiming enum has NO OnUseOption member — TriggerTimings.OnUseOption exists as a raw
//   engine trigger string (emitted by OptionActivateAction) and CardEffectCommons.CanTriggerWhenOwnerUseOption
//   exists as a predicate helper, but no ActivatedEffectResolver.ResolveAsync call site (PlayCardAction /
//   DigivolveAction / OptionActivateAction / the bridge in ActivatedEffectResolver) ever passes an
//   EffectTiming.OnUseOption value — there is no dispatch surface to register this timing block against.
//   Not registered (primitive/timing gap, no engine mod).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_091 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectCard(CardSource cs) => cs.IsOption && cs.HasCardColor("Purple");
            bool CanSelectCardCondition(HeadlessEntityId id) =>
                CanSelectCard(new CardSource(card.Context, id, card.Owner, card.Owner));

            if (CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCard)
                && ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash).Count >= 10)
            {
                cardEffects.Add(CardEffectFactory.SelectAndAddToHandFromZoneEffect(
                    card,
                    fromZone: ChoiceZone.Trash,
                    canTarget: CanSelectCardCondition,
                    maxCount: 2,
                    canEndNotMax: true,
                    description: "[When Digivolving] If you have 10 or more cards in your trash, you may return up to 2 purple Option cards from your trash to your hand."));
            }
        }

        // STOP: [Your Turn][Once Per Turn] When you use an Option card, gain 2 memory — see file-header
        // STOP note (no EffectTiming.OnUseOption dispatch surface). Not registered.

        return cardEffects;
    }
}
