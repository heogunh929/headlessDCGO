using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// E-3 witness (AS-IS CardSource.CanNotPlayThisOption, CardSource.cs:184-248). An active ICanNotPlayCardEffect
// forbids an Option's play. The headless port centralises the AS-IS 3-region scan in CanNotPlayOptionScan and
// wires it into the option-play legality gate (OptionActivateAction.Validate).
//   * BT8_057 [None] — a FIELD static (region ②): while all the owner's Digimon are suspended on the opponent's
//     turn, the opponent can't use Option cards.
//   * EX1_072 [Main]/[Security] — a PLAYER-bucket duration-bound grant (region ①) registered via
//     AddEffectToPlayer: [Main] UntilOpponentTurnEnd, [Security] UntilEachTurnEnd + add-to-hand.
//   * A self-forbidding option (region ③): the option's OWN [None] effect, scanned dispatch-first while it is
//     not a permanent.

HeadlessPlayerId P1 = new(1); // BT8_057 / EX1_072 owner (the "caster")
HeadlessPlayerId P2 = new(2); // the opponent whose Options are forbidden

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

EngineContext NewCtx(HeadlessPlayerId turnPlayer, int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    // (R3-W3c-4) the live CanNotPlayThisOption scan evaluates each granting effect's CanUse -> CanActivate ->
    // IsDisabled (GManager.instance) and CanTrigger (DoneStartGame = phase past Setup). Provide both, exactly as
    // production's game loop does (the C3-Witness / G9-057 precedent). Cases that re-set the phase (the [Your Turn]
    // unsuspend case) still do so explicitly below.
    if (ctx.TurnController.Current.Phase is HeadlessPhase.None)
    {
        ctx.TurnController.SetPhase(HeadlessPhase.Main);
    }

    HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Enter(ctx);
    return ctx;
}

// Place a Digimon whose effect class is `cardNumber` on `owner`'s battle area, suspended or not, and register
// its (auto) continuous effects. Returns the instance id.
HeadlessEntityId PlaceDigimon(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, bool suspended, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{cardNumber}:{tag}");
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{cardNumber}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = suspended }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    ctx.RegisterEnteredCardEffects(id, owner);
    return id;
}

// Place an Option (colorless, cost 0) in `owner`'s hand. `cardNumber` optionally dispatches an effect class.
HeadlessEntityId PlaceOption(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{cardNumber}:{tag}");
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var id = new HeadlessEntityId($"{owner.Value}:{cardNumber}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>()));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

// Place a plain (effectless) card of `cardType` into `owner`'s `zone`. Returns the instance id.
HeadlessEntityId PlaceInZone(EngineContext ctx, HeadlessPlayerId owner, string cardType, ChoiceZone zone, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:PLAIN:{tag}");
    cards.Upsert(new CardRecord(defId, "PLAIN", "PLAIN",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:PLAIN:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>()));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    return id;
}

int SecurityCount(EngineContext ctx, HeadlessPlayerId owner) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Security).Count;

bool ReadSuspended(EngineContext ctx, string instanceId) =>
    ctx.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(instanceId), out CardInstanceRecord? rec)
    && rec is not null
    && rec.Metadata.TryGetValue("isSuspended", out object? raw)
    && raw is bool suspended
    && suspended;

// =========================================================================================================
// (1) BT8_057 [None] FIELD static — region ②
// =========================================================================================================

// (1a) all conditions met: P1 has BT8_057 (suspended) on field, it's P2's turn -> P2's Option is BLOCKED.
{
    EngineContext ctx = NewCtx(turnPlayer: P2, seed: 8571);
    PlaceDigimon(ctx, P1, "BT8_057", suspended: true, "host");
    HeadlessEntityId p2opt = PlaceOption(ctx, P2, "P2OPT", "a");
    Check(CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt),
        "BT8_057: P2 Option blocked when all P1 Digimon suspended on P2's turn");
}

// (1b) a SECOND P1 Digimon is unsuspended -> not all suspended -> CanUse fails -> ALLOWED.
{
    EngineContext ctx = NewCtx(turnPlayer: P2, seed: 8572);
    PlaceDigimon(ctx, P1, "BT8_057", suspended: true, "host");
    PlaceDigimon(ctx, P1, "P1VANILLA", suspended: false, "other"); // vanilla, no effect class -> just a Digimon
    HeadlessEntityId p2opt = PlaceOption(ctx, P2, "P2OPT", "b");
    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt),
        "BT8_057: P2 Option ALLOWED when one of P1's Digimon is unsuspended");
}

// (1c) it is P1's OWN turn (not the opponent's) -> CanUse fails -> ALLOWED.
{
    EngineContext ctx = NewCtx(turnPlayer: P1, seed: 8573);
    PlaceDigimon(ctx, P1, "BT8_057", suspended: true, "host");
    HeadlessEntityId p2opt = PlaceOption(ctx, P2, "P2OPT", "c");
    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt),
        "BT8_057: P2 Option ALLOWED on P1's own turn (CanUse requires opponent's turn)");
}

// (1d) the forbidden CardCondition is the OPPONENT's option only — a P1 (caster) option is NOT blocked.
{
    EngineContext ctx = NewCtx(turnPlayer: P2, seed: 8574);
    PlaceDigimon(ctx, P1, "BT8_057", suspended: true, "host");
    HeadlessEntityId p1opt = PlaceOption(ctx, P1, "P1OPT", "d");
    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P1, p1opt),
        "BT8_057: the CASTER's own Option is NOT blocked (CardCondition is opponent-only)");
}

// (1e) BT8_057 host leaves the battle area -> binding unregistered + CanUse lapses -> ALLOWED.
{
    EngineContext ctx = NewCtx(turnPlayer: P2, seed: 8575);
    HeadlessEntityId host = PlaceDigimon(ctx, P1, "BT8_057", suspended: true, "host");
    HeadlessEntityId p2opt = PlaceOption(ctx, P2, "P2OPT", "e");
    Check(CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt), "BT8_057: (pre) blocked while host on field");
    // Host leaves the field.
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.BattleArea, ChoiceZone.Trash)).GetAwaiter().GetResult();
    CardEffectRegistrar.UnregisterCard(ctx, host);
    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt), "BT8_057: ALLOWED once the host leaves the battle area");
}

// (1f) INTEGRATION: the wired OptionActivateAction legality gate excludes the P2 Option (control vs treatment).
{
    // Control: NO BT8_057 -> the P2 Option is a legal action.
    EngineContext control = NewCtx(turnPlayer: P2, seed: 8576);
    HeadlessEntityId ctlOpt = PlaceOption(control, P2, "P2OPT", "f");
    var action = new OptionActivateAction();
    bool Offers(LegalAction a, HeadlessEntityId opt) =>
        OptionActivateActionPayload.TryRead(a, out OptionActivateActionPayload? p, out _) && p is not null && p.CardId == opt;
    bool legalWithout = action.GetLegalActions(control, P2).Any(a => Offers(a, ctlOpt));

    // Treatment: WITH BT8_057 active -> the same P2 Option is excluded from the legal set.
    EngineContext treat = NewCtx(turnPlayer: P2, seed: 8577);
    PlaceDigimon(treat, P1, "BT8_057", suspended: true, "host");
    HeadlessEntityId trOpt = PlaceOption(treat, P2, "P2OPT", "f");
    bool legalWith = action.GetLegalActions(treat, P2).Any(a => Offers(a, trOpt));

    Check(legalWithout, "BT8_057 integration: control — the P2 Option IS legal with no forbidder");
    Check(!legalWith, "BT8_057 integration: treatment — the P2 Option is EXCLUDED from the legal set");
}

// =========================================================================================================
// (2) EX1_072 [Main] PLAYER-bucket grant — region ①, UntilOpponentTurnEnd
// =========================================================================================================
{
    EngineContext ctx = NewCtx(turnPlayer: P1, seed: 10721);
    // EX1_072 belongs to P1; drive its [Main] activated effect via the resolver.
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId("DEF:EX1_072");
    cards.Upsert(new CardRecord(defId, "EX1_072", "EX1_072", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var ex1 = new HeadlessEntityId("1:EX1_072:main");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(ex1, defId, P1, Metadata: new Dictionary<string, object?>()));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, ex1, ChoiceZone.None, ChoiceZone.Trash)).GetAwaiter().GetResult();

    HeadlessEntityId p2opt = PlaceOption(ctx, P2, "P2OPT", "main");

    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt), "EX1_072 [Main]: (pre) P2 Option allowed before activation");
    int resolved = ActivatedEffectResolver.ResolveAsync(ctx, ex1, P1, EffectTiming.OptionSkill).GetAwaiter().GetResult();
    Check(resolved > 0, "EX1_072 [Main]: activated effect resolved");
    Check(CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt), "EX1_072 [Main]: P2 Option BLOCKED after activation");

    // Does NOT expire at the CASTER's (P1) turn end — the grant lasts until the OPPONENT's turn end.
    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, endingTurnPlayerId: P1);
    Check(CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt), "EX1_072 [Main]: still blocked after the caster's own turn ends");

    // Expires at the OPPONENT's (P2) turn end.
    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, endingTurnPlayerId: P2);
    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt), "EX1_072 [Main]: released at the end of the opponent's next turn (UntilOpponentTurnEnd)");
}

// =========================================================================================================
// (3) EX1_072 [Security] PLAYER-bucket grant — region ①, UntilEachTurnEnd + add-to-hand
// =========================================================================================================
{
    // The [Security] resolves during the opponent's (P2's) turn; caster = P1 (security owner).
    EngineContext ctx = NewCtx(turnPlayer: P2, seed: 10722);
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId("DEF:EX1_072");
    cards.Upsert(new CardRecord(defId, "EX1_072", "EX1_072", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var ex1 = new HeadlessEntityId("1:EX1_072:sec");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(ex1, defId, P1, Metadata: new Dictionary<string, object?>()));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, ex1, ChoiceZone.None, ChoiceZone.Trash)).GetAwaiter().GetResult();

    HeadlessEntityId p2opt = PlaceOption(ctx, P2, "P2OPT", "sec");

    int resolved = ActivatedEffectResolver.ResolveAsync(ctx, ex1, P1, EffectTiming.SecuritySkill).GetAwaiter().GetResult();
    Check(resolved > 0, "EX1_072 [Security]: activated effect resolved");
    Check(CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt), "EX1_072 [Security]: P2 Option BLOCKED after activation");

    // "Then, add this card to its owner's hand." — EX1_072 moved to P1's hand.
    bool inHand = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Contains(ex1);
    Check(inHand, "EX1_072 [Security]: this card was added to its owner's hand");

    // (P2-1 isolation) "this turn" -> UntilEachTurnEnd expires at ANY turn end, INCLUDING the caster/controller's
    // (P1's) OWN turn end (EffectDurationExpiry: UntilEachTurnEnd => always true). A UntilOpponentTurnEnd grant
    // (controller P1) would SURVIVE P1's own turn end (as the [Main] test above proves) — so releasing here on
    // ExpireTurnEnd(P1) is CONSTANT-SENSITIVE: it fails if the [Security] duration were mistakenly the
    // opponent-turn-end constant. (The prior ExpireTurnEnd(P2) fired under BOTH constants and did not isolate.)
    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, endingTurnPlayerId: P1);
    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P2, p2opt),
        "EX1_072 [Security]: released at the CASTER's OWN turn end (UntilEachTurnEnd — a UntilOpponentTurnEnd grant would survive)");
}

// =========================================================================================================
// (4) self-region ③: an Option's OWN [None] CanNotPlay effect forbids its own play while in hand.
// =========================================================================================================
{
    EngineContext ctx = NewCtx(turnPlayer: P1, seed: 10723);
    HeadlessEntityId selfOpt = PlaceOption(ctx, P1, "TfxOptionForbidsSelf", "self");
    Check(CanNotPlayOptionScan.CanNotPlay(ctx, P1, selfOpt),
        "self-region ③: an Option with its OWN self-forbidding [None] effect cannot be played from hand");

    // A DIFFERENT vanilla option is unaffected (the self predicate matched only the granting card).
    HeadlessEntityId otherOpt = PlaceOption(ctx, P1, "P1OPT", "self-other");
    Check(!CanNotPlayOptionScan.CanNotPlay(ctx, P1, otherOpt),
        "self-region ③: a different Option is NOT forbidden by another card's self-only effect");
}

// =========================================================================================================
// (5) BT8_057 [Your Turn] (E3-P1-2) — OnUnTappedAnyone: on the natural unsuspend, trash opponent top security.
//     (4b B6 re-pin) The OLD HeadlessEarlyPhaseFlow drive is retired; the PUMP unsuspend seat is the mirror
//     TurnStateMachine.ActivePhaseAsync (:586-624 unsuspend -> IUnsuspendPermanents.Unsuspend fires the
//     OnUnTappedAnyone window, AS-IS CardController.cs:5754 -> AutoProcessCheck drains it in-body).
//     Baseline note: this witness was RED on the OLD drive (the phase-flow unsuspend bypassed the window
//     collection); the green under the pump seat is the OLD-artifact red resolving with the driver swap.
// =========================================================================================================

// (5a) treatment: P1's turn, BT8_057 suspended on P1 field, P2 has 2 security -> after the pump active-phase
//      body (natural unsuspend) the opponent's top security is trashed (2 -> 1).
{
    EngineContext ctx = NewCtx(turnPlayer: P1, seed: 8578);
    ctx.TurnController.SetPhase(HeadlessPhase.Active);
    PlaceDigimon(ctx, P1, "BT8_057", suspended: true, "yt-host");
    PlaceInZone(ctx, P2, "Digimon", ChoiceZone.Security, "yt-sec1");
    PlaceInZone(ctx, P2, "Digimon", ChoiceZone.Security, "yt-sec2");
    Check(SecurityCount(ctx, P2) == 2, "BT8_057 [Your Turn]: (pre) opponent has 2 security");

    TurnStateMachine.For(ctx).ActivePhaseAsync().GetAwaiter().GetResult();

    Check(!ReadSuspended(ctx, $"{P1.Value}:BT8_057:yt-host"),
        "BT8_057 [Your Turn]: the host unsuspended on the natural unsuspend (pump active-phase body)");
    Check(SecurityCount(ctx, P2) == 1,
        "BT8_057 [Your Turn]: opponent top security trashed on the natural unsuspend (2 -> 1)");
}

// (5b) control (own-turn gate): it is P2's turn, so P1's BT8_057 does NOT unsuspend (not the turn player) and
//      the [Your Turn] gate fails -> opponent security untouched.
{
    EngineContext ctx = NewCtx(turnPlayer: P2, seed: 8579);
    ctx.TurnController.SetPhase(HeadlessPhase.Active);
    PlaceDigimon(ctx, P1, "BT8_057", suspended: true, "yt-host2");
    PlaceInZone(ctx, P1, "Digimon", ChoiceZone.Security, "yt2-p1sec"); // P1 (caster) security untouched too
    PlaceInZone(ctx, P1, "Digimon", ChoiceZone.Security, "yt2-p1sec2");

    TurnStateMachine.For(ctx).ActivePhaseAsync().GetAwaiter().GetResult();

    Check(ReadSuspended(ctx, $"{P1.Value}:BT8_057:yt-host2"),
        "BT8_057 [Your Turn]: the host stays suspended on the opponent's turn (not the turn player, no Reboot)");
    Check(SecurityCount(ctx, P1) == 2,
        "BT8_057 [Your Turn]: does NOT fire on the opponent's turn (host does not unsuspend / gate requires owner's turn)");
}

// =========================================================================================================
// (6) P1-1: the EFFECT-DRIVEN option play path (PlayOptionCardEffect.BuildRequest) filters CanNotPlay options.
//     AS-IS CardEffectCommons.PlayOptionCards excludes `!CanNotPlayThisOption` candidates before the select.
// =========================================================================================================
{
    EngineContext ctx = NewCtx(turnPlayer: P1, seed: 8580);
    // The caster whose effect plays options from P1's hand.
    HeadlessEntityId casterId = PlaceDigimon(ctx, P1, "P1CASTER", suspended: false, "caster");
    var caster = new CardSource(ctx, casterId, P1, P1);

    HeadlessEntityId plainOpt = PlaceOption(ctx, P1, "P1OPT", "epd-plain");
    HeadlessEntityId selfForbidOpt = PlaceOption(ctx, P1, "TfxOptionForbidsSelf", "epd-forbid");

    var playEffect = new PlayOptionCardEffect(caster, ChoiceZone.Hand, _ => true, maxCount: 2, canEndNotMax: true,
        "Play up to 2 Options from your hand.");
    ChoiceRequest request = playEffect.BuildRequest(new[] { P1, P2 });
    var candidateIds = request.Candidates.Select(c => c.Id).ToList();

    Check(candidateIds.Contains(plainOpt),
        "PlayOptionCardEffect (P1-1): a normally-playable Option IS a candidate");
    Check(!candidateIds.Contains(selfForbidOpt),
        "PlayOptionCardEffect (P1-1): a self-forbidding Option (CanNotPlay active) is EXCLUDED from the effect-driven play");
}

// =========================================================================================================
// (7) P2-2: an add-to-hand effect resolves through the RESOLVER path (ResolveAsync), not just hand.Apply(sink)
//     — ST3_13 [Security] "add this card to its owner's hand" (one of the 9 cards the resolver case restored).
// =========================================================================================================
{
    EngineContext ctx = NewCtx(turnPlayer: P2, seed: 8581);
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId("DEF:ST3_13");
    cards.Upsert(new CardRecord(defId, "ST3_13", "ST3_13", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var st3 = new HeadlessEntityId("1:ST3_13:sec");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(st3, defId, P1, Metadata: new Dictionary<string, object?>()));
    // Starts in the Security zone (a resolving [Security] option), not the hand.
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, st3, ChoiceZone.None, ChoiceZone.Security)).GetAwaiter().GetResult();

    Check(!((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Contains(st3),
        "ST3_13 [Security] (P2-2): (pre) card is not yet in hand");
    int resolved = ActivatedEffectResolver.ResolveAsync(ctx, st3, P1, EffectTiming.SecuritySkill).GetAwaiter().GetResult();
    Check(resolved > 0, "ST3_13 [Security] (P2-2): activated effects resolved via ResolveAsync");
    Check(((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Contains(st3),
        "ST3_13 [Security] (P2-2): add-this-card-to-hand restored through the RESOLVER path (AddThisCardToHandEffect case)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall E-3 CanNotPlayOption witness checks passed.");
