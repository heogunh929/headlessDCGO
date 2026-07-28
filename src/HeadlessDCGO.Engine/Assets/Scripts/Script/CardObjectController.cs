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
// zone mover. Effects/PlayLog/SE = UI (stripped).
//
// (fidelity defect E) The former header claim that `cardSource.Init()` is "a substrate no-op" was FALSE: the mirror
// `CardSource.Init()` (CardSource.cs:181) is real — it clears the card's per-turn use counts
// (`cEntity_EffectController.InitUseCountThisTurn()`) and materialises its effect lists. Every AS-IS
// `cardSource.Init()` site in this file is now called at the AS-IS position/guard: :550 (RemoveField),
// :627 (AddHandCard), :732 (AddTrashCard), :835 (AddLibraryTopCards), :929 (AddLibraryBottomCards),
// :969 (AddExecutingCard). AS-IS :773 belongs to `AddTrashCards(List<CardSource>)`, which this mirror does not
// have at all (unported method — reported, not invented).
// The mirror `Init()` does NOT carry AS-IS Init()'s other two statements: `SetFace()` (each mirror move performs
// its own face stamp at its destination) and `SetChangedLocationTime()` (no carrier — design item
// MIG3-LOCATIONTIME). See CardSource.cs:118-122.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script
{

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R3-A note) the static helper library <c>CardEffectCommons</c> is a CLASS whose simple name collides with the
// sibling namespace <c>...Script.CardEffectCommons</c> (imported above for <c>Permanent</c>); from this compilation
// unit the bare <c>CardEffectCommons</c> binds to that namespace, so its methods are reached through the
// namespace-qualified class path <c>CardEffectCommons.CardEffectCommons.X(...)</c>.
public static class CardObjectController
{
    #region デッキカード生成

    /// <summary>1:1 of AS-IS <c>CardObjectController.CreatePlayerDecks</c> (CardObjectController.cs:16-341), called
    /// from AS-IS <c>TurnStateMachine.Init</c> (:233). AS-IS body is three stages:
    /// <list type="number">
    /// <item>(:22-93) pick the AI opponent's deck — <c>GManager.instance.IsAI</c> / <c>ContinuousController.DeckDatas</c>
    /// / a random 50+5 sample. The headless opponent is the RL agent, not the Unity AI bot, so this stage has no
    /// analog: BOTH seats' decks arrive from the platform, exactly as the human-vs-human path.</item>
    /// <item>(:95-317) resolve each Photon client's deck recipe — <c>DeckRecipie(player)</c> /
    /// <c>DigitamaDeckRecipie(player)</c> read <c>player.CustomProperties[DeckDataPropertyKey]</c> into a
    /// <c>DeckData</c> and return <c>RandomUtility.ShuffledDeckCards(deckData.DeckCards())</c>. THE PLATFORM READ:
    /// headless has no Photon room, so the deck definition-id lists come from the substrate
    /// <see cref="MatchSetupConfig"/> instead — same seam, same call path, only the data source differs. The
    /// AS-IS shuffle is preserved: <c>RandomUtility.ShuffledDeckCards</c> ≡ the seeded
    /// <see cref="IRandomSource.Shuffle"/> (GameRandomSource is the byte-identical mirror of AS-IS GameRandom).</item>
    /// <item>(:319-341) 「カードを生成」 — instantiate every recipe entry and append it to that seat's zone:
    /// <c>PlayerFromID(n).LibraryCards.Add(CreateCardSource(...))</c> / <c>.DigitamaLibraryCards.Add(...)</c>.
    /// The mirror's zone views are read-only, so the append is the authoritative substrate write (the same
    /// translation this file's header documents for every other AS-IS list mutation): a MoveToDeckBottom per main-deck
    /// card reproduces <c>LibraryCards.Add</c>'s ORDER exactly (append to the deck's bottom, top = index 0), and a
    /// Move→DigitamaLibrary does the same for the egg deck. <c>GManager.instance.CardIndex</c> (:320) is the AS-IS
    /// per-card instance counter — mirrored by the per-seat/per-deck instance-id ordinal.</item>
    /// </list>
    /// AS-IS iterates MASTER client first, then non-master (:322-340); the mirror walks the configured player order,
    /// which is that same seat order (player id 0/1 ≡ master/non-master, cf. TurnStateMachine.cs:277).</summary>
    public static async Task CreatePlayerDecks(
        EngineContext context,
        MatchSetupConfig setup,
        IReadOnlyList<HeadlessPlayerId> playerIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(playerIds);

        Dictionary<HeadlessPlayerId, PlayerDeckSetup> decksByPlayer =
            setup.PlayerDecks.ToDictionary(deck => deck.PlayerId);

        // AS-IS :322-340 — per seat, in master-then-non-master order.
        foreach (HeadlessPlayerId playerId in playerIds)
        {
            if (!decksByPlayer.TryGetValue(playerId, out PlayerDeckSetup? deck))
            {
                continue;
            }

            // AS-IS DeckRecipie / DigitamaDeckRecipie: the platform deck list, SHUFFLED (:143/:229 etc.).
            IReadOnlyList<HeadlessEntityId> mainDeck =
                PrepareDeck(deck.MainDeckDefinitionIds, setup.ShuffleDecks, context.RandomSource);
            IReadOnlyList<HeadlessEntityId> digitamaDeck =
                PrepareDeck(deck.DigitamaDeckDefinitionIds, setup.ShuffleDigitamaDecks, context.RandomSource);

            // AS-IS :323-327 LibraryCards.Add(CreateCardSource(...)) per main-deck card.
            await SeedDeck(context, playerId, mainDeck, ChoiceZone.Library, "main", cancellationToken)
                .ConfigureAwait(false);
            // AS-IS :329-333 DigitamaLibraryCards.Add(CreateCardSource(...)) per egg-deck card.
            await SeedDeck(context, playerId, digitamaDeck, ChoiceZone.DigitamaLibrary, "digitama", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>RandomUtility.ShuffledDeckCards(deckData.DeckCards())</c> — the deck list in draw order.
    /// Shuffling is the AS-IS default for every deck source; a deterministic setup opts out.</summary>
    private static IReadOnlyList<HeadlessEntityId> PrepareDeck(
        IReadOnlyList<HeadlessEntityId> source,
        bool shuffle,
        IRandomSource randomSource)
    {
        HeadlessEntityId[] cards = source.ToArray();
        if (shuffle)
        {
            randomSource.Shuffle(cards);
        }

        return cards;
    }

    /// <summary>AS-IS <c>CreateCardSource(playerId, cEntity_Base, false)</c> + the zone append (:319-341): register
    /// the instance against its definition, then place it. Order-exact — the main deck appends to the deck BOTTOM so
    /// index 0 stays the top card, matching <c>LibraryCards.Add</c>.</summary>
    private static async Task SeedDeck(
        EngineContext context,
        HeadlessPlayerId playerId,
        IReadOnlyList<HeadlessEntityId> definitionIds,
        ChoiceZone zone,
        string deckKind,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < definitionIds.Count; index++)
        {
            HeadlessEntityId definitionId = definitionIds[index];
            // AS-IS GManager.instance.CardIndex (:320) — the per-card instance ordinal.
            HeadlessEntityId instanceId = new(
                $"p{playerId.Value}:{deckKind}:{index + 1:D3}:{definitionId.Value}");
            context.CardInstanceRepository.Upsert(new CardInstanceRecord(
                instanceId,
                definitionId,
                playerId,
                Metadata: new Dictionary<string, object?>
                {
                    ["setupDeck"] = deckKind,
                    ["setupIndex"] = index
                }));

            if (zone == ChoiceZone.DigitamaLibrary)
            {
                await context.ZoneMover
                    .MoveAsync(new ZoneMoveRequest(playerId, instanceId, ChoiceZone.None, ChoiceZone.DigitamaLibrary), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await context.ZoneMover
                    .MoveToDeckBottomAsync(playerId, instanceId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    #endregion

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
    /// ported-effect fresh-play-context init on field entry (AS-IS <c>CardSource.Init()</c>,
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
                [ZoneMoveMetadataKeys.EnteredThisTurnKey] = true,
            };
            context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
        }

        // (R4 S3b-2② → RD-R3-02) fresh AS-IS `new Permanent(...)` semantics (a re-played card must not see
        // the just-after bookkeeping of its previous life) are enforced by the zone-mover lifetime chokepoint
        // at the None→field move above (InMemoryZoneMover.MoveCard) — shared by EVERY field-entry path, not
        // just this one.

        CardSource.Init(context, card.InstanceId, card.Owner);
        return new Permanent(context, card.InstanceId, card.Owner);
    }

    #endregion

    #region remove permanent from field

    /// <summary>(R3-A) 1:1 of AS-IS <c>CardObjectController.RemoveField</c> (CardObjectController.cs:513-555): open
    /// the OnRemovedField cut-in window for the leaving permanent, charge the ACE overflow of its whole stack
    /// (unless ignoreOverflow), then pull the top card off the field (AS-IS nulls the frame / FieldPermanents slot).
    /// The mirror's field removal = a Move BattleArea/BreedingArea→None (the frame-null substrate). AS-IS :546-554
    /// (per-stack-card <c>Init()</c> + the <c>cardSources</c> reset) is reproduced below — the Init loop verbatim, the
    /// list reset by the zone-mover lifetime chokepoint.</summary>
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

        // AS-IS :545-554 `if (permanent.TopCard != null) { foreach (cardSource in permanent.cardSources)
        // cardSource.Init(); permanent.cardSources = new List<CardSource>(); }`. AS-IS reads `cardSources` AFTER the
        // frame nulling because the AS-IS frame-null does NOT touch the Permanent's own stack list; the mirror's
        // translation of the frame-null (the field->None zone move) DOES tear the stack down, so the AS-IS-equal
        // value of `permanent.cardSources` is the one captured here, before the move. (The `TopCard != null` guard
        // is the entry guard above.)
        List<CardSource> leavingStack = permanent.cardSources;

        // AS-IS :531-543 remove the top off its field slot (frame null) — Move to None.
        ChoiceZone from = CurrentZoneOf(context, permanent.OwnerId, permanent.InstanceId);
        if (from is ChoiceZone.BattleArea or ChoiceZone.BreedingArea)
        {
            await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(permanent.OwnerId, permanent.InstanceId, from, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);

            // (R4 S3b-2② → RD-R3-02) the AS-IS `permanent.cardSources = new List<CardSource>()` (:553) — the AS-IS
            // Permanent object dies with the field slot — is enforced by the zone-mover lifetime chokepoint at the
            // field→None move above (InMemoryZoneMover.MoveCard), shared by EVERY field-leave path.
        }

        // AS-IS :547-550 cardSource.Init() over the whole leaving stack.
        foreach (CardSource cardSource in leavingStack)
        {
            cardSource.Init();
        }
    }

    #endregion

    #region add card to trash

    /// <summary>(R3-A) 1:1 of AS-IS <c>CardObjectController.AddTrashCard</c> (CardObjectController.cs:717-734): if the
    /// card is not already in the trash, withdraw it from wherever it is and (for a non-token) turn it face-up and
    /// insert it at the top of the trash. AS-IS SetFace() = face stamp; the withdraw+insert = one AddToTrashAsync
    /// (remove-from-all-zones + insert). AS-IS :732 <c>cardSource.Init()</c> is called at its AS-IS position.</summary>
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

                // AS-IS :732 cardSource.Init().
                cardSource.Init();
            }
        }
    }

    #endregion

    #region add card to security

    /// <summary>(R4 S3b-2③) 1:1 of AS-IS <c>CardObjectController.AddExecutingCard</c>
    /// (CardObjectController.cs:957-972): skip when already executing; withdraw from everywhere; a non-token
    /// goes face-up onto the owner's EXECUTING pile (AS-IS <c>Owner.ExecutingCards.Insert(0, …)</c> → the
    /// substrate Execution zone; SetFace = the move's face stamp; AS-IS :969 <c>Init()</c> is called at its AS-IS
    /// position). The option executor (UseOptionClass) parks the resolving option here.</summary>
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

            // AS-IS :969 cardSource.Init().
            cardSource.Init();
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

        // AS-IS :627 cardSource.Init() — the FIRST statement of AddHandCard, before SetFace and unguarded.
        cardSource.Init();

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

    #region put a card at the top of the deck

    /// <summary>1:1 of AS-IS <c>CardObjectController.AddLibraryTopCards</c> (CardObjectController.cs:781-859): move a
    /// list of cards to the TOP of their owners' decks. Control flow is AS-IS VERBATIM — if ANY card came from trash
    /// open the "[When a card is returned from the trash to the deck]" (OnReturnCardsToLibraryFromTrash) window with
    /// {"CardSources", cardSources}; charge the cards' ACE overflow FIRST (AS-IS :787 orders Overflow BEFORE the
    /// window); then per card: withdraw from all areas, face it up (AS-IS <c>SetFace()</c>), and (non-token) insert a
    /// non-DigiEgg at the deck top / a DigiEgg into the digitama library. SUBSTRATE: <c>Owner.LibraryCards.Insert(0,x)</c>
    /// → <see cref="IZoneMover.MoveToDeckTopAsync"/> (Library, insertTop); <c>Owner.DigitamaLibraryCards.Insert(0,x)</c>
    /// → a Move→DigitamaLibrary (the established egg-routing seam AddSecurityCard uses) — NOTE the DigitamaLibrary
    /// TOP-insert order is not expressible on the current zone mover (append), a substrate limitation for the
    /// egg-to-deck-top branch. AS-IS isFromHand DeleteHandCardEffect wait (:810-813) = UI; SetFace = the
    /// <see cref="SecurityFaceState"/> stamp; AS-IS :835 <c>Init()</c> is called at its AS-IS position; Log = stripped.</summary>
    public static async Task AddLibraryTopCards(List<CardSource> cardSources, bool notAddLog = false, CancellationToken cancellationToken = default)
    {
        _ = notAddLog; // AS-IS gates only the (stripped) PlayLog append.
        if (cardSources.Count <= 0)
        {
            return;
        }

        EngineContext context = cardSources[0].Context;

        // AS-IS :785 whether any subject currently sits in trash (drives the return-from-trash window).
        bool isFromTrash = cardSources.Any(cardSource => CardEffectCommons.CardEffectCommons.IsExistOnTrash(cardSource));

        // AS-IS :787 the cards' ACE overflow (charged BEFORE the trash-return window, AS-IS order).
        await new AceOverflowClass(cardSources).Overflow(cancellationToken).ConfigureAwait(false);

        if (isFromTrash)
        {
            // AS-IS :789-801 StackSkillInfos(OnReturnCardsToLibraryFromTrash) with {"CardSources", cardSources}.
            var hashtable = new System.Collections.Hashtable { { "CardSources", cardSources } };
            await GManager.instance.autoProcessing.StackSkillInfos(hashtable, EffectTiming.OnReturnCardsToLibraryFromTrash).ConfigureAwait(false);
        }

        // AS-IS :804-837 per card: withdraw, face up, insert at the deck top (DigiEgg → digitama library).
        foreach (CardSource cardSource in cardSources)
        {
            await RemoveFromAllArea(cardSource, cancellationToken).ConfigureAwait(false);

            // AS-IS :810-813 isFromHand DeleteHandCardEffect wait = UI (stripped).

            if (!cardSource.IsToken)
            {
                // AS-IS :817 SetFace() — the card is turned face up.
                SecurityFaceState.Stamp(context.CardInstanceRepository, cardSource.InstanceId, faceUp: true);

                if (!cardSource.IsDigiEgg)
                {
                    // AS-IS :821-824 Owner.LibraryCards.Insert(0, cardSource).
                    await context.ZoneMover.MoveToDeckTopAsync(cardSource.Owner, cardSource.InstanceId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // AS-IS :829-832 Owner.DigitamaLibraryCards.Insert(0, cardSource) — egg-routing seam (append; see summary).
                    await context.ZoneMover.MoveAsync(
                        new ZoneMoveRequest(cardSource.Owner, cardSource.InstanceId, ChoiceZone.None, ChoiceZone.DigitamaLibrary),
                        cancellationToken).ConfigureAwait(false);
                }

                // AS-IS :835 cardSource.Init().
                cardSource.Init();
            }
        }

        // AS-IS :839-858 Log = PlayLog (stripped).
    }

    #endregion

    #region put a card at the bottom of the deck

    /// <summary>1:1 of AS-IS <c>CardObjectController.AddLibraryBottomCards</c> (CardObjectController.cs:863-953): the
    /// deck-BOTTOM sibling of <see cref="AddLibraryTopCards"/>. Control flow is AS-IS VERBATIM — charge ACE overflow
    /// first; open the trash-return window if any came from trash; a pre-loop (AS-IS :886-896) discards the
    /// RemoveDigivolveRoot VFX for any card still sitting as a digivolution source under a permanent (= UI, stripped);
    /// then per card (over a CLONE, AS-IS :898): withdraw, face up, insert a non-DigiEgg at the deck bottom
    /// (<c>LibraryCards.Add</c> → <see cref="IZoneMover.MoveToDeckBottomAsync"/>) / a DigiEgg into the digitama library
    /// (<c>DigitamaLibraryCards.Add</c> → Move→DigitamaLibrary, order-exact for the bottom insert).</summary>
    public static async Task AddLibraryBottomCards(List<CardSource> cardSources, bool notAddLog = false, CancellationToken cancellationToken = default)
    {
        _ = notAddLog; // AS-IS gates only the (stripped) PlayLog append.
        if (cardSources.Count <= 0)
        {
            return;
        }

        EngineContext context = cardSources[0].Context;

        // AS-IS :867 whether any subject currently sits in trash (drives the return-from-trash window).
        bool isFromTrash = cardSources.Any(cardSource => CardEffectCommons.CardEffectCommons.IsExistOnTrash(cardSource));

        // AS-IS :869 the cards' ACE overflow (charged BEFORE the trash-return window, AS-IS order).
        await new AceOverflowClass(cardSources).Overflow(cancellationToken).ConfigureAwait(false);

        if (isFromTrash)
        {
            // AS-IS :871-883 StackSkillInfos(OnReturnCardsToLibraryFromTrash) with {"CardSources", cardSources}.
            var hashtable = new System.Collections.Hashtable { { "CardSources", cardSources } };
            await GManager.instance.autoProcessing.StackSkillInfos(hashtable, EffectTiming.OnReturnCardsToLibraryFromTrash).ConfigureAwait(false);
        }

        // AS-IS :886-896 RemoveDigivolveRootEffect for a card still buried as a source under a permanent = UI (stripped).

        // AS-IS :898-931 per card (over a clone): withdraw, face up, insert at the deck bottom (DigiEgg → digitama library).
        foreach (CardSource cardSource in cardSources.ToList())
        {
            await RemoveFromAllArea(cardSource, cancellationToken).ConfigureAwait(false);

            // AS-IS :904-907 isFromHand DeleteHandCardEffect wait = UI (stripped).

            if (!cardSource.IsToken)
            {
                // AS-IS :911 SetFace() — the card is turned face up.
                SecurityFaceState.Stamp(context.CardInstanceRepository, cardSource.InstanceId, faceUp: true);

                if (!cardSource.IsDigiEgg)
                {
                    // AS-IS :915-918 Owner.LibraryCards.Add(cardSource).
                    await context.ZoneMover.MoveToDeckBottomAsync(cardSource.Owner, cardSource.InstanceId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // AS-IS :923-926 Owner.DigitamaLibraryCards.Add(cardSource) — egg-routing seam (bottom insert, order-exact).
                    await context.ZoneMover.MoveAsync(
                        new ZoneMoveRequest(cardSource.Owner, cardSource.InstanceId, ChoiceZone.None, ChoiceZone.DigitamaLibrary),
                        cancellationToken).ConfigureAwait(false);
                }

                // AS-IS :929 cardSource.Init().
                cardSource.Init();
            }
        }

        // AS-IS :933-952 Log = PlayLog (stripped).
    }

    #endregion

    #region Shuffle

    /// <summary>1:1 of AS-IS <c>CardObjectController.Shuffle</c> (CardObjectController.cs:1010-1018): shuffles the
    /// player's LIBRARY in place. The AS-IS body is three statements — <c>PlaySE(ShuffleSE)</c> (UI, stripped),
    /// <c>player.LibraryCards = RandomUtility.ShuffledDeckCards(player.LibraryCards)</c>, and
    /// <c>player.ShuffleAnimation()</c> (UI, stripped). The single rule-relevant statement is the library
    /// reorder: the AS-IS whole-list REASSIGNMENT is not expressible on the mirror's read-only zone views, so it
    /// delegates to the authoritative substrate seam <see cref="IZoneMover.ShuffleAsync"/> (Library-scoped, driven
    /// by the seeded deterministic RNG — the same seam the AS-IS <c>RandomUtility.ShuffledDeckCards</c> maps to for
    /// the Security/Hand siblings, cf. ShuffleSecurityAsync in BT1_087 / ShuffleHandAsync in BT13_033).</summary>
    public static async Task Shuffle(Player player, CancellationToken cancellationToken = default)
    {
        // AS-IS :1012 PlaySE(ShuffleSE) = UI (stripped).
        // AS-IS :1014 player.LibraryCards = RandomUtility.ShuffledDeckCards(player.LibraryCards).
        await player.Context.ZoneMover
            .ShuffleAsync(player.PlayerId, cancellationToken)
            .ConfigureAwait(false);
        // AS-IS :1016 player.ShuffleAnimation() = UI (stripped).
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
