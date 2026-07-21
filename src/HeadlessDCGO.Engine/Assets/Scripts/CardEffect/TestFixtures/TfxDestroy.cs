// TEST FIXTURE (not a real card). [Main] (OptionSkill) computes the list of the opponent's battle-area
// Digimon and DIRECTLY deletes them — mirrors the original DestroyPermanentsClass (a pre-computed target
// list). Used by tests/BT-PRE-A3 (G9-017). Inert in actual play (no real card numbered "TfxDestroy").
//
// (이연③-b RE-TARGET) re-pointed old-model `DestroyPermanentsEffect` (retired) → the AS-IS DeleteKind sink
// path driven inline through a new-model `ActivateClass`: a fresh MatchStateMutationSink (same ctor as
// SelectPermanentEffect.NewSink / ActivatedEffectResolver's sink), one `CardEffectCommons.DestroyPermanent`
// (AS-IS DestroyPermanentsClass(target).Destroy() sink helper) per pre-computed opponent target, then
// FlushAsync. The sink's centralised immunity / deletion-prevention gate filters (cannotBeDeleted). Same
// behaviour as before, resolved via ActivatedEffectResolver's ActivateICardEffect case (no bespoke switch).

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

public sealed class TfxDestroy : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill && card.Context.ZoneMover is IZoneStateReader zones)
        {
            var targets = new List<HeadlessEntityId>();
            foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
            {
                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
                {
                    if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                    {
                        targets.Add(id);
                    }
                }
            }

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete all of your opponent's Digimon.", CanUseConditionTrue, card);
            activateClass.SetUpActivateClass(null, ActivateDestroyCoroutine, -1, false, "Delete all of your opponent's Digimon.");
            cardEffects.Add(activateClass);

            bool CanUseConditionTrue(Hashtable hashtable) => true;

            async Task ActivateDestroyCoroutine(Hashtable _hashtable)
            {
                EngineContext context = card.Context;
                // AS-IS "one DestroyPermanentsClass call == one delete batch" (SelectPermanentEffect Mode.Destroy
                // NewSink idiom): stage a DeleteKind mutation per target, then flush. The sink's centralised
                // immunity / deletion-prevention gate filters (source = this card, cannotBeDeleted honoured).
                var sink = new MatchStateMutationSink(
                    context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
                    context.EffectRegistry, context.GameEventQueue, context: context);
                foreach (HeadlessEntityId target in targets)
                {
                    if (!target.IsEmpty)
                    {
                        CardEffectCommons.DestroyPermanent(sink, card, target);
                    }
                }

                await sink.FlushAsync();
            }
        }

        return cardEffects;
    }
}
