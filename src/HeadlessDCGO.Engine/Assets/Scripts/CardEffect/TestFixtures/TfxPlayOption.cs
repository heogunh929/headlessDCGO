// TEST FIXTURE (not a real card). Its [Main] (OptionSkill) plays an Option card from the owner's HAND as a
// nested effect (AS-IS PlayOptionCards). Used by tests/PRIM-P0 (Build Order 5). Inert in actual play.
// (R7 종점) Re-pointed off the retired invented `PlayOptionCardEffect` carrier (+ its bespoke resolver switch
// arm) onto the AS-IS inline `new ActivateClass()` idiom: the ActivateCoroutine selects the Option(s) then
// drives the LIVE substrate `CardEffectCommons.PlayOptionCards` (the AS-IS-signature W3 bridge that trashes
// each Option, opens the OnUseOption window, and resolves its [Main]) — resolved by the resolver's generic
// ActivateICardEffect case.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

public sealed class TfxPlayOption : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing != EffectTiming.OptionSkill)
        {
            return effects;
        }

        const string description = "Play 1 Option from your hand.";
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect(description, _ => true, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, description);
        effects.Add(activateClass);
        return effects;

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            // (E3-P1-1 / AS-IS CardEffectCommons.PlayOptionCards) the effect-driven option-play candidates are
            // filtered by the AS-IS `!CanNotPlayThisOption` getter (regions ①②③ + the colour requirement)
            // before the select — the same legality gate PlayOptionCards re-applies.
            var zones = (IZoneStateReader)card.Context.ZoneMover;
            var candidates = zones.GetCards(card.Owner, ChoiceZone.Hand)
                .Where(id => !new CardSource(card.Context, id, card.Owner, card.Owner).CanNotPlayThisOption)
                .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.Hand, isSelectable: true, card.Owner))
                .ToList();
            int max = Math.Min(1, candidates.Count);
            var request = EffectChoiceHelpers.CreatePermanentRequest(card.Owner, description, minCount: max, maxCount: max, canSkip: false, candidates);

            ChoiceResult result = await card.Context.ChoiceProvider.ChooseAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (result.IsSkipped)
            {
                return;
            }

            var selected = result.SelectedIds
                .Where(id => !id.IsEmpty)
                .Select(id => new CardSource(card.Context, id, card.Owner))
                .ToList();
            if (selected.Count > 0)
            {
                await CardEffectCommons.PlayOptionCards(selected, activateClass, payCost: false, SelectCardEffect.Root.Hand).ConfigureAwait(false);
            }
        }
    }
}
