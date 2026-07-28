// TEST FIXTURE. "[Digi-Burst 2] This gains Piercing" — the Digi-Burst body is a CONTINUOUS keyword grant (not
// an activated draw/delete): after the cost is paid it is REGISTERED into the permanent's AS-IS duration bucket
// at the keyword's live-read timing (Pierce reads OnDetermineDoSecurityCheck, NewModelContinuousScan.HasPierce),
// exactly the R6-Da'-4 GrantTiming bucket path the resolver used.
// (이연③-h) Re-written from the retired invented `CardEffectFactory.DigiBurstEffect` to the AS-IS inline
// `new IDigiBurst(permanent, N, activateClass)` idiom (ST4_13.cs): CanUseCondition = `CanDigiBurst()`;
// ActivateCoroutine awaits `IDigiBurst.DigiBurst()` (select+OnUseDigiburst+trash) THEN registers the continuous
// Pierce grant via `CardEffectCommons.AddEffectToPermanent(...)` at OnDetermineDoSecurityCheck.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDigiBurstKeyword : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("This gains Piercing", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "[Digi-Burst 2] This gains Piercing.");
            effects.Add(activateClass);

            bool CanUseCondition(Hashtable _hashtable) =>
                new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).CanDigiBurst();

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).DigiBurst();

                // (R6-Da'-4 / RD-P6B-6) the body is a CONTINUOUS keyword-static grant — register it into the
                // permanent's AS-IS duration bucket at the keyword's live-read timing (Pierce reads
                // OnDetermineDoSecurityCheck), exactly the GainPierce idiom, rather than running an activated body.
                CardEffectCommons.AddEffectToPermanent(
                    targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                    effectDuration: EffectDuration.UntilOwnerTurnEnd,
                    card: card,
                    cardEffect: CardEffectFactory.PierceSelfEffect(false, card, null),
                    timing: EffectTiming.OnDetermineDoSecurityCheck);
            }
        }
        return effects;
    }
}
