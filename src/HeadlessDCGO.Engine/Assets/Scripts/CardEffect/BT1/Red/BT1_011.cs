// 1:1 mirror of the original BT1_011 (BT1/Red).
//   [On Play] Return 1 Digimon card with [Agumon] in its name from your trash to your hand.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionOwnersCardInTrash(card,
//   CanSelectCardCondition), ORDER=-1, ISOPTIONAL=false, ActivateCoroutine = SelectCardEffect.SetUp
//   (mode: AddHand, root: Trash, maxCount: Min(1, owner's matching trash count), canEndNotMax: false).
//   CanSelectCardCondition = cardSource.IsDigimon && cardSource.ContainsCardName("Agumon").
//   Headless mirror: CardEffectFactory.SelectAndAddToHandFromZoneEffect (AS-IS SelectCardEffect Mode.AddHand /
//   Root.Trash) — the [On Play] play path (PlayCardAction) resolves this card's own OnEnterFieldAnyone
//   effects directly (subject = this card), so CanTriggerOnPlay / IsExistOnBattleArea are structurally
//   satisfied; the trash-match precondition is covered by the primitive's own candidate-count fold
//   (maxCount = Min(1, candidates.Count) inside ActivatedSelectFromZoneEffect.BuildRequest), same pattern as
//   BT1_010's dropped LibraryCards gate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_011 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.ContainsCardName("Agumon");
            }

            cardEffects.Add(CardEffectFactory.SelectAndAddToHandFromZoneEffect(
                card,
                fromZone: ChoiceZone.Trash,
                canTarget: CanSelectCardCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[On Play] Return 1 Digimon card with [Agumon] in its name from your trash to your hand."));
        }

        return cardEffects;
    }
}
