// TEST FIXTURE (not a real card). "When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory"
// with NO once-per-turn cap — so it fires once PER deletion. Used by tests/RD11-PerEventFire to prove a
// driving-event trigger fires per event in a pass (AS-IS stacks a SkillInfo per event). Inert in actual play.
//
// R3-C2b-2: the production old-model AddMemoryTriggerEffect was deleted, but this fixture drives the STILL-LIVE
// registry-collection subsystem (AutoProcessingTriggerCollector), so it uses the test-scoped old-model
// TfxTriggeredMemoryEffect (registers a ToBinding registry binding) — the same registry path it used before.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnDeleteGainMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool Condition() => CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);

            bool TriggerGate(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerOnPermanentDeleted(card, ctx, id => CardEffectCommons.IsOpponentOwnedDigimon(card, id))
                && CardEffectCommons.IsDPZeroDelete(card, ctx);

            cardEffects.Add(new TfxTriggeredMemoryEffect(
                card: card,
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 1,
                isInheritedEffect: true,
                condition: Condition,
                description: "When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory (uncapped).",
                triggerGate: TriggerGate,
                maxCountPerTurn: null,   // uncapped -> fires once per deletion event
                hash: "Memory+1_TfxOnDeleteGainMemory",
                isOptional: false));
        }

        return cardEffects;
    }
}
