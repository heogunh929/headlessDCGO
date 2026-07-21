// TEST FIXTURE. "[Digi-Burst 2] Draw 1" — trash 2 of this card's own digivolution sources, then draw 1.
// (이연③-h) Re-written from the retired invented `CardEffectFactory.DigiBurstEffect` (DigiBurstActivatedEffect)
// to the literal AS-IS inline `new IDigiBurst(permanent, N, activateClass)` idiom the printed-card corpus uses
// (ST4_13.cs): an OptionSkill ActivateClass whose CanUseCondition is `CanDigiBurst()` (the source-cost gate)
// and whose ActivateCoroutine awaits `IDigiBurst.DigiBurst()` (select+OnUseDigiburst+trash) THEN the inner
// `new DrawClass(...).Draw()` body (the BT1_046 draw idiom). Resolved via OptionSkill by the resolver's
// ActivateICardEffect case.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDigiBurst : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "[Digi-Burst 2] Draw 1.");
            effects.Add(activateClass);

            bool CanUseCondition(Hashtable _hashtable) =>
                new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).CanDigiBurst();

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).DigiBurst();
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }
        return effects;
    }
}
