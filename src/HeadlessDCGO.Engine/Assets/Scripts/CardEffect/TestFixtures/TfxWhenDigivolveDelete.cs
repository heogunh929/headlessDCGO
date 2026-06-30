// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [When Digivolving] mirrors
// EX8_074's "When Digivolving" region: ① suspend 1 Digimon (any owner, optional), then ② delete 1 of your
// opponent's Digimon whose DP <= 8000 + 3000 * (other suspended Digimon). The dynamic threshold is a closure
// in the delete target predicate — evaluated at the delete step, AFTER the suspend has applied (the mutation
// sink upserts immediately), so the count reflects the just-suspended Digimon. Built from the existing
// ActivatedSelectEffect (Mode.Tap / Mode.Destroy) — no new effect type. Used by tests/G9-009. Inert in
// actual play (no real card numbered "TfxWhenDigivolveDelete").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class TfxWhenDigivolveDelete : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            // ① You may suspend 1 Digimon (any owner).
            bool CanSuspend(HeadlessEntityId id) => CardEffectCommons.IsBattleAreaDigimon(card, id);
            cardEffects.Add(new ActivatedSelectEffect(
                card, CanSuspend, maxCount: 1, canNoSelect: true, canEndNotMax: false,
                SelectPermanentEffect.Mode.Tap, "Suspend 1 Digimon."));

            // ② You may delete 1 of your opponent's Digimon with DP <= the dynamic threshold.
            int DeletionMaxDp() => CardEffectCommons.MaxDpDeleteThreshold(card,
                8000 + 3000 * CardEffectCommons.MatchConditionPermanentCount(card,
                    id => CardEffectCommons.IsBattleAreaDigimon(card, id)
                        && CardEffectCommons.IsSuspended(card, id)
                        && id != card.InstanceId));
            bool CanDelete(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.CurrentDp(card, id) <= DeletionMaxDp();
            cardEffects.Add(new ActivatedSelectEffect(
                card, CanDelete, maxCount: 1, canNoSelect: true, canEndNotMax: false,
                SelectPermanentEffect.Mode.Destroy, "Delete 1 of your opponent's Digimon (DP threshold scales with suspended Digimon)."));
        }

        // (EX8-2 brick) An entry point that re-activates the above [When Digivolving] effects, exercising
        // ReuseWhenDigivolvingEffect. (Placed under OptionSkill only so a test can drive it via
        // ActivatedEffectResolver; the real EX8_074 offers this through a once-per-turn on-play trigger.)
        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new ReuseWhenDigivolvingEffect("Activate this Digimon's [When Digivolving] effects."));
        }

        return cardEffects;
    }
}
