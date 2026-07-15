// (C-Del 3c-2b conversion) The retired invented gate enumerator FindScapegoatSacrificeCandidates and the
// invented `timing.IsPreAwaiting` keyword-offer were a headless substitute for two AS-IS Scapegoat rules that
// now live in their AS-IS homes:
//   (1) the sacrifice candidate is an owner-battle-area DIGIMON that isn't the holder — AS-IS Scapegoat.cs:53
//       CanSelectPermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
//       permanent != PermanentOfThisCard (mirrored verbatim in CardEffectFactory.ScapegoatEffect:62), and
//   (2) Scapegoat does NOT trigger when the deletion was caused by the owner's OWN effect — AS-IS Scapegoat.cs
//       CanUseCondition = ... && !IsByEffect(hashtable, IsOwnerEffect) (mirrored in ScapegoatEffect:73-92).
// These tests exercise those AS-IS rules directly (the candidate predicate over a Digimon/Tamer field, and the
// printed ScapegoatSelfEffect ActivateClass's CanUseCondition over a would-remove-field Hashtable with / without
// an own-effect cause) — no retired gate/timing symbol is referenced.

using System.Collections;
using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

async Task<(EngineContext ctx, HeadlessEntityId holder, HeadlessEntityId digimonAlly, HeadlessEntityId tamerAlly)> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 53);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DIGI"), "DIGI", "Digi",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("TAMER"), "TAMER", "Tamer",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));

    var holder = new HeadlessEntityId("p1:HOLDER");
    var digimonAlly = new HeadlessEntityId("p1:DIGI");
    var tamerAlly = new HeadlessEntityId("p1:TAMER");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(holder, new HeadlessEntityId("DIGI"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4, ["isSuspended"] = false }));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(digimonAlly, new HeadlessEntityId("DIGI"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4, ["isSuspended"] = false }));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(tamerAlly, new HeadlessEntityId("TAMER"), P1));
    foreach (var id in new[] { holder, digimonAlly, tamerAlly })
    {
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    }
    return (ctx, holder, digimonAlly, tamerAlly);
}

Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1);
CardSource Card(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1, P1);

// --- 1. AS-IS candidate rule: Digimon ally qualifies, Tamer ally / the holder do not. ---
{
    var (ctx, holder, digimonAlly, tamerAlly) = await Setup();
    using var scope = AmbientMatchContext.Enter(ctx);
    CardSource holderCard = Card(ctx, holder);

    // Verbatim AS-IS CanSelectPermanentCondition (CardEffectFactory.ScapegoatEffect:62).
    Func<Permanent, bool> canSelect = p =>
        CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(p, holderCard) && p.InstanceId != holder;

    Check(canSelect(Perm(ctx, digimonAlly)), "a Digimon ally is a valid Scapegoat sacrifice");
    Check(!canSelect(Perm(ctx, tamerAlly)), "a Tamer ally is NOT a valid Scapegoat sacrifice (Digimon-only)");
    Check(!canSelect(Perm(ctx, holder)), "the holder itself is never a sacrifice candidate");
    Check(CardEffectCommons.MatchConditionPermanentCount(holderCard, canSelect) == 1,
        "exactly the one Digimon ally qualifies");
}

// --- 2. Own-effect gate: Scapegoat's printed CanUseCondition offers for a non-own-effect (opponent/battle)
//        would-be-deletion, and is SUPPRESSED when the deletion was caused by the owner's own effect. ---
{
    // A would-remove-field Hashtable naming the holder's permanent as the leaving permanent, with an optional
    // causing effect. IsByEffect(hashtable, IsOwnerEffect) reads the "CardEffect" entry (AS-IS OnDeletion.cs:111).
    Hashtable RemovalHashtable(EngineContext ctx, HeadlessEntityId holder, ICardEffect? cause) =>
        CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(
            new List<Permanent> { Perm(ctx, holder) }, cardEffect: cause!, battle: null!);

    var (ctx1, holder1, _, _) = await Setup();
    using (AmbientMatchContext.Enter(ctx1))
    {
        var scapegoat = CardEffectFactory.ScapegoatSelfEffect(false, Card(ctx1, holder1), null, "Scapegoat", "Scapegoat");
        Check(scapegoat.CanUseCondition(RemovalHashtable(ctx1, holder1, cause: null)),
            "Scapegoat is offered when NOT deleted by the owner's own effect");
    }

    var (ctx2, holder2, _, _) = await Setup();
    using (AmbientMatchContext.Enter(ctx2))
    {
        var scapegoat = CardEffectFactory.ScapegoatSelfEffect(false, Card(ctx2, holder2), null, "Scapegoat", "Scapegoat");
        // An effect whose EffectSourceCard is owned by the holder's own player -> IsOwnerEffect true -> suppressed.
        ICardEffect ownEffect = CardEffectFactory.ScapegoatSelfEffect(false, Card(ctx2, holder2), null, "cause", "cause");
        Check(!scapegoat.CanUseCondition(RemovalHashtable(ctx2, holder2, cause: ownEffect)),
            "Scapegoat is SUPPRESSED when deleted by the owner's own effect");
    }
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-5 Scapegoat-guard checks passed.");
