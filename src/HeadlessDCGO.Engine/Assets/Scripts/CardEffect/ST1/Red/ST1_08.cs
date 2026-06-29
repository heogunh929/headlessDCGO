// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_08.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 timed-buff wave).
//
// 1:1 mirror of the original ST1_08:
//   [When Digivolving] 1 of your Digimon gets +3000 DP for the turn.  -> SelectAndBuffDpEffect
// The original registers under OnEnterFieldAnyone gated by CanTriggerWhenDigivolving + an inline
// ActivateClass/SelectPermanent; the headless declares it under WhenDigivolving and uses the
// SelectAndBuffDpEffect helper. As a select (choice) effect it is resolved via the activation flow, not
// auto-registered (recipe §5) — same select->timed-DP logic as ST1_13 [Main].

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_08 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: 3000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[When Digivolving] 1 of your Digimon gets +3000 DP for the turn."));
        }

        return cardEffects;
    }
}
