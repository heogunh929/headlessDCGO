// TEST FIXTURE (not a real card). Returns a NON-interactive OPTIONAL activated effect at
// EffectTiming.OnEnterFieldAnyone: "[On Play] you MAY gain 1 memory (once per turn)". Used by
// tests/RD13-OptionalGate to prove the optional yes/no gate (RD-13) and that declining consumes no per-turn
// use (RD-12).
// (uniform-사멸 flip) Re-written from the retired invented uniform `ActivatedEffect(isOptional:true)` + `MemoryBody`
// to the literal AS-IS inline `new ActivateClass()` idiom: SetUpActivateClass(isOptional: true) routes through the
// AS-IS OptionalSkill yes/no prompt (Activate_Optional), and the register-before-body OnProcessCallbuck fires only
// AFTER the accept (Activate_Execute) — declining consumes no use. The memory gain is the AS-IS card idiom
// `card.Owner.AddMemory(1, activateClass)` (Player.cs:813 bridge extension). Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOptionalMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may gain 1 memory", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, true, EffectDescription());
            effects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Play] You may gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
