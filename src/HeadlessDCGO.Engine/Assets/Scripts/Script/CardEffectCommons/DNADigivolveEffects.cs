// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/DNADigivolveEffects.cs
// (EFFECT-MODEL REBUILD / bridge W3) AS-IS-signature `Task` overloads for the two card-called DNA-digivolve
// mutation helpers of this AS-IS file (docs/audit/mutation_helper_bridge_map.md rows):
//   - DNADigivolvePermanentsIntoHandOrTrashCard  (AS-IS :458, 55 card calls — near-1:1 substrate delegation)
//   - DNADigivolveWithHandOrTrashCardIntoHandOrTrash (AS-IS :256, 2 calls — STOP kept, declared at the AS-IS
//     signature so verbatim card code COMPILES and fails loudly at activation, never silently)
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    #region DNA digivolve permanents into a hand/trash card (AS-IS DNADigivolveEffects.cs:458)

    /// <summary>(BRIDGE W3) AS-IS <c>DNADigivolvePermanentsIntoHandOrTrashCard</c> — AS-IS-signature overload;
    /// the substrate overload is already param-for-param (bridge map: "near-perfect 1:1, best delegation
    /// candidate of the whole set"). Delegates translated per the established rules
    /// (<c>Func&lt;CardSource,IEnumerator&gt;</c>→<c>Func&lt;CardSource,Task&gt;</c>,
    /// <c>Func&lt;IEnumerator&gt;</c>→<c>Func&lt;Task&gt;</c>). BEHAVIOUR NUANCE (design item RD-W3-6, from the
    /// substrate's own doc): the substrate discards <paramref name="payCost"/> at runtime (predicate-form DNA
    /// is treated as cost-0; cost is carried by a DNA recipe instead) — the parameter is threaded through to
    /// the substrate unchanged (no wrapper-side drop) so the behaviour is centralised where it is documented.</summary>
    public static async Task DNADigivolvePermanentsIntoHandOrTrashCard(
        Func<CardSource, bool> canSelectDNACardCondition,
        bool payCost,
        bool isHand,
        ICardEffect activateClass,
        Func<Permanent, bool>[] permanentConditions = null,
        Func<CardSource, Task> successProcess = null,
        bool ignoreSelection = false,
        Func<Task> failedProcess = null,
        bool isOptional = true)
    {
        // AS-IS guards — activateClass/EffectSourceCard null → silent no-op.
        if (activateClass?.EffectSourceCard == null)
        {
            return;
        }

        await DNADigivolvePermanentsIntoHandOrTrashCard(
            canSelectDNACardCondition, payCost, isHand, activateClass.EffectSourceCard,
            permanentConditions, successProcess, ignoreSelection, failedProcess, isOptional).ConfigureAwait(false);
    }

    #endregion

    #region DNA digivolve WITH a hand/trash card into a hand/trash card (AS-IS DNADigivolveEffects.cs:256) — STOP

    /// <summary>(BRIDGE W3 · RD-W3 DNA-temp) AS-IS <c>DNADigivolveWithHandOrTrashCardIntoHandOrTrash</c> —
    /// declared at the AS-IS signature so a verbatim ported card COMPILES against it and fails LOUDLY at
    /// activation. The AS-IS body selects a DNA-capable card from hand/trash to be one material, TEMPORARILY
    /// materialises it as a permanent (<c>PlayTempPermanent</c>/<c>CardObjectController.CreateNewPermanent</c>) so
    /// it can be jointly evaluated with a field permanent, resolves ordering, executes the jogress via
    /// <c>PlayCardClass.SetJogress</c>, and UN-plays the temp permanent on failure.
    ///
    /// DESIGN DECISION RESOLVED (RD-S3-BT17_095, proven by tests/DNATEMP-Witness.Tests): the transient-permanent
    /// machinery IS now expressible — the entity-less pure-predicate transient = a read-only <c>Permanent</c> VIEW
    /// over the card's own id + <c>snapshotZone: BattleArea</c> (TopCard = the card; field-membership answered from
    /// the snapshot, no zone mutation/trigger); the REAL material = <c>CardObjectController.CreateNewPermanent</c>
    /// (live); rollback = the public <c>CardObjectController.AddHandCard(card, isDraw)</c> (live); the jogress
    /// itself = <c>CardSource.CanJogressFromTargetPermanent</c> + <c>PlayCardClass.SetJogress</c> (live, to the MIG4
    /// collapse leaf). The prior "no headless substrate surface" rationale is RETRACTED.
    ///
    /// This overload nonetheless stays a STOP: (1) the mirror's LIVE effect-driven DNA-from-hand/trash path is
    /// <c>FusionDigivolveHelpers.FuseAsync</c> (the SpecialPlay architecture the ported <c>TfxDnaFromHand</c> /
    /// tests/PRIM.DnaFromHand use) — a 1:1 PlayTempPermanent port here would be an unverified PARALLEL path; and
    /// (2) the raw helper has NO live ported caller (its AS-IS callers EX11_059/EX6_072 are unported), so per the
    /// completion-before-porting / witness-selection rules its full interactive-flow port is deferred to those
    /// cards' own witness-selected unit. BT17_095's &lt;Delay&gt; (which hand-rolls the same mechanism in-card) is
    /// the resolved witness of the family — see BT17_095.cs.</summary>
    public static Task DNADigivolveWithHandOrTrashCardIntoHandOrTrash(
        Func<CardSource, bool> targetCardCondition,
        Func<Permanent, bool> permanentCondition,
        Func<CardSource, bool> digivolutionCardCondition,
        bool payCost,
        bool isWithHandCard,
        bool isIntoHandCard,
        ICardEffect activateClass,
        Func<Task> successProcess,
        bool ignoreSelection = false,
        Func<Task> failedProcess = null,
        bool isOptional = true) =>
        throw new NotSupportedException(
            "DNA-with-temporary-material: the transient-permanent mechanism IS modeled (RD-S3-BT17_095 resolved — " +
            "SnapshotZone view + CreateNewPermanent + AddHandCard + SetJogress, witnessed by DNATEMP-Witness). This " +
            "raw AS-IS helper stays a STOP: the live effect-driven DNA-from-hand path is FusionDigivolveHelpers." +
            "FuseAsync (TfxDnaFromHand/PRIM.DnaFromHand), and this helper has no live ported caller (EX11_059/" +
            "EX6_072 unported) — its full interactive-flow port is deferred to those cards' witness. See BT17_095.cs.");

    #endregion

    #region Create Jogress Conditions from Permanent Conditions (P6C3, AS-IS DNADigivolveEffects.cs:630-649)

    /// <summary>1:1 mirror of AS-IS <c>GetJogressConditions</c> — two material-slot predicates, verbatim
    /// (including the AS-IS quirk that <c>permanentCondition1</c>/<c>permanentCondition2</c> are used AS-IS,
    /// UNCOMPOSED with the local <c>PermanentCondition</c>/<c>FullPermanentCondition{1,2}</c> helpers below —
    /// those are declared but never referenced by the returned <see cref="JogressCondition"/>, dead code in the
    /// original kept verbatim per the no-simplification rule).</summary>
    public static JogressCondition GetJogressConditions(Func<Permanent, bool> permanentCondition1, string description1, Func<Permanent, bool> permanentCondition2, string description2, CardSource card, int cost = 0)
    {
        JogressConditionElement[] elements =
        {
            new JogressConditionElement(permanentCondition1, description1),
            new JogressConditionElement(permanentCondition2, description2),
        };

        JogressCondition jogressCondition = new(elements, cost);

        return jogressCondition;

        bool PermanentCondition(Permanent permanent)
        {
            return permanent != null
                && permanent.TopCard != null
                && permanent.TopCard.Owner == card.Owner
                && permanent.IsDigimon
                && CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
        }

        bool FullPermanentCondition1(Permanent permanent) => PermanentCondition(permanent) && permanentCondition1 != null && permanentCondition1(permanent);

        bool FullPermanentCondition2(Permanent permanent) => PermanentCondition(permanent) && permanentCondition2 != null && permanentCondition2(permanent);
    }

    #endregion
}
