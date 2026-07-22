// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/DNADigivolveEffects.cs
// (EFFECT-MODEL REBUILD / bridge W3) AS-IS-signature `Task` overloads for the two card-called DNA-digivolve
// mutation helpers of this AS-IS file (docs/audit/mutation_helper_bridge_map.md rows):
//   - DNADigivolvePermanentsIntoHandOrTrashCard  (AS-IS :458, 55 card calls — near-1:1 substrate delegation)
//   - DNADigivolveWithHandOrTrashCardIntoHandOrTrash (AS-IS :256, 2 calls — STOP kept, declared at the AS-IS
//     signature so verbatim card code COMPILES and fails loudly at activation, never silently)
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Choices;

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

    #region DNA digivolve WITH a hand/trash card into a hand/trash card (AS-IS DNADigivolveEffects.cs:256) — PORTED

    // ══════════════════════════════════════════════════════════════════════════════════════════════════════
    // RD-S3-EX6_072 (temp-material DNA family): the AS-IS-signature helper + its private co-eval dependencies
    // (AS-IS DNADigivolveEffects.cs:20-452), ported 1:1. The ONLY substrate translation is the RD-S3-BT17_095
    // OPTION-2 mapping (proven load-bearing by tests/DNATEMP-Witness): the AS-IS entity-less transient
    //   `new Permanent(new List<CardSource>(){card}){IsSuspended=false}` + `FieldPermanents[frameID]=transient`
    //   (a raw slot write with NO ETB/trigger, nulled back after the check)
    // = a READ-ONLY mirror `Permanent` VIEW over the card's OWN instance id carrying `snapshotZone:BattleArea`
    //   (TopCard = the card → every TopCard-property predicate identical; the field-membership sub-check the
    //    jogress `EvoRootCondition` wrapper (IsPermanentExistsOnOwnerBattleAreaDigimon) gates on answers TRUE
    //    from the snapshot, WITHOUT any zone mutation or trigger — 1:1 with the AS-IS zero-side-effect slot write).
    // The AS-IS `PlayTempPermanent(card, finalCard:false)` (co-eval) → that snapshot view; the AS-IS null-slot
    // restore (`owner.FieldPermanents[…]=null`) → a no-op (no slot was written). The AS-IS materialisation
    // `PlayTempPermanent(card, true)` + `CreateNewPermanent(permanent, frameID)` → the frameless
    // `CardObjectController.CreateNewPermanent(card, isSuspended:false)` (the RD-P6C1-1/-2 no-slot/no-capacity
    // adaptation — the AS-IS empty-frame search + `0<=frameID<fieldCardFrames.Count` guard is the slot-capacity
    // gate the frameless zone-append subsumes; PlayTempPermanent's null return is unreachable here). Rollback
    // `CardObjectController.AddHandCard/AddTrashCard` and the jogress `CanJogressFromTargetPermanents` +
    // `PlayCardClass.SetJogress` are all live. Standard translations: IEnumerator→Task; `.Some/.Filter/.Map`→
    // `.Any/.Where/.Select`; `activateClass.EffectSourceCard.Owner` (HeadlessPlayerId) → `new Player(ctx, owner)`;
    // AS-IS card-less `HasMatchConditionPermanent(cond)` → the mirror `(anchorCard, cond)` overload;
    // `jogressTarget.jogressCondition` → the `JogressConditionOf()` accessor; the AS-IS Func<Permanent,bool>
    // SelectPermanentEffect predicate → the id-shaped W4 adapter. Live witness: EX6_072 (hand→hand DNA
    // digivolve), tests/DNATEMP-Witness.
    // ══════════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>AS-IS <c>PlayTempPermanent</c> co-eval role (DNADigivolveEffects.cs:20-39, <c>finalCard:false</c>):
    /// the entity-less transient jointly evaluated against the DNA recipe. The mirror is a read-only
    /// <see cref="Permanent"/> VIEW over the card's own id + <c>snapshotZone:BattleArea</c> (RD-S3-BT17_095
    /// Option 2) — no <c>FieldPermanents</c> slot write / null-back (frameless substrate). AS-IS returned null on a
    /// bad frame (→ the requirement fails); the view always materialises (the documented capacity-gate
    /// subsumption), so callers keep the AS-IS null-guard shape but it is unreachable.</summary>
    private static Permanent TempMaterialView(CardSource card) =>
        new Permanent(card.Context, card.InstanceId, card.Owner, ChoiceZone.BattleArea);

    /// <summary>AS-IS <c>CardFulfillsRequirement</c> (DNADigivolveEffects.cs:50-95): can <paramref name="cardSource"/>
    /// be a jogress root of <paramref name="jogressTarget"/>? If <paramref name="firstCondition"/> is null it must
    /// fill slot[0] with SOME battle-area permanent filling slot[1]; otherwise it fills slot[1] with the fixed
    /// first root in slot[0].</summary>
    private static bool CardFulfillsRequirement(Player owner, CardSource cardSource, CardSource jogressTarget, Permanent? firstCondition, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? cardCondition = null)
    {
        if (jogressTarget.JogressConditionOf().Count <= 0)
        {
            return false;
        }

        bool isValid = false;
        if (cardCondition == null || cardCondition(cardSource))
        {
            Permanent tempPermanent = TempMaterialView(cardSource);
            if (firstCondition == null)
            {
                foreach (JogressCondition DNACondition in jogressTarget.JogressConditionOf())
                {
                    if (isValid)
                    {
                        break;
                    }

                    if (DNACondition.elements[0].EvoRootCondition(tempPermanent))
                    {
                        foreach (Permanent secondPermanent in owner.GetBattleAreaDigimons().Where(permanent => permanentCondition == null || permanentCondition(permanent)))
                        {
                            if (secondPermanent == tempPermanent)
                            {
                                continue;
                            }

                            if (DNACondition.elements[1].EvoRootCondition(secondPermanent))
                            {
                                isValid = true;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (JogressCondition DNACondition in jogressTarget.JogressConditionOf())
                {
                    if (DNACondition.elements[0].EvoRootCondition(firstCondition) && DNACondition.elements[1].EvoRootCondition(tempPermanent))
                    {
                        isValid = true;
                        break;
                    }
                }
            }

            // AS-IS :92 `owner.FieldPermanents[tempPermanent.PermanentFrame.FrameID] = null` — no-op (no slot was
            // written; the SnapshotZone co-eval view is read-only).
        }

        return isValid;
    }

    /// <summary>AS-IS <c>SelectHandCard</c> (DNADigivolveEffects.cs:97-119): pick 1 hand card that fills the
    /// remaining jogress root.</summary>
    private static async Task SelectHandCard(Player owner, CardSource jogressTarget, Permanent? firstCondition, bool isOptional, ICardEffect activateClass, Func<CardSource, Task> SelectCardCoroutine, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? digivolutionCardCondition = null)
    {
        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

        selectHandEffect.SetUp(
            selectPlayer: owner.PlayerId,
            canTargetCondition: cardSource => CardFulfillsRequirement(owner, cardSource, jogressTarget, firstCondition, permanentCondition, digivolutionCardCondition),
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            maxCount: 1,
            canNoSelect: isOptional,
            canEndNotMax: false,
            isShowOpponent: true,
            selectCardCoroutine: SelectCardCoroutine,
            afterSelectCardCoroutine: null,
            mode: SelectHandEffect.Mode.Custom,
            cardEffect: activateClass);

        selectHandEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.", "The opponent is selecting DNA digivolution cards.");
        selectHandEffect.SetNotShowCard();

        await selectHandEffect.Activate();
    }

    /// <summary>AS-IS <c>SelectTrashCard</c> (DNADigivolveEffects.cs:121-147): the trash-root variant of
    /// <see cref="SelectHandCard"/>.</summary>
    private static async Task SelectTrashCard(Player owner, CardSource jogressTarget, Permanent? firstCondition, bool isOptional, ICardEffect activateClass, Func<CardSource, Task> SelectCardCoroutine, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? digivolutionCardCondition = null)
    {
        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

        selectCardEffect.SetUp(
            canTargetCondition: cardSource => CardFulfillsRequirement(owner, cardSource, jogressTarget, firstCondition, permanentCondition, digivolutionCardCondition),
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            canNoSelect: () => isOptional,
            selectCardCoroutine: SelectCardCoroutine,
            afterSelectCardCoroutine: null,
            message: "Select 1 Digimon to DNA digivolve.",
            maxCount: 1,
            canEndNotMax: false,
            isShowOpponent: false,
            mode: SelectCardEffect.Mode.Custom,
            root: SelectCardEffect.Root.Trash,
            customRootCardList: null,
            canLookReverseCard: true,
            selectPlayer: owner.PlayerId,
            cardEffect: activateClass);

        selectCardEffect.SetNotShowCard();
        selectCardEffect.SetNotAddLog();

        await selectCardEffect.Activate();
    }

    /// <summary>AS-IS <c>PermanentFulfillsRequirement</c> (DNADigivolveEffects.cs:159-205): can
    /// <paramref name="permanent"/> be a jogress root of <paramref name="jogressTarget"/>? Symmetric to
    /// <see cref="CardFulfillsRequirement"/> — when <paramref name="firstCondition"/> is null the permanent fills
    /// slot[0] with SOME hand/trash card filling slot[1]; otherwise it fills slot[1] against the fixed
    /// first root.</summary>
    private static bool PermanentFulfillsRequirement(Player owner, Permanent permanent, CardSource jogressTarget, Permanent? firstCondition, bool isWithHandCard, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? cardCondition = null)
    {
        if (jogressTarget.JogressConditionOf().Count <= 0 || jogressTarget.CanNotEvolve(permanent))
        {
            return false;
        }

        if (permanentCondition == null || permanentCondition(permanent))
        {
            if (firstCondition == null)
            {
                foreach (JogressCondition DNACondition in jogressTarget.JogressConditionOf())
                {
                    if (DNACondition.elements[0].EvoRootCondition(permanent))
                    {
                        List<CardSource> sources = isWithHandCard ? owner.HandCards : owner.TrashCards;

                        foreach (CardSource cardSource in sources.Where(cardSource => cardCondition == null || cardCondition(cardSource)))
                        {
                            Permanent tempPermanent = TempMaterialView(cardSource);
                            bool isValid = DNACondition.elements[1].EvoRootCondition(tempPermanent);

                            // AS-IS :179 null-slot restore — no-op (read-only SnapshotZone view).
                            if (isValid)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            else
            {
                if (permanent == firstCondition)
                {
                    return false;
                }

                foreach (JogressCondition DNACondition in jogressTarget.JogressConditionOf())
                {
                    if (DNACondition.elements[0].EvoRootCondition(firstCondition) && DNACondition.elements[1].EvoRootCondition(permanent))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>SelectPermanent</c> (DNADigivolveEffects.cs:207-229): pick 1 battle-area permanent that
    /// fills a jogress root. The AS-IS <c>Func&lt;Permanent,bool&gt;</c> target predicate is adapted to the mirror
    /// id-shaped <see cref="SelectPermanentEffect"/> canTargetCondition (the W4 bridge idiom); DNA roots are
    /// owner-scoped, so the candidate view carries the owner's id (BT17_095 precedent).</summary>
    private static async Task SelectPermanent(Player owner, CardSource jogressTarget, Permanent? firstCondition, bool isOptional, ICardEffect activateClass, bool isWithHand, Func<Permanent, Task> SelectPermanentCoroutine, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? digivolutionCardCondition = null)
    {
        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

        selectPermanentEffect.SetUp(
            selectPlayer: owner.PlayerId,
            canTargetCondition: id => PermanentFulfillsRequirement(owner, new Permanent(owner.Context, id, owner.PlayerId), jogressTarget, firstCondition, isWithHand, permanentCondition, digivolutionCardCondition),
            canTargetCondition_ByPreSelecetedList: null,
            canEndSelectCondition: null,
            maxCount: 1,
            canNoSelect: isOptional,
            canEndNotMax: false,
            selectPermanentCoroutine: SelectPermanentCoroutine,
            afterSelectPermanentCoroutine: null,
            mode: SelectPermanentEffect.Mode.Custom,
            cardEffect: activateClass);

        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.", "The opponent is selecting 1 Digimon to DNA digivolve.");

        await selectPermanentEffect.Activate();
    }

    /// <summary>AS-IS <c>CanJogressWithHandOrTrash</c> (DNADigivolveEffects.cs:231-240) — the faithful co-eval
    /// gate (the DNA card sits in hand/trash, passes <paramref name="targetCardCondition"/>, has a recipe, and
    /// SOME permanent+card OR two cards fill it). This is the full-fidelity 7-arg AS-IS overload (Player owner +
    /// permanent/digivolution conditions); the 5-arg SpecialPlay-registry probe in CardEffectCommons.cs is a
    /// separate, distinct signature.</summary>
    public static bool CanJogressWithHandOrTrash(CardSource source, Player owner, bool isWithHandCard, bool isIntoHandCard, Func<CardSource, bool>? targetCardCondition = null, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? digivolutionCardCondition = null)
    {
        return (isIntoHandCard ? IsExistOnHand(source) : IsExistOnTrash(source))
            && (targetCardCondition == null || targetCardCondition(source))
            && source.JogressConditionOf().Count > 0
            && (HasMatchConditionPermanent(source, permanent => PermanentFulfillsRequirement(owner, permanent, source, null, isWithHandCard, permanentCondition, digivolutionCardCondition))
                || (isWithHandCard
                    ? owner.HandCards.Any(cardSource => CardFulfillsRequirement(owner, cardSource, source, null, permanentCondition, digivolutionCardCondition))
                    : owner.TrashCards.Any(cardSource => CardFulfillsRequirement(owner, cardSource, source, null, permanentCondition, digivolutionCardCondition))));
    }

    /// <summary>AS-IS <c>DNADigivolveWithHandOrTrashCardIntoHandOrTrash</c> (DNADigivolveEffects.cs:256-452),
    /// ported 1:1 (temp-material DNA family, RD-S3-EX6_072 — witnessed by EX6_072 + tests/DNATEMP-Witness). Selects
    /// a DNA-capable card from hand/trash to DNA-digivolve into, using ONE battle-area permanent and ONE hand/trash
    /// card (materialised via <see cref="CardObjectController.CreateNewPermanent"/>) as the two roots; executes the
    /// jogress via <see cref="PlayCardClass.SetJogress"/>; un-plays the temp material on abort. See the file-header
    /// substrate note for the Option-2 SnapshotZone mapping. (<paramref name="successProcess"/>/
    /// <paramref name="failedProcess"/> are threaded at the AS-IS signature; the AS-IS body never invokes them —
    /// kept verbatim.)</summary>
    public static async Task DNADigivolveWithHandOrTrashCardIntoHandOrTrash(
        Func<CardSource, bool> targetCardCondition,
        Func<Permanent, bool> permanentCondition,
        Func<CardSource, bool> digivolutionCardCondition,
        bool payCost,
        bool isWithHandCard,
        bool isIntoHandCard,
        ICardEffect activateClass,
        Func<Task>? successProcess,
        bool ignoreSelection = false,
        Func<Task>? failedProcess = null,
        bool isOptional = true)
    {
        _ = successProcess;
        _ = failedProcess;

        // AS-IS :273 `Player owner = activateClass.EffectSourceCard.Owner`. Guard the AS-IS deref (null source →
        // no-op, matching the DNADigivolvePermanentsIntoHandOrTrashCard overload's guard).
        if (activateClass?.EffectSourceCard is not { } sourceCard)
        {
            return;
        }

        Player owner = new Player(sourceCard.Context, sourceCard.Owner);

        CardSource? dnaTarget = null;
        Permanent? selectedPermanent = null;
        CardSource? selectedCardSource = null;
        Permanent? playedPermanent = null;

        Task SelectDNACardCoroutine(CardSource source)
        {
            dnaTarget = source;
            return Task.CompletedTask;
        }

        Task SelectPermanentCoroutine(Permanent permanent)
        {
            selectedPermanent = permanent;
            return Task.CompletedTask;
        }

        Task SelectCardCoroutine(CardSource cardSource)
        {
            selectedCardSource = cardSource;
            return Task.CompletedTask;
        }

        if (ignoreSelection)
        {
            dnaTarget = sourceCard;
        }
        else if (isIntoHandCard && owner.HandCards.Any(cardSource => CanJogressWithHandOrTrash(cardSource, owner, isWithHandCard, isIntoHandCard, targetCardCondition, permanentCondition, digivolutionCardCondition)))
        {
            SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

            selectHandEffect.SetUp(
                selectPlayer: owner.PlayerId,
                canTargetCondition: cardSource => CanJogressWithHandOrTrash(cardSource, owner, isWithHandCard, isIntoHandCard, targetCardCondition, permanentCondition, digivolutionCardCondition),
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: 1,
                canNoSelect: isOptional,
                canEndNotMax: false,
                isShowOpponent: true,
                selectCardCoroutine: SelectDNACardCoroutine,
                afterSelectCardCoroutine: null,
                mode: SelectHandEffect.Mode.Custom,
                cardEffect: activateClass);

            selectHandEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.", "The opponent is selecting 1 Digimon to DNA digivolve.");

            await selectHandEffect.Activate();
        }
        else if (!isIntoHandCard && owner.TrashCards.Any(cardSource => CanJogressWithHandOrTrash(cardSource, owner, isWithHandCard, isIntoHandCard, targetCardCondition, permanentCondition, digivolutionCardCondition)))
        {
            SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

            selectCardEffect.SetUp(
                canTargetCondition: cardSource => CanJogressWithHandOrTrash(cardSource, owner, isWithHandCard, isIntoHandCard, targetCardCondition, permanentCondition, digivolutionCardCondition),
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                canNoSelect: () => isOptional,
                selectCardCoroutine: SelectDNACardCoroutine,
                afterSelectCardCoroutine: null,
                message: "Select 1 Digimon to DNA digivolve.",
                maxCount: 1,
                canEndNotMax: false,
                isShowOpponent: false,
                mode: SelectCardEffect.Mode.Custom,
                root: SelectCardEffect.Root.Trash,
                customRootCardList: null,
                canLookReverseCard: true,
                selectPlayer: owner.PlayerId,
                cardEffect: activateClass);

            selectCardEffect.SetNotShowCard();
            selectCardEffect.SetNotAddLog();

            await selectCardEffect.Activate();
        }

        if (dnaTarget == null)
        {
            return;
        }

        bool validPermanent = HasMatchConditionPermanent(sourceCard, permanent => PermanentFulfillsRequirement(owner, permanent, dnaTarget, null, isWithHandCard, permanentCondition, digivolutionCardCondition));
        bool validHandOrTrash = isWithHandCard
            ? owner.HandCards.Any(cardSource => CardFulfillsRequirement(owner, cardSource, dnaTarget, null, permanentCondition, digivolutionCardCondition))
            : owner.TrashCards.Any(cardSource => CardFulfillsRequirement(owner, cardSource, dnaTarget, null, permanentCondition, digivolutionCardCondition));

        if (validPermanent || validHandOrTrash)
        {
            #region select source cards
            if (validPermanent && validHandOrTrash)
            {
                List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                {
                    new SelectionElement<bool>(message: $"From Battle Area", value: true, spriteIndex: 0),
                    new SelectionElement<bool>(message: isWithHandCard ? $"From Hand" : $"From Trash", value: false, spriteIndex: 1),
                };

                string selectPlayerMessage = "From where will you select the first digimon?";
                string notSelectPlayerMessage = "The opponent is selecting 1 Digimon to DNA digivolve.";

                GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: owner.PlayerId, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
            }
            else
            {
                GManager.instance.userSelectionManager.SetBool(validPermanent);
            }

            await GManager.instance.userSelectionManager.WaitForEndSelect();

            bool isPermanentFirst = GManager.instance.userSelectionManager.SelectedBoolValue;
            if (isPermanentFirst)
            {
                await SelectPermanent(owner, dnaTarget, null, isOptional, activateClass, isWithHandCard, SelectPermanentCoroutine, permanentCondition, digivolutionCardCondition);

                if (selectedPermanent != null)
                {
                    if (isWithHandCard)
                    {
                        await SelectHandCard(owner, dnaTarget, selectedPermanent, isOptional, activateClass, SelectCardCoroutine, permanentCondition, digivolutionCardCondition);
                    }
                    else
                    {
                        await SelectTrashCard(owner, dnaTarget, selectedPermanent, isOptional, activateClass, SelectCardCoroutine, permanentCondition, digivolutionCardCondition);
                    }

                    if (selectedCardSource != null)
                    {
                        // AS-IS :394-396 `PlayTempPermanent(selectedCardSource, true)` + `CreateNewPermanent(
                        // playedPermanent, PreferredFrame().FrameID)` → the frameless materialisation (file header).
                        playedPermanent = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);
                    }
                }
            }
            else
            {
                if (isWithHandCard)
                {
                    await SelectHandCard(owner, dnaTarget, null, isOptional, activateClass, SelectCardCoroutine, permanentCondition, digivolutionCardCondition);
                }
                else
                {
                    await SelectTrashCard(owner, dnaTarget, null, isOptional, activateClass, SelectCardCoroutine, permanentCondition, digivolutionCardCondition);
                }

                if (selectedCardSource != null)
                {
                    // AS-IS :409-414 materialise the temp material, THEN pick the field root against it.
                    playedPermanent = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);

                    await SelectPermanent(owner, dnaTarget, playedPermanent, isOptional, activateClass, isWithHandCard, SelectPermanentCoroutine, permanentCondition, digivolutionCardCondition);
                }
            }
            #endregion

            if (selectedPermanent != null && playedPermanent != null)
            {
                List<Permanent> orderedRoots = isPermanentFirst ? new List<Permanent>() { selectedPermanent, playedPermanent } : new List<Permanent>() { playedPermanent, selectedPermanent };
                int[] JogressEvoRootsFrameIDs = orderedRoots.Select(permanent => permanent.PermanentFrame.FrameID).ToArray();

                if (dnaTarget.CanJogressFromTargetPermanents(orderedRoots, payCost))
                {
                    PlayCardClass playCard = new PlayCardClass(
                        cardSources: new List<CardSource>() { dnaTarget },
                        hashtable: CardEffectHashtable(activateClass),
                        payCost: payCost,
                        targetPermanent: null,
                        isTapped: false,
                        root: isIntoHandCard ? SelectCardEffect.Root.Hand : SelectCardEffect.Root.Trash,
                        activateETB: true);

                    playCard.SetJogress(JogressEvoRootsFrameIDs);

                    await playCard.PlayCard();
                }
            }
        }

        // AS-IS :442-451 rollback: if the DNA target never became a live permanent (jogress aborted), un-play the
        // materialised temp material back to its origin zone.
        if (dnaTarget == null || dnaTarget.PermanentOfThisCard() == null)
        {
            if (playedPermanent != null)
            {
                if (isWithHandCard)
                {
                    await CardObjectController.AddHandCard(selectedCardSource!, false);
                }
                else
                {
                    await CardObjectController.AddTrashCard(selectedCardSource!);
                }
            }
        }
    }

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
