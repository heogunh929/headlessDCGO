using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (faceup security) AS-IS scans `player.SecurityCards where !IsFlipped` as a continuous-effect SOURCE population
// alongside the field permanents in every getter (DP / immunity / battle-deletion). This verifies a FACE-UP
// security card carrying a player-scope "your Digimon get +2000 DP" static is folded into a FIELD Digimon's DP
// (via the shared ContinuousScopeEvaluation that every getter uses), gated on the substrate face-up state:
// face-down / not-in-security contributes nothing.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

async Task<(EngineContext ctx, HeadlessEntityId field, HeadlessEntityId oppField, HeadlessEntityId sec)> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 71);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (P7 test-fix) CanTrigger/CanUse gate on DoneStartGame (mirror proxy: phase past None/Setup).
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    var cards = (CardDatabase)ctx.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("DIGI"), "DIGI", "Digi",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
    // The face-up security source: CardNumber picks the dispatch fixture with a player-scope +2000 DP static.
    cards.Upsert(new CardRecord(new HeadlessEntityId("BUFF"), "TfxSecurityDpBuff", "Buff",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));

    var field = new HeadlessEntityId("p1:FIELD");
    var oppField = new HeadlessEntityId("p2:FIELD");
    var sec = new HeadlessEntityId("p1:SEC");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(field, new HeadlessEntityId("DIGI"), P1));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(oppField, new HeadlessEntityId("DIGI"), P2));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(sec, new HeadlessEntityId("BUFF"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, field, ChoiceZone.None, ChoiceZone.BattleArea));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, oppField, ChoiceZone.None, ChoiceZone.BattleArea));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, sec, ChoiceZone.None, ChoiceZone.Security));
    return (ctx, field, oppField, sec);
}

int Dp(EngineContext ctx, HeadlessEntityId id) => ContinuousDpGate.ResolveDp(ctx, id, baseDp: 3000);

// --- 1. Face-up security source folds into the owner's field Digimon DP (uniform continuous-source scan). ---
{
    var (ctx, field, oppField, sec) = await Setup();
    SecurityFaceState.Stamp(ctx.CardInstanceRepository, sec, faceUp: true);

    Check(SecurityFaceState.IsFaceUpInSecurity(ctx, sec), "substrate: stamped security card reads face-up in security");
    Check(SecurityFaceState.FaceUpSecurityCards(ctx, P1).Contains(sec), "substrate: enumerated among P1 face-up security");
    Check(Dp(ctx, field) == 5000, "face-up security source grants +2000 DP to the owner's field Digimon (3000+2000)");
    Check(Dp(ctx, oppField) == 3000, "the P1-scoped buff does NOT reach the opponent's field Digimon");
}

// --- 2. Face-DOWN security card contributes nothing (AS-IS !IsFlipped gate). ---
{
    var (ctx, field, _, sec) = await Setup();
    SecurityFaceState.Stamp(ctx.CardInstanceRepository, sec, faceUp: false);
    Check(!SecurityFaceState.IsFaceUpInSecurity(ctx, sec), "substrate: face-down security card is not face-up");
    Check(Dp(ctx, field) == 3000, "a FACE-DOWN security source grants no DP (3000)");
}

// --- 3. An unstamped security card defaults to face-down (AS-IS security default). ---
{
    var (ctx, field, _, _) = await Setup();
    Check(Dp(ctx, field) == 3000, "an unstamped (default face-down) security source grants no DP (3000)");
}

// --- 4. Once the card leaves the Security zone the source is gone even if the flag lingers (zone-gated). ---
{
    var (ctx, field, _, sec) = await Setup();
    SecurityFaceState.Stamp(ctx.CardInstanceRepository, sec, faceUp: true);
    Check(Dp(ctx, field) == 5000, "precondition: face-up source active while in security");
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, sec, ChoiceZone.Security, ChoiceZone.Trash));
    Check(!SecurityFaceState.IsFaceUpInSecurity(ctx, sec), "substrate: a card that left security is not face-up-security");
    Check(Dp(ctx, field) == 3000, "a source that left the security zone no longer contributes (3000)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall face-up security source checks passed.");
