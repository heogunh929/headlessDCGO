// TEST FIXTURE. Effect-driven DNA Digivolution: DNA-digivolve INTO a hand card ("INTO") by fusing a battle-area
// permanent ("PERM") with a hand material ("MAT") (AS-IS DNADigivolveWithHandOrTrashCardIntoHandOrTrash).
// (R7 종점) Re-pointed off the retired invented `DnaFromHandOrTrashActivatedEffect` carrier (+ its bespoke
// resolver switch arm) onto the AS-IS inline `new ActivateClass()` idiom — resolved by the resolver's generic
// ActivateICardEffect case.
// (DIGIVOLVE cluster teardown) The ActivateCoroutine now drives the AS-IS helper itself
// (CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash, DNADigivolveEffects.cs:256-452 — the
// EX6_072 idiom: three predicates + payCost/isWithHandCard/isIntoHandCard + the activate class) instead of the
// retired substrate `FusionDigivolveHelpers.FuseAsync`, whose whole mechanism that AS-IS helper supersedes
// (co-eval → materialise the hand material → SelectJogressEffect roots → PlayCardClass.SetJogress → rollback).
// The card-number predicates keep this fixture's INTO / PERM / MAT selection contract.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

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

        // AS-IS predicate trio (EX6_072 idiom): the DNA card to digivolve INTO, the battle-area evo root, and
        // the hand material that is materialised as the second root.
        bool IsIntoCard(CardSource cardSource) => cardSource.CardNumber == "INTO";

        bool IsPermanentRoot(Permanent permanent) => permanent.TopCard != null && permanent.TopCard.CardNumber == "PERM";

        bool IsMaterialCard(CardSource cardSource) => cardSource.CardNumber == "MAT";

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            // (B-3 tuck reset) DNA/Jogress resets every source of the fused stack (CardController.cs:1509-1512) —
            // owned by the jogress arm of PlayCardClass the AS-IS helper drives.
            await CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash(
                IsIntoCard,
                IsPermanentRoot,
                IsMaterialCard,
                true,
                true,
                true,
                activateClass,
                null).ConfigureAwait(false);
        }
    }
}
