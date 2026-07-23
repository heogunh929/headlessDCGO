using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Scr = HeadlessDCGO.Engine.Assets.Scripts.Script;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using Permanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;
using ICardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.ICardEffect;
using ActivateClass = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// LATENT-Close — behavior witnesses for the portable latent-STOP close-out (RD-3A-02 / MIG4-DETACH-LIVE-TOP).
//
//   * EX8_059 self-[On Deletion] grant (RD-3A-02 retired): the granted reactor is stored on the TARGET's
//     UntilOpponentTurnEnd duration bucket at timing OnDestroyedAnyone via the plain AS-IS AddEffectToPermanent
//     bucket idiom, and the collect-BEFORE-removal OnDestroyedAnyone window surfaces it on the target's OWN
//     deletion (survive-own-leave). No invented AddSelfRemovalEffectToPermanent temp.
//   * BT7_058 live-top re-parent (MIG4-DETACH-LIVE-TOP): IPlacePermanentToDigivolutionCards folds ANOTHER
//     permanent's whole live top under this Digimon. RemoveField-first ordering makes the per-card detach
//     early-return (the source top is off-field, PermanentOfThisCard() empty) — no STOP; the top attaches 1:1.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT7_058 re-parent (top-only source, BT7_058 post-trash state): IPlacePermanentToDigivolutionCards folds SRC's live top under HOST as a bottom digivolution source; SRC leaves the battle area — no DetachEmbeddedSourceOrLinkAsync STOP", ReparentTopOnly),
    ("BT7_058 trash-then-fold (BT7_058 :112 + :115): a SRC with a buried source has its sources trashed, then its live top folds under HOST; the dissolution guard (!IsPermanentExistsOnBattleArea(SRC)) holds and HOST.DigivolutionCards contains the folded top", TrashThenFold),
    ("BT7_058 effect registration: CardEffects(OnAllyAttack) yields the [When Attacking] ActivateClass; CardEffects(None) yields the inherited +1 S.Attack static effect", Bt7058Registration),
    ("EX8_059 effect registration: CardEffects(None) yields the alt-digivolution requirement; OnEnterFieldAnyone yields the [On Play] grant; WhenDigivolving yields the [When Digivolving] grant; OnAllyAttack yields the ESS draw", Ex8059Registration),
    ("EX8_059 self-[On Deletion] grant (RD-3A-02): AddEffectToPermanent(target, UntilOpponentTurnEnd, card, reactor, OnDestroyedAnyone) lands the reactor in the target's OWN duration bucket, surfaced by EffectList_Added ONLY at OnDestroyedAnyone (timing-keyed) — the AS-IS bucket idiom carries the self-removal reactor, no invented temp", Ex8059GrantBucket),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st) Console.WriteLine(string.Join('\n', st.Split('\n').Take(14)));
    }
}
if (failed > 0) { Console.Error.WriteLine($"\n{failed} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task ReparentTopOnly()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST");
    HeadlessEntityId srcTop = PlaceDigimon(context, "SRC");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(context);

    var srcPerm = new Permanent(context, srcTop, P1);
    var hostPerm = new Permanent(context, host, P1);
    ActivateClass effect = Effect(context, host);

    await new Scr.IPlacePermanentToDigivolutionCards(
        new List<Permanent[]> { new[] { srcPerm, hostPerm } }, false, effect).PlacePermanentToDigivolutionCards();

    AssertTrue(SourceIds(context, host).Contains(srcTop), "SRC top folded under HOST as a digivolution source");
    AssertFalse(InZone(context, P1, ChoiceZone.BattleArea, srcTop), "SRC left the battle area");
    AssertFalse(Cec.CardEffectCommons.IsPermanentExistsOnBattleArea(srcPerm), "SRC no longer a battle-area permanent (dissolved)");
}

async Task TrashThenFold()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST");
    HeadlessEntityId srcTop = PlaceDigimon(context, "SRC");
    HeadlessEntityId buried = PlaceHand(context, "BURIED");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(context);

    var srcPerm = new Permanent(context, srcTop, P1);
    var hostPerm = new Permanent(context, host, P1);
    await srcPerm.AddDigivolutionCardsTop(new List<CardSource> { new(context, buried, P1, P1) }, causeEffectSourceId: null);
    AssertTrue(SourceIds(context, srcTop).Contains(buried), "precondition: BURIED is a digivolution source of SRC");

    ActivateClass effect = Effect(context, host);
    CardSource topCard = srcPerm.TopCard;

    // BT7_058 :110-113 — trash all trashable digivolution cards of SRC first.
    if (srcPerm.DigivolutionCards.Count(cs => !cs.CanNotTrashFromDigivolutionCards(effect)) >= 1)
    {
        await Cec.CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(
            targetPermanent: srcPerm, trashCount: srcPerm.DigivolutionCards.Count, isFromTop: true, activateClass: effect);
    }
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, buried), "BURIED source trashed");

    // BT7_058 :115 — fold SRC's live top under HOST (bottom).
    await new Scr.IPlacePermanentToDigivolutionCards(
        new List<Permanent[]> { new[] { srcPerm, hostPerm } }, false, effect).PlacePermanentToDigivolutionCards();

    // BT7_058 :117-119 dissolution guard, mirror form.
    AssertFalse(Cec.CardEffectCommons.IsPermanentExistsOnBattleArea(srcPerm), "SRC dissolved (mirror of TopCard == null)");
    AssertTrue(hostPerm.DigivolutionCards.Any(c => c.InstanceId == topCard.InstanceId), "HOST.DigivolutionCards contains the folded SRC top (BT7_058 :119)");
}

async Task Bt7058Registration()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "BT7HOST");
    var card = new CardSource(context, host, P1, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.Black.BT7_058();

    List<ICardEffect> onAttack = effect.CardEffects(Cec.EffectTiming.OnAllyAttack, card);
    AssertTrue(onAttack.Count == 1, "OnAllyAttack yields exactly 1 effect (the [When Attacking] ActivateClass)");

    List<ICardEffect> none = effect.CardEffects(Cec.EffectTiming.None, card);
    AssertTrue(none.Count == 1, "None yields exactly 1 effect (the inherited +1 S.Attack)");
    await Task.CompletedTask;
}

async Task Ex8059Registration()
{
    EngineContext context = Board();
    HeadlessEntityId self = PlaceDigimon(context, "EX8SELF");
    var card = new CardSource(context, self, P1, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Purple.EX8_059();

    AssertTrue(effect.CardEffects(Cec.EffectTiming.None, card).Count == 1, "None yields the alt-digivolution requirement");
    AssertTrue(effect.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card).Count == 1, "OnEnterFieldAnyone yields the [On Play] grant");
    AssertTrue(effect.CardEffects(Cec.EffectTiming.WhenDigivolving, card).Count == 1, "WhenDigivolving yields the [When Digivolving] grant");
    AssertTrue(effect.CardEffects(Cec.EffectTiming.OnAllyAttack, card).Count == 1, "OnAllyAttack yields the ESS draw");
    await Task.CompletedTask;
}

async Task Ex8059GrantBucket()
{
    EngineContext context = Board();
    HeadlessPlayerId P2 = new(2);
    // Target = an OPPONENT's Digimon (EX8_059 targets 1 of your opponent's Digimon); card = the owner (P1).
    HeadlessEntityId targetTop = PlaceDigimonOwned(context, "TARGET", P2);
    var card = new CardSource(context, PlaceDigimon(context, "EX8OWNER"), P1, P1);
    var target = new Permanent(context, targetTop, P2);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(context);

    // The granted reactor: EX8_059's inner activateClass1 "[On Deletion] Trash 1 card", sourced onto the target.
    var reactor = new ActivateClass();
    reactor.SetUpICardEffect("Trash 1 card", _ => true, target.TopCard);
    reactor.SetEffectSourcePermanent(target);

    // AS-IS EX8_059 :149/:324 — the bucket idiom (NOT the retired AddSelfRemovalEffectToPermanent temp).
    Cec.CardEffectCommons.AddEffectToPermanent(
        targetPermanent: target, effectDuration: EffectDuration.UntilOpponentTurnEnd, card: card,
        cardEffect: reactor, timing: Cec.EffectTiming.OnDestroyedAnyone);

    List<ICardEffect> atDeletion = target.EffectList_Added(Cec.EffectTiming.OnDestroyedAnyone);
    List<ICardEffect> atNone = target.EffectList_Added(Cec.EffectTiming.None);

    AssertTrue(atDeletion.Contains(reactor), "the granted [On Deletion] reactor is surfaced on the target's OWN bucket at OnDestroyedAnyone (survive-own-leave contract)");
    AssertFalse(atNone.Contains(reactor), "the reactor is timing-keyed — NOT surfaced at a non-deletion timing");
    await Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

ActivateClass Effect(EngineContext context, HeadlessEntityId sourceTop)
{
    var effect = new ActivateClass();
    effect.SetUpICardEffect("witness", _ => true, new CardSource(context, sourceTop, P1, P1));
    return effect;
}

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    return context;
}

HeadlessEntityId PlaceDigimon(EngineContext context, string tag)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = 4000 }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId PlaceDigimonOwned(EngineContext context, string tag, HeadlessPlayerId owner)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = 4000 }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId PlaceHand(EngineContext context, string tag)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

IReadOnlyList<HeadlessEntityId> SourceIds(EngineContext context, HeadlessEntityId host) =>
    new Permanent(context, host, P1).DigivolutionCards.Select(c => c.InstanceId).ToList();

bool InZone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
