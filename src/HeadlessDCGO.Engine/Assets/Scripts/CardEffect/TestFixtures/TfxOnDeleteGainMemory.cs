// TEST FIXTURE (not a real card). "When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory"
// with NO once-per-turn cap — so it fires once PER deletion. Used by tests/RD11-PerEventFire to prove a
// driving-event trigger fires per event in a pass (AS-IS stacks a SkillInfo per event). Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnDeleteGainMemory : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool Condition() => CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);

            bool TriggerGate(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerOnPermanentDeleted(card, ctx, id => CardEffectCommons.IsOpponentOwnedDigimon(card, id))
                && CardEffectCommons.IsDPZeroDelete(card, ctx);

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 1,
                isInheritedEffect: true,
                card: card,
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
