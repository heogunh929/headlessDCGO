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
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
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
                    // AS-IS play routes through PlayCardClass.PlayCard() via the PlayCardKind sink handler
                    // (MatchStateMutationSink.ApplyPlayCard). Cost-free play of the pre-selected top-library card.
                    var sink = new MatchStateMutationSink(
                        context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
                        context.EffectRegistry, context.GameEventQueue, context: context);
                    sink.Apply(new EffectMutation(
                        MatchStateMutationSink.PlayCardKind,
                        card.InstanceId,
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            [MatchStateMutationSink.TargetEntityIdKey] = targetCardId.Value,
                            [MatchStateMutationSink.FromZoneKey] = ChoiceZone.Library.ToString(),
                        }));

                    await sink.FlushAsync();
                }
            }
        }

        return cardEffects;
    }
}
