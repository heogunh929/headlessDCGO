// TEST FIXTURE (not a real card). [Main] (OptionSkill) plays the controller's top library card onto the
// battle area at no cost — mirrors the original PlayCardClass simple case (payCost:false, root:Library).
// Used by tests/BT-PRE-A5 (G9-019). Inert in actual play (no real card numbered "TfxPlayCard").
//
// (이연③-b RE-TARGET) re-pointed old-model `PlayCardEffect` (retired) → the AS-IS PlayCardKind sink path
// driven inline through a new-model `ActivateClass`: a fresh MatchStateMutationSink, one PlayCardKind
// mutation (its handler routes through PlayCardClass.PlayCard(), MatchStateMutationSink.ApplyPlayCard),
// then FlushAsync. Same cost-free play as before, resolved via ActivatedEffectResolver's ActivateICardEffect
// case (no bespoke resolver switch).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxPlayCard : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill && card.Context.ZoneMover is IZoneStateReader zones)
        {
            IReadOnlyList<HeadlessEntityId> library = zones.GetCards(card.Owner, ChoiceZone.Library);
            if (library.Count > 0)
            {
                HeadlessEntityId targetCardId = library[0];

                ActivateClass activateClass = new ActivateClass();
                activateClass.SetUpICardEffect("Play the top library card cost-free.", CanUseConditionTrue, card);
                activateClass.SetUpActivateClass(null, ActivatePlayCoroutine, -1, false, "Play the top library card cost-free.");
                cardEffects.Add(activateClass);

                bool CanUseConditionTrue(Hashtable hashtable) => true;

                async Task ActivatePlayCoroutine(Hashtable _hashtable)
                {
                    if (targetCardId.IsEmpty)
                    {
                        return;
                    }

                    EngineContext context = card.Context;
                    // (RDW re-migration off the retired MatchStateMutationSink) AS-IS cost-free play of the pre-
                    // selected top-library card via the direct `new PlayCardClass(...).PlayCard()` mirror (the same
                    // call the sink's PlayCardKind handler wrapped): payCost:false, root:Library, cause = the driving
                    // ActivateClass threaded through CardEffectHashtable, activateETB:true (ETB effects fire on play).
                    await new PlayCardClass(
                        cardSources: new List<CardSource> { new CardSource(context, targetCardId, card.Owner, card.Owner) },
                        hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                        payCost: false,
                        targetPermanent: null,
                        isTapped: false,
                        root: SelectCardEffect.Root.Library,
                        activateETB: true).PlayCard();
                }
            }
        }

        return cardEffects;
    }
}
