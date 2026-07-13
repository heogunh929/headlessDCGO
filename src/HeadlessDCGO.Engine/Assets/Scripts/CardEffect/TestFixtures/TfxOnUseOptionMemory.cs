// TEST FIXTURE (not a real card). "[Your Turn] when an Option is used, gain 2 memory" — a TRIGGERED
// ACTIVATED effect at OnUseOption. Exercises the G2 dispatch: OnUseOption is emitted at option use
// (OptionActivateAction) and must now be broadcast by GameFlowProcessor to reacting field cards. Inert in
// actual play beyond this.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOnUseOptionMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnUseOption)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnUseOption,
                canUse: null,
                canActivate: null,
                body: new MemoryBody(2),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Your Turn] When an Option is used, gain 2 memory."));
        }

        return effects;
    }
}
