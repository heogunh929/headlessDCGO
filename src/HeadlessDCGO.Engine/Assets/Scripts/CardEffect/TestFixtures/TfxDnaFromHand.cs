// TEST FIXTURE. Effect-driven DNA Digivolution: DNA-digivolve INTO a hand card ("INTO") by fusing a battle-area
// permanent ("PERM") with a hand material ("MAT") (AS-IS DNADigivolveWithHandOrTrashCardIntoHandOrTrash).
// (R7 종점) Re-pointed off the retired invented `DnaFromHandOrTrashActivatedEffect` carrier (+ its bespoke
// resolver switch arm) onto the AS-IS inline `new ActivateClass()` idiom: the ActivateCoroutine auto-matches an
// into-card/permanent/material and drives the SAME live `FusionDigivolveHelpers.FuseAsync` the resolver arm
// composed — resolved by the resolver's generic ActivateICardEffect case.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxDnaFromHand : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing != EffectTiming.OptionSkill)
        {
            return effects;
        }

        const string description = "DNA Digivolve using a hand/trash card";
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect(description, _ => true, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, description);
        effects.Add(activateClass);
        return effects;

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            EngineContext context = card.Context;
            var reader = (IZoneStateReader)context.ZoneMover;
            HeadlessPlayerId owner = card.Owner;

            HeadlessEntityId? First(IReadOnlyList<HeadlessEntityId> pool, System.Func<CardSource, bool> condition, HeadlessEntityId exclude)
            {
                foreach (HeadlessEntityId id in pool)
                {
                    if (id != exclude && condition(new CardSource(context, id, owner, owner)))
                    {
                        return id;
                    }
                }

                return null;
            }

            HeadlessEntityId? into = First(reader.GetCards(owner, ChoiceZone.Hand), cs => cs.CardNumber == "INTO", exclude: default);
            HeadlessEntityId? permanent = First(reader.GetCards(owner, ChoiceZone.BattleArea), cs => cs.CardNumber == "PERM", exclude: default);
            HeadlessEntityId? material = into is HeadlessEntityId intoId
                ? First(reader.GetCards(owner, ChoiceZone.Hand), cs => cs.CardNumber == "MAT", exclude: intoId)
                : null;

            if (into is HeadlessEntityId topId && permanent is HeadlessEntityId permId && material is HeadlessEntityId matId)
            {
                // (B-3 tuck reset) DNA/Jogress resets every source of the fused stack (CardController.cs:1509-1512).
                await FusionDigivolveHelpers.FuseAsync(
                    context.CardInstanceRepository, context.ZoneMover, topId, ChoiceZone.Hand,
                    new[] { permId, matId }, gameEventQueue: context.GameEventQueue,
                    kind: FusionKind.DnaDigivolve, cancellationToken: CancellationToken.None,
                    context: context).ConfigureAwait(false);
            }
        }
    }
}
