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
