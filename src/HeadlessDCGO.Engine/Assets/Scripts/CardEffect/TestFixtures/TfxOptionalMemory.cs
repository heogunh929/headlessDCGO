// TEST FIXTURE (not a real card). Returns a NON-interactive OPTIONAL uniform activated effect at
// EffectTiming.OnEnterFieldAnyone: "[On Play] you MAY gain 1 memory (once per turn)". Used by
// tests/RD13-OptionalGate to prove the optional yes/no gate (RD-13) and that declining consumes no per-turn
// use (RD-12). Inert in actual play. The activated EffectId is "{instanceId}:ae:OnEnterFieldAnyone:MemoryBody".

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOptionalMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: null,
                canActivate: null,
                body: new MemoryBody(1),
                maxCountPerTurn: 1,
                isOptional: true,
                description: "[On Play] You may gain 1 memory."));
        }

        return effects;
    }
}
