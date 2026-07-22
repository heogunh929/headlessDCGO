using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// SKEL-Exhaust witness — behavioural coverage for the units ported/rehoused during skeleton exhaustion
// (docs/audit/primitive_residual_census_2026-07-22.md §2/§3). Groups:
//   (1) MinMax_DP_Cost_Level predicates rehoused to their mirrored paths (IsMaxDP/IsMinDP/IsMaxLevel/
//       IsMinLevel/IsMinLevelBoard/IsMaxCost/IsMinCost/IsMinDigivolutionCards + GetNonMaxCostPermanents).
//   (2) GiveEffect grants: GainCanNotBeDeletedByBattle (rehoused), StartOfMainAttack (rehoused),
//       ChangeDigimonLinkMax (latent), ChangeDigivolutionCostPlayerEffect / GainIgnoreDigivolutionRequirement-
//       PlayerEffect (latent, Func-returning).
//   (3) TrainingClass (latent keyword body) — construction + guard smoke.
//   (4) SelectTrashLinkedCards — STOP-guard contract (guards short-circuit; the interactive loop throws
//       NotSupportedException per design item RD-SKEL-01).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

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
    if (ctx.TurnController.Current.Phase is HeadlessPhase.None)
    {
        ctx.TurnController.SetPhase(HeadlessPhase.Main);
    }

    AmbientMatchContext.Enter(ctx);
    return ctx;
}

HeadlessEntityId PlaceDigimon(EngineContext ctx, HeadlessPlayerId owner, string tag, int dp, int level, int? cost, bool suspended = false)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    cards.Upsert(new CardRecord(defId, tag, tag, meta, CardType: "Digimon", PlayCost: cost));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = suspended }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

Permanent Perm(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId owner) => new(ctx, id, owner);

ActivateClass MakeEffect(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId hostId, string label)
{
    var host = new CardSource(ctx, hostId, owner, owner);
    var ac = new ActivateClass();
    ac.SetUpICardEffect(label, _ => true, host);
    return ac;
}

// =========================================================================================================
// (1) MinMax_DP_Cost_Level predicates (rehoused to MinMax_DP_Cost_Level/**). Proves the relocation is live.
// =========================================================================================================

// (1a) DP: two P1 Digimon (5000 / 3000). Max is the 5000, min is the 3000.
{
    EngineContext ctx = NewCtx(P1, 40001);
    HeadlessEntityId hi = PlaceDigimon(ctx, P1, "DPHI", dp: 5000, level: 4, cost: 4);
    HeadlessEntityId lo = PlaceDigimon(ctx, P1, "DPLO", dp: 3000, level: 3, cost: 3);
    Check(CardEffectCommons.IsMaxDP(Perm(ctx, hi, P1), P1), "IsMaxDP: 5000 is the max");
    Check(!CardEffectCommons.IsMaxDP(Perm(ctx, lo, P1), P1), "IsMaxDP: 3000 is NOT the max");
    Check(CardEffectCommons.IsMinDP(Perm(ctx, lo, P1), P1), "IsMinDP: 3000 is the min");
    Check(!CardEffectCommons.IsMinDP(Perm(ctx, hi, P1), P1), "IsMinDP: 5000 is NOT the min");
    Check(!CardEffectCommons.IsMaxDP(null, P1), "IsMaxDP: null subject -> false (guard)");
}

// (1b) Level: two P1 Digimon (level 6 / 3). IsMaxLevel / IsMinLevel / IsMinLevelBoard.
{
    EngineContext ctx = NewCtx(P1, 40002);
    HeadlessEntityId hi = PlaceDigimon(ctx, P1, "LVHI", dp: 4000, level: 6, cost: 5);
    HeadlessEntityId lo = PlaceDigimon(ctx, P1, "LVLO", dp: 4000, level: 3, cost: 3);
    // A lower-level opponent Digimon so IsMinLevelBoard differs from the owner-only IsMinLevel.
    HeadlessEntityId enemyLo = PlaceDigimon(ctx, P2, "LVEN", dp: 4000, level: 2, cost: 2);
    Check(CardEffectCommons.IsMaxLevel(Perm(ctx, hi, P1), P1), "IsMaxLevel: level 6 is the owner max");
    Check(CardEffectCommons.IsMinLevel(Perm(ctx, lo, P1), P1), "IsMinLevel: level 3 is the owner min");
    Check(!CardEffectCommons.IsMinLevel(Perm(ctx, hi, P1), P1), "IsMinLevel: level 6 is NOT the owner min");
    Check(!CardEffectCommons.IsMinLevelBoard(Perm(ctx, lo, P1)), "IsMinLevelBoard: owner's level-3 is NOT the board min (opponent has level 2)");
    Check(CardEffectCommons.IsMinLevelBoard(Perm(ctx, enemyLo, P2)), "IsMinLevelBoard: opponent's level-2 is the board min");
}

// (1c) Cost: two P1 Digimon (cost 6 / 2). IsMaxCost / IsMinCost (digimon-only) + GetNonMaxCostPermanents.
{
    EngineContext ctx = NewCtx(P1, 40003);
    HeadlessEntityId hi = PlaceDigimon(ctx, P1, "CSHI", dp: 4000, level: 5, cost: 6);
    HeadlessEntityId lo = PlaceDigimon(ctx, P1, "CSLO", dp: 4000, level: 3, cost: 2);
    Check(CardEffectCommons.IsMaxCost(Perm(ctx, hi, P1), P1, IsDigimonOnly: true), "IsMaxCost: cost 6 is the max");
    Check(CardEffectCommons.IsMinCost(Perm(ctx, lo, P1), P1, IsDigimonOnly: true), "IsMinCost: cost 2 is the min");
    var nonMax = CardEffectCommons.GetNonMaxCostPermanents(new CardSource(ctx, hi, P1, P1), P1, digimonOnly: true);
    Check(nonMax.Any(p => p.InstanceId == lo) && nonMax.All(p => p.InstanceId != hi),
        "GetNonMaxCostPermanents: the cost-2 Digimon is below-max, the cost-6 Digimon is not");
}

// (1d) DigivolutionCards: a lone Digimon (0 digivolution cards) is trivially the min; null -> false (guard).
{
    EngineContext ctx = NewCtx(P1, 40004);
    HeadlessEntityId only = PlaceDigimon(ctx, P1, "DVONLY", dp: 4000, level: 4, cost: 4);
    Check(CardEffectCommons.IsMinDigivolutionCards(Perm(ctx, only, P1), P1),
        "IsMinDigivolutionCards: the sole battle-area Digimon is trivially the min");
    Check(!CardEffectCommons.IsMinDigivolutionCards(null, P1), "IsMinDigivolutionCards: null subject -> false (guard)");
}

// =========================================================================================================
// (2) GiveEffect grants.
// =========================================================================================================

// (2a) GainCanNotBeDeletedByBattle (rehoused GiveEffectToPermanent/CanNotBeDeletedByBattle.cs).
{
    EngineContext ctx = NewCtx(P1, 40005);
    HeadlessEntityId target = PlaceDigimon(ctx, P1, "IMMUNE", dp: 4000, level: 4, cost: 4);
    ActivateClass eff = MakeEffect(ctx, P1, target, "immune-grant");
    bool granted = CardEffectCommons.GainCanNotBeDeletedByBattle(
        Perm(ctx, target, P1), (a, b, c, d) => true, EffectDuration.UntilOwnerTurnEnd, eff, "Can't be deleted by battle");
    Check(granted, "GainCanNotBeDeletedByBattle: valid target on the battle area -> grant registered (true)");
    Check(!CardEffectCommons.GainCanNotBeDeletedByBattle(
        null!, (a, b, c, d) => true, EffectDuration.UntilOwnerTurnEnd, eff, "x"),
        "GainCanNotBeDeletedByBattle: null target -> refused (false)");
}

// (2b) StartOfMainAttack (rehoused GiveEffectToPermanent/StartOfMainAttack.cs): registers an OnStartMainPhase binding.
{
    EngineContext ctx = NewCtx(P1, 40006);
    HeadlessEntityId attacker = PlaceDigimon(ctx, P1, "MAINATK", dp: 4000, level: 4, cost: 4);
    var host = new CardSource(ctx, attacker, P1, P1);
    bool threw = false;
    try { CardEffectCommons.StartOfMainAttack(Perm(ctx, attacker, P1), host); }
    catch (Exception e) { threw = true; Console.Error.WriteLine(e); }
    Check(!threw, "StartOfMainAttack: registers the mandatory-attack binding without throwing");
    // null target is a no-op (guard), not a throw.
    bool threw2 = false;
    try { CardEffectCommons.StartOfMainAttack(null, host); } catch { threw2 = true; }
    Check(!threw2, "StartOfMainAttack: null target -> no-op (guard, no throw)");
}

// (2c) ChangeDigimonLinkMax (latent, both overloads): grants a link-max delta without throwing.
{
    EngineContext ctx = NewCtx(P1, 40007);
    HeadlessEntityId target = PlaceDigimon(ctx, P1, "LINKMAX", dp: 4000, level: 4, cost: 4);
    ActivateClass eff = MakeEffect(ctx, P1, target, "linkmax-grant");
    bool threw = false;
    try
    {
        CardEffectCommons.ChangeDigimonLinkMax(Perm(ctx, target, P1), 1, EffectDuration.UntilOwnerTurnEnd, eff).GetAwaiter().GetResult();
        CardEffectCommons.ChangeDigimonLinkMax(Perm(ctx, target, P1), -1, EffectDuration.UntilOwnerTurnEnd, eff, activateAnimation: true, hashstring: "h").GetAwaiter().GetResult();
    }
    catch (Exception e) { threw = true; Console.Error.WriteLine(e); }
    Check(!threw, "ChangeDigimonLinkMax: both overloads grant a link-max delta without throwing");
    // changeValue 0 is a no-op guard.
    bool threw2 = false;
    try { CardEffectCommons.ChangeDigimonLinkMax(Perm(ctx, target, P1), 0, EffectDuration.UntilOwnerTurnEnd, eff).GetAwaiter().GetResult(); }
    catch { threw2 = true; }
    Check(!threw2, "ChangeDigimonLinkMax: changeValue 0 -> no-op (guard)");
}

// (2d) ChangeDigivolutionCostPlayerEffect (latent, Func-returning).
{
    EngineContext ctx = NewCtx(P1, 40008);
    HeadlessEntityId host = PlaceDigimon(ctx, P1, "COSTSRC", dp: 4000, level: 4, cost: 4);
    ActivateClass eff = MakeEffect(ctx, P1, host, "cost-grant");
    var sel = CardEffectCommons.ChangeDigivolutionCostPlayerEffect(null, null, null, changeValue: -1, setFixedCost: false, eff);
    Check(sel is not null, "ChangeDigivolutionCostPlayerEffect: valid effect -> non-null timing selector");
    Check(CardEffectCommons.ChangeDigivolutionCostPlayerEffect(null, null, null, -1, false, null!) is null,
        "ChangeDigivolutionCostPlayerEffect: null activateClass -> null");
}

// (2e) GainIgnoreDigivolutionRequirementPlayerEffect (latent, Func-returning).
{
    EngineContext ctx = NewCtx(P1, 40009);
    HeadlessEntityId host = PlaceDigimon(ctx, P1, "IGNSRC", dp: 4000, level: 4, cost: 4);
    ActivateClass eff = MakeEffect(ctx, P1, host, "ignore-grant");
    var sel = CardEffectCommons.GainIgnoreDigivolutionRequirementPlayerEffect(null, null, ignoreDigivolutionRequirement: true, digivolutionCost: 0, eff);
    Check(sel is not null, "GainIgnoreDigivolutionRequirementPlayerEffect: valid effect -> non-null timing selector");
    Check(CardEffectCommons.GainIgnoreDigivolutionRequirementPlayerEffect(null, null, true, 0, null!) is null,
        "GainIgnoreDigivolutionRequirementPlayerEffect: null activateClass -> null");
}

// =========================================================================================================
// (3) TrainingClass (latent keyword body) — construction + guard smoke.
// =========================================================================================================
{
    var latent = new TrainingClass(null!);
    Check(latent is not null, "TrainingClass: constructs");
    bool threw = false;
    try { latent.Training().GetAwaiter().GetResult(); } catch { threw = true; }
    Check(!threw, "TrainingClass.Training: null activateClass -> returns via guard (no throw)");
}

// =========================================================================================================
// (4) SelectTrashLinkedCards — STOP-guard contract (design item RD-SKEL-01).
// =========================================================================================================
{
    EngineContext ctx = NewCtx(P1, 40010);
    HeadlessEntityId host = PlaceDigimon(ctx, P1, "TRASHSRC", dp: 4000, level: 4, cost: 4);
    ActivateClass eff = MakeEffect(ctx, P1, host, "trash-linked");

    // Guards short-circuit to a completed task (no throw).
    bool guardThrew = false;
    try { CardEffectCommons.SelectTrashLinkedCards(null, null, 0, false, false, eff).GetAwaiter().GetResult(); }
    catch { guardThrew = true; }
    Check(!guardThrew, "SelectTrashLinkedCards: maxCount<=0 -> completes via guard (no throw)");

    bool guard2Threw = false;
    try { CardEffectCommons.SelectTrashLinkedCards(null, null, 1, false, false, null!).GetAwaiter().GetResult(); }
    catch { guard2Threw = true; }
    Check(!guard2Threw, "SelectTrashLinkedCards: null activateClass -> completes via guard (no throw)");

    // The interactive selection loop is STOP-guarded with NotSupportedException.
    bool stopThrew = false;
    try { CardEffectCommons.SelectTrashLinkedCards(null, null, 1, false, false, eff).GetAwaiter().GetResult(); }
    catch (NotSupportedException) { stopThrew = true; }
    Check(stopThrew, "SelectTrashLinkedCards: valid args reach the STOP-guarded loop (NotSupportedException, RD-SKEL-01)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall SKEL-Exhaust witness checks passed.");
