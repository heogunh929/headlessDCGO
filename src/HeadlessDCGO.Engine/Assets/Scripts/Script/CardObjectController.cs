// Source: Assets/Scripts/Script/CardObjectController.cs
// Decision: PORT
// Category: GameState
// Priority: HIGH
// Migration: Port core engine source — INCREMENTAL.
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// (R3-A) AS-IS CardObjectController is a FILE of static zone-move helper methods (RemoveFromAllArea / RemoveField
// / AddTrashCard / AddSecurityCard / …) the primitive library + DestroyPermanentsClass call directly. This mirror
// grows in AS-IS file order, method by method. Only the methods needed by the R3-A deletion / security re-housing
// land here now: RemoveFromAllArea (:370), RemoveField (:513), AddTrashCard (:717), AddSecurityCard (:976).
//
// SUBSTRATE SEAM (存移動 適用部 = the retained substrate per bigbang §3-R2): the AS-IS Player-list mutations
// (`Owner.SecurityCards.Insert(0, x)`, `Owner.TrashCards.Insert(0, x)`, frame nulling) are NOT re-expressible on
// the mirror's read-only zone VIEWS — they translate to the authoritative IZoneMover writes the sink already owns
// (`MoveCardToSingleZone`/AddToSecurityAsync/AddToTrashAsync do the remove-from-all-zones + insert). The AS-IS
// control-flow STRUCTURE / order / guards are preserved 1:1; only the physical list mutation delegates to the
// zone mover. `cardSource.Init()` (AS-IS clears the leaving card's transient effect list) is a substrate no-op —
// the mirror CardSource is a transient view holding no per-instance mutable effect state (bindings live in the
// registry, removed on leave-play). Effects/PlayLog/SE = UI (stripped).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script
{

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R3-A note) the static helper library <c>CardEffectCommons</c> is a CLASS whose simple name collides with the
// sibling namespace <c>...Script.CardEffectCommons</c> (imported above for <c>Permanent</c>); from this compilation
// unit the bare <c>CardEffectCommons</c> binds to that namespace, so its methods are reached through the
// namespace-qualified class path <c>CardEffectCommons.CardEffectCommons.X(...)</c>.
public static class CardObjectController
{
    #region remove card from all area

    /// <summary>(R3-A) 1:1 of AS-IS <c>CardObjectController.RemoveFromAllArea</c> (CardObjectController.cs:370-404):
    /// detach the card from any permanent it is embedded in (a link via RemoveLinkedCard(trashCard:false), a buried
    /// source via the bare RemoveCardSource), then withdraw it from whatever concrete zone (hand/deck/digitama/trash)
    /// it physically sits in. The mirror expresses BOTH arms via the zone mover: the physical withdrawal is one
    /// Move→None; the embedded-source/link detach is the verified <see cref="Permanent.RemoveCardSource"/> /
    /// <see cref="Permanent.RemoveLinkedCard"/> re-housed in R2 (walked over every field permanent, AS-IS :381-403).
    /// AS-IS SetFace / DeleteHandCardEffectCoroutine (:373-378) = face-state + UI, folded into the callers' explicit
    /// face stamp / stripped.</summary>
    public static async Task RemoveFromAllArea(CardSource cardSource, CancellationToken cancellationToken = default)
    {
        EngineContext context = cardSource.Context;

        // AS-IS :381-403 — scan every field permanent; if this card is embedded, detach it (link vs source).
        foreach (Player player in new GameContext(context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                if (permanent.cardSources.Any(cs => cs.InstanceId == cardSource.InstanceId))
                {
                    if (permanent.LinkedCards.Any(lc => lc.InstanceId == cardSource.InstanceId))
                    {
                        await permanent.RemoveLinkedCard(cardSource, trashCard: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await permanent.RemoveCardSource(cardSource, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        // AS-IS :407-428 — physically pull the card out of hand/deck/digitama/trash (whichever it is in).
        ChoiceZone from = CurrentZoneOf(context, cardSource.Owner, cardSource.InstanceId);
        if (from != ChoiceZone.None)
        {
            await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(cardSource.Owner, cardSource.InstanceId, from, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region create a new permanent

    /// <summary>(R4 S3b-2①) AS-IS <c>CardObjectController.CreateNewPermanent(permanent, frameID)</c>
    /// (CardObjectController.cs:479-510). SUBSTRATE MAPPING: the mirror <see cref="Permanent"/> is a
    /// zone-derived VIEW, so the AS-IS two-step — <c>new Permanent(cards){IsSuspended} +
    /// CreateNewPermanent(permanent, frameID)</c> — collapses into this one op:
    /// <list type="bullet">
    /// <item>AS-IS :485 RemoveFromAllArea(top) — kept 1:1;</item>
    /// <item>AS-IS :488-491 frame-slot assignment (<c>FieldPermanents[frameID] = permanent</c>) → the
    /// authoritative zone ENTRY (BattleArea / BreedingArea append — the established no-frame/slot
    /// adaptation, RD-P6C1-2 family);</item>
    /// <item>the AS-IS <c>Permanent</c>-object initializer state (<c>IsSuspended</c>) + the sink's
    /// entered-this-turn stamp → instance metadata;</item>
    /// <item>G6-001: AS-IS effects live statically ON the card object — the mirror's equivalent is the
    /// ported-effect auto-registration on field entry (<see cref="CardEffectRegistrar.RegisterCard"/>,
    /// the same call the verified play/digivolve actions make);</item>
    /// <item>AS-IS :493-508 SetFace / FieldPermanentCard instantiation / canvas position = UI (stripped).</item>
    /// </list>
    /// The zone entry carries NO OnEnterField supply metadata — the AS-IS executor opens the
    /// OnEnterFieldAnyone window INLINE (PlayPermanentClass :1694), so the SkillWindowSupply conversion
    /// GAP-drops this move (design doc S3b-2① — the supply half stays the OLD path's seat until S3c).</summary>
    public static async Task<Permanent> CreateNewPermanent(
        CardSource card,
        bool isSuspended,
        bool isBreedingArea = false,
        CancellationToken cancellationToken = default)
    {
        EngineContext context = card.Context;

        await RemoveFromAllArea(card, cancellationToken).ConfigureAwait(false);

        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                card.Owner,
                card.InstanceId,
                ChoiceZone.None,
                isBreedingArea ? ChoiceZone.BreedingArea : ChoiceZone.BattleArea),
            cancellationToken).ConfigureAwait(false);

        if (context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) && record is not null)
        {
            var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
            {
                ["isSuspended"] = isSuspended,
                [MatchStateMutationSink.EnteredThisTurnKey] = true,
            };
            context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
        }

        // (R4 S3b-2② → RD-R3-02) fresh AS-IS `new Permanent(...)` semantics (a re-played card must not see
        // the just-after bookkeeping of its previous life) are enforced by the zone-mover lifetime chokepoint
        // at the None→field move above (InMemoryZoneMover.MoveCard) — shared by EVERY field-entry path, not
        // just this one.

        CardEffectRegistrar.RegisterCard(context, card.InstanceId, card.Owner);
        return new Permanent(context, card.InstanceId, card.Owner);
    }

    #endregion

    #region remove permanent from field

    /// <summary>(R3-A) 1:1 of AS-IS <c>CardObjectController.RemoveField</c> (CardObjectController.cs:513-555): open
    /// the OnRemovedField cut-in window for the leaving permanent, charge the ACE overflow of its whole stack
    /// (unless ignoreOverflow), then pull the top card off the field (AS-IS nulls the frame / FieldPermanents slot).
    /// The mirror's field removal = a Move BattleArea/BreedingArea→None (the frame-null substrate). cardSources.Init()
    /// / cardSources reset (:546-554) = the transient-view substrate no-op (see file header).</summary>
    public static async Task RemoveField(Permanent permanent, bool ignoreOverflow = false, CancellationToken cancellationToken = default)
    {
        if (permanent == null) return;
        CardSource topCard = permanent.TopCard;
        if (topCard == null) return;

        EngineContext context = topCard.Context;

        // AS-IS :518-524 "When this permanent would remove field" cut-in (null cardEffect / null battle).
        await GManager.instance.autoProcessing_CutIn.StackSkillInfos(
            CardEffectCommons.CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(
                new List<Permanent> { permanent },
                null,
                null),
            EffectTiming.OnRemovedField).ConfigureAwait(false);

        // AS-IS :526-529 the stack's ACE overflow.
        if (!ignoreOverflow)
        {
            await new AceOverflowClass(permanent.cardSources).Overflow(cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :531-543 remove the top off its field slot (frame null) — Move to None.
        ChoiceZone from = CurrentZoneOf(context, permanent.OwnerId, permanent.InstanceId);
        if (from is ChoiceZone.BattleArea or ChoiceZone.BreedingArea)
        {
            await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(permanent.OwnerId, permanent.InstanceId, from, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);

            // (R4 S3b-2② → RD-R3-02) the AS-IS Permanent object dies with the field slot — its just-after
            // bookkeeping reset is enforced by the zone-mover lifetime chokepoint at the field→None move
            // above (InMemoryZoneMover.MoveCard), shared by EVERY field-leave path, not just this one.
        }
    }

    #endregion

    #region add card to trash

    /// <summary>(R3-A) 1:1 of AS-IS <c>CardObjectController.AddTrashCard</c> (CardObjectController.cs:717-734): if the
    /// card is not already in the trash, withdraw it from wherever it is and (for a non-token) turn it face-up and
    /// insert it at the top of the trash. AS-IS SetFace() = face stamp; the withdraw+insert = one AddToTrashAsync
    /// (remove-from-all-zones + insert). cardSource.Init() = substrate no-op (file header).</summary>
    public static async Task AddTrashCard(CardSource cardSource, CancellationToken cancellationToken = default)
    {
        if (!CardEffectCommons.CardEffectCommons.IsExistOnTrash(cardSource))
        {
            await RemoveFromAllArea(cardSource, cancellationToken).ConfigureAwait(false);

            if (!cardSource.IsToken)
            {
                // AS-IS SetFace() — a trashed card is face-up.
                SecurityFaceState.Stamp(cardSource.Context.CardInstanceRepository, cardSource.InstanceId, faceUp: true);

                if (!CardEffectCommons.CardEffectCommons.IsExistOnTrash(cardSource))
                {
                    await cardSource.Context.ZoneMover.AddToTrashAsync(cardSource.Owner, cardSource.InstanceId, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    #endregion

    #region add card to security

    /// <summary>(R4 S3b-2③) 1:1 of AS-IS <c>CardObjectController.AddExecutingCard</c>
    /// (CardObjectController.cs:957-972): skip when already executing; withdraw from everywhere; a non-token
    /// goes face-up onto the owner's EXECUTING pile (AS-IS <c>Owner.ExecutingCards.Insert(0, …)</c> → the
    /// substrate Execution zone; SetFace = the move's face stamp; <c>Init()</c> = the transient-view no-op,
    /// file header). The option executor (UseOptionClass) parks the resolving option here.</summary>
    public static async Task AddExecutingCard(CardSource cardSource, CancellationToken cancellationToken = default)
    {
        EngineContext context = cardSource.Context;

        if (CurrentZoneOf(context, cardSource.Owner, cardSource.InstanceId) == ChoiceZone.Execution)
        {
            return;
        }

        await RemoveFromAllArea(cardSource, cancellationToken).ConfigureAwait(false);

        if (!cardSource.IsToken)
        {
            await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(cardSource.Owner, cardSource.InstanceId, ChoiceZone.None, ChoiceZone.Execution, FaceUp: true),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>(R3-A / RD-P6C2-1) 1:1 of AS-IS <c>CardObjectController.AddSecurityCard</c>
    /// (CardObjectController.cs:976-1007): if the card is not already in security, withdraw it from all areas, then a
    /// DigiEgg goes to the digitama library and a non-token card is inserted (face-down unless faceUp) into the
    /// security stack — top by default, bottom when !toTop — and the <see cref="IAddSecurity"/> "when security cards
    /// are added" window opens. AS-IS <c>SetReverse()/SetFace()</c> = the <see cref="SecurityFaceState"/> stamp (the
    /// established IAddSecurityFromLibrary route); <c>SecurityCards.Insert(0,x)</c> / the !toTop remove+add =
    /// AddToSecurityAsync(toTop); CreateRecoveryEffect (useEffect) = UI (stripped, so useEffect is unused past the
    /// gate). A single-card add gets one per-card security batch id (NextSecurityAddBatchId).</summary>
    public static async Task AddSecurityCard(CardSource cardSource, bool toTop = true, bool faceUp = false, bool useEffect = true, CancellationToken cancellationToken = default)
    {
        _ = useEffect; // AS-IS gates ONLY the (stripped) CreateRecoveryEffect VFX.
        EngineContext context = cardSource.Context;

        var owner = new Player(context, cardSource.Owner);
        if (!owner.SecurityCards.Any(c => c.InstanceId == cardSource.InstanceId))
        {
            await RemoveFromAllArea(cardSource, cancellationToken).ConfigureAwait(false);

            if (cardSource.IsDigiEgg)
            {
                // AS-IS :984 Owner.DigitamaLibraryCards.Add(cardSource).
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(cardSource.Owner, cardSource.InstanceId, ChoiceZone.None, ChoiceZone.DigitamaLibrary),
                    cancellationToken).ConfigureAwait(false);
            }
            else if (!cardSource.IsToken)
            {
                // AS-IS :988-991 SetReverse()/SetFace() BEFORE the insert.
                SecurityFaceState.Stamp(context.CardInstanceRepository, cardSource.InstanceId, faceUp: faceUp);

                // AS-IS :993-999 Insert(0) then the !toTop demote — expressed as the top/bottom insert.
                await context.ZoneMover.AddToSecurityAsync(
                    cardSource.Owner, cardSource.InstanceId, faceUp: faceUp, toTop: toTop,
                    addSecurityBatchId: context.NextSecurityAddBatchId(),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // AS-IS :1001-1002 CreateRecoveryEffect (useEffect) = UI (stripped).

                // AS-IS :1004 the "when security cards are added" window (unconditional per non-token, non-DigiEgg).
                await new IAddSecurity(cardSource).AddSecurity(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region add cards to hand

    /// <summary>(R6-P / RD-R6-03, retires RD-P6C1-8) 1:1 of AS-IS <c>CardObjectController.AddHandCards</c>
    /// (CardObjectController.cs:559-623): move a list of cards into their owners' hands. Control flow is AS-IS
    /// VERBATIM — drop the cards already in hand; if ANY came from trash open the "[When a card is returned from
    /// the trash to the hand]" (OnReturnCardsToHandFromTrash) window on the surviving list; strip token views;
    /// route DigiEggs to the library bottom (AS-IS QUIRK preserved: the <c>eggCards.Count &lt;= 0</c> guard means
    /// this only ever runs on an EMPTY list — an inert AS-IS call, mirrored verbatim); charge the added cards'
    /// ACE overflow; add each; then, past game start, open the "[When the number of cards in hand increases]"
    /// (OnAddHand) window. ADAPTATION: OnAddHand is not a manual StackSkillInfos here — the mirror derives it from
    /// each card's ->Hand CardMoved stamped with a SHARED add-hand batch id (so a multi-card add collapses to one
    /// reactor fire, AS-IS's single StackSkillInfos) + the CAUSE effect id (AS-IS <c>{"CardEffect", …}</c>, so a
    /// non-effect add does not fire the gate) — the same zone-derived convention <see cref="AddSecurityCard"/>
    /// uses for OnAddSecurity.</summary>
    public static async Task AddHandCards(List<CardSource> cardSources, bool isDraw, ICardEffect? cardEffect, CancellationToken cancellationToken = default)
    {
        if (cardSources.Count == 0)
        {
            return;
        }

        EngineContext context = cardSources[0].Context;

        // AS-IS :563 whether any subject currently sits in trash (drives the return-from-trash window).
        bool isFromTrash = cardSources.Any(cardSource => CardEffectCommons.CardEffectCommons.IsExistOnTrash(cardSource));

        // AS-IS :565 drop the cards already in their owner's hand.
        cardSources = cardSources
            .Where(cardSource => cardSource != null
                && !new Player(context, cardSource.Owner).HandCards.Any(c => c.InstanceId == cardSource.InstanceId))
            .ToList();

        if (isFromTrash)
        {
            // AS-IS :567-579 StackSkillInfos(OnReturnCardsToHandFromTrash) with {"CardSources", cardSources}.
            var hashtable = new System.Collections.Hashtable { { "CardSources", cardSources } };
            await GManager.instance.autoProcessing.StackSkillInfos(hashtable, EffectTiming.OnReturnCardsToHandFromTrash).ConfigureAwait(false);
        }

        // AS-IS :582-588 tokens vanish (removed from all areas) rather than entering hand.
        foreach (CardSource cardSource in cardSources)
        {
            if (cardSource.IsToken)
            {
                await RemoveFromAllArea(cardSource, cancellationToken).ConfigureAwait(false);
            }
        }

        List<CardSource> eggCards = cardSources.Where(cardSource => cardSource.IsDigiEgg).ToList();
        List<CardSource> addedCards = cardSources.Where(cardSource => !cardSource.IsDigiEgg && !cardSource.IsToken).ToList();

        // AS-IS :593-594 QUIRK — the guard is `eggCards.Count <= 0`, so AddLibraryBottomCards runs ONLY on an empty
        // list (an inert call). Mirrored verbatim: a DigiEgg among returned cards is silently dropped (no hand, no
        // library) exactly as AS-IS, so nothing is done here.

        if (addedCards.Count <= 0)
        {
            return;
        }

        // AS-IS :598 the added cards' ACE overflow (memory swing on an ACE leaving the field to hand).
        await new AceOverflowClass(addedCards).Overflow(cancellationToken).ConfigureAwait(false);

        // AS-IS :600-603 add each. One SHARED add-hand batch id across the list = AS-IS's single
        // StackSkillInfos(OnAddHand); the cause id gates the derived window (AS-IS {"CardEffect", …}).
        long addHandBatchId = context.NextAddHandBatchId();
        HeadlessEntityId? cause = cardEffect?.EffectSourceCard?.InstanceId;
        if (cause is { IsEmpty: true })
        {
            cause = null;
        }

        foreach (CardSource cardSource in addedCards)
        {
            await AddHandCard(cardSource, isDraw, addHandBatchId, cause, cancellationToken).ConfigureAwait(false);
        }

        // (C1) AS-IS CardObjectController.cs:605-621 — drained from C2 flip. StackSkillInfos({"Players", Players},
        // {"CardEffect", cardEffect}, {"CardSources", addedCards}, OnAddHand) guarded by DoneStartGame && count>0.
        // Unlike the id-threading routines, AddHandCards keeps the live ICardEffect? cardEffect param, so the
        // {"CardEffect"} member IS reconstructable verbatim here; Players = the distinct owners as mirror Player views.
        if (GManager.instance.turnStateMachine.DoneStartGame && addedCards.Count > 0)
        {
            List<Player> Players = addedCards.Select(cardSource => cardSource.Owner).Distinct()
                .Select(playerId => new Player(context, playerId)).ToList();
            await GManager.instance.autoProcessing.StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "Players", Players },
                    { "CardEffect", cardEffect },
                    { "CardSources", addedCards },
                },
                EffectTiming.OnAddHand).ConfigureAwait(false);
        }

        // AS-IS :605-621 the OnAddHand window (past game start) is now derived from the ->Hand CardMoveds above.
    }

    /// <summary>(R6-P / RD-R6-03) 1:1 of AS-IS <c>CardObjectController.AddHandCard</c> (CardObjectController.cs:625-636+):
    /// place ONE card into its owner's hand — face it up, and if it is not already there withdraw it from wherever
    /// it sits and insert it (non-tokens only; a token was already removed by the caller). AS-IS
    /// <c>Owner.HandCards.Add</c> = the zone mover's ->Hand insert; everything after (:638+ Instantiate/animation/
    /// AlignHand/SE) is UI (stripped). The batch/cause ids stamp the derived OnAddHand window (see
    /// <see cref="AddHandCards"/>).</summary>
    /// <summary>(RD-P6C1-8 residual RESOLVED) Public 1:1 of the AS-IS single-card
    /// <c>CardObjectController.AddHandCard(cardSource, isDraw)</c> (CardObjectController.cs:625-636) — the
    /// entry the temp-material DNA ROLLBACK (BT17_095 &lt;Delay&gt; / DNADigivolveEffects failed-flow) calls to
    /// un-materialize a temp material back to hand. Delegates to the private batch-stamped body with a fresh
    /// add-hand batch id and NO cause (a plain restore, not an effect-driven add) — the AS-IS single AddHandCard
    /// likewise has no OnAddHand cut-in beyond the ACE/window scaffolding the PLURAL <see cref="AddHandCards"/>
    /// owns.</summary>
    public static Task AddHandCard(CardSource cardSource, bool isDraw, CancellationToken cancellationToken = default) =>
        AddHandCard(cardSource, isDraw, cardSource.Context.NextAddHandBatchId(), causeEffectId: null, cancellationToken);

    private static async Task AddHandCard(
        CardSource cardSource, bool isDraw, long addHandBatchId, HeadlessEntityId? causeEffectId, CancellationToken cancellationToken)
    {
        _ = isDraw; // AS-IS gates only the (stripped) draw VFX/SE.

        // AS-IS :628 SetFace() — a card entering hand is face-up.
        SecurityFaceState.Stamp(cardSource.Context.CardInstanceRepository, cardSource.InstanceId, faceUp: true);

        var owner = new Player(cardSource.Context, cardSource.Owner);
        if (!owner.HandCards.Any(c => c.InstanceId == cardSource.InstanceId))
        {
            // AS-IS :632 RemoveFromAllArea then :636 HandCards.Add — the withdraw + the ->Hand insert.
            await RemoveFromAllArea(cardSource, cancellationToken).ConfigureAwait(false);

            if (!cardSource.IsToken)
            {
                await cardSource.Context.ZoneMover.AddToHandAsync(
                    cardSource.Owner, cardSource.InstanceId,
                    addHandBatchId: addHandBatchId, causeEffectId: causeEffectId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    #endregion

    /// <summary>The concrete zone <paramref name="cardId"/> currently sits in for <paramref name="owner"/> (or
    /// <see cref="ChoiceZone.None"/> if none) — the live fromZone a card's unknown AS-IS origin needs (mirror of
    /// the Permanent.CurrentZoneOf substrate helper).</summary>
    private static ChoiceZone CurrentZoneOf(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId cardId)
    {
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return ChoiceZone.None;
        }

        foreach (KeyValuePair<ChoiceZone, IReadOnlyList<HeadlessEntityId>> pair in zones.Snapshot(owner))
        {
            if (pair.Value.Contains(cardId))
            {
                return pair.Key;
            }
        }

        return ChoiceZone.None;
    }
}

}
