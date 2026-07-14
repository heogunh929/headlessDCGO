// TEST FIXTURE (not a real card). A SELF-scope [Your Turn] OnMove reactor: when THIS Digimon itself is promoted
// from the breeding area to the battle area, draw 1 card. This is the SELF witness for the F1 Tier1 OnMove bridge
// — the majority shape (18 of 30 real OnMove cards gate `permanent == card.PermanentOfThisCard()`, e.g.
// EX11_007/ST22_03/ST24_04), which all real self cards wrap around primitive-heavy bodies (reveal-3 / select-grant /
// destroy). This fixture isolates the self gate + a single draw so the test observes exactly the subject-as-
// reactor path of the EventBroadcast bridge (the field scan visits the promoted subject itself, whose own gate
// `permanent == self` passes) without a compound body. Inert in actual play.
//
// Mirrors the AS-IS self shape verbatim (EX11_007.cs:104-113 / ST22_03.cs:63-73):
//   CanUse = IsExistOnBattleAreaDigimon(card) && CanTriggerOnMove(permanent => permanent == card.PermanentOfThisCard()).
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): the `DrawBody(1)` body becomes the
// AS-IS `new DrawClass(...).Draw()` coroutine idiom (BT1_046).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnMoveSelfDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnMove)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[Your Turn] When this Digimon moves from the breeding area to the battle area, draw 1 (uncapped).";

            // AS-IS self gate: the moved permanent IS this card's own permanent (permanent == PermanentOfThisCard()).
            bool PermanentCondition(Permanent permanent) => permanent.InstanceId == card.InstanceId;

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && CardEffectCommons.CanTriggerOnMove(hashtable, PermanentCondition);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }

        return effects;
    }
}
