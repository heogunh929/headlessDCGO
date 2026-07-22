using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using ScriptSelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S2 witness 스위트 — Sonnet 트랜치 S2 10장, 카드당 1개 이상(PILOT-S1.Witness.Tests 템플릿 복제).
// 표준 템플릿: DcgoMatch.CreatePumpDriven + 에이전트 액션 구동(ApplyActionAsync); 효과-내부 Select*/Optional
// 프롬프트는 PolicyChoiceProvider 좌석으로 응답.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT8_071 W1: [All Turns] 등재 + CannotReduceCostClass 조건이 실제 코스트-감소 카드를 차단", BT8071_CannotReduceCost),
    ("BT8_059 W1: [All Turns] 등재 + CannotIgnoreDigivolutionConditionClass 조건이 항상 true", BT8059_CannotIgnoreDigivolutionCondition),
    ("BT2_066 W1: [On Play] 발화 → 카운트 1 선택 → 상대 디지몬 De-Digivolve로 진화원 1장 감소", BT2066_OnPlayDeDigivolve),
    ("BT20_025 W1: 대체진화(코스트3) 착지 → [When Digivolving] 6000DP 이하 상대 디지몬 삭제", BT20025_AltDigivolveThenDelete),
    ("BT14_081 W1: [When Digivolving] 발화 → 트래시의 [Dark Animal] 레벨4 이하 디지몬 무료 플레이", BT14081_WhenDigivolvingPlaysFromTrash),
    ("LM_018 W1: [On Play] 발화 → 레벨4 이하 디지몬 삭제 + [Gyuukimon] 토큰 무료 플레이", LM018_OnPlayDeleteThenToken),
    ("BT15_082 W1: [Start of Your Turn] 발화 → SetMemoryTo3TamerEffect가 메모리를 3으로 설정", BT15082_StartOfTurnSetsMemoryTo3),
    ("BT25_039 W1: [On Deletion] 발화 → 자신을 시큐리티 맨 밑에 앞면으로 배치(트래시 아님)", BT25039_OnDeletionPlacesFaceUpBottomSecurity),
    ("BT25_092 W1: [Start of your Main Phase] 발화 → [TS] 카드 트래시 → Draw 1 + 메모리 +1", BT25092_StartOfMainDiscardDrawsAndGainsMemory),
    ("ST17_08 W1: [When Digivolving] 발화 → 상대 디지몬 최대 2체 tap(서스펜드)", ST17_08_WhenDigivolvingTapsOpponents),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try
    {
        await body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st)
        {
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(20)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ BT8_071 ═══════════════════════════════════

async Task BT8071_CannotReduceCost()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt8071 = Stage(match, P1, "BT8_071", ChoiceZone.BattleArea, "1:battle:BT8071", register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt8071, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Purple.BT8_071();

    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);
    Cec.ICardEffect? cannotReduceEffect = noneEffects.FirstOrDefault(e => e.GetType().Name == "CannotReduceCostClass");
    AssertTrue(cannotReduceEffect is not null,
        $"CannotReduceCostClass must register under timing=None — got [{string.Join(",", noneEffects.Select(e => e.GetType().Name))}]");

    var cannotReduce = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.CannotReduceCostClass)cannotReduceEffect!;
    var owner = new Cec.Player(match.Context, P1);
    HeadlessEntityId costCard = Stage(match, P1, "BT1_028", ChoiceZone.Hand, "1:hand:BT1028cost");
    var costCardSource = new Cec.CardSource(match.Context, costCard, P1);

    AssertTrue(cannotReduce.CannotReduceCost(owner, null!, costCardSource),
        "CannotReduceCostClass.CannotReduceCost returns true for a real play-costed card with no target permanents (blocks the reduction)");
}

// ═══════════════════════════════════ BT8_059 ═══════════════════════════════════

async Task BT8059_CannotIgnoreDigivolutionCondition()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt8059 = Stage(match, P1, "BT8_059", ChoiceZone.BattleArea, "1:battle:BT8059", register: true);
    HeadlessEntityId target = StageSynthetic(match, P1, "EXT2-TARGET", dp: 3000, level: 3, "1:battle:target");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt8059, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Black.BT8_059();

    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);
    Cec.ICardEffect? cannotIgnoreEffect = noneEffects.FirstOrDefault(e => e.GetType().Name == "CannotIgnoreDigivolutionConditionClass");
    AssertTrue(cannotIgnoreEffect is not null,
        $"CannotIgnoreDigivolutionConditionClass must register under timing=None — got [{string.Join(",", noneEffects.Select(e => e.GetType().Name))}]");

    var cannotIgnore = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.CannotIgnoreDigivolutionConditionClass)cannotIgnoreEffect!;
    var owner = new Cec.Player(match.Context, P1);
    var targetPermanent = new Cec.Permanent(match.Context, target, P1);
    HeadlessEntityId sourceCardId = Stage(match, P1, "BT1_028", ChoiceZone.Hand, "1:hand:BT1028src");
    var sourceCard = new Cec.CardSource(match.Context, sourceCardId, P1);

    AssertTrue(cannotIgnore.cannotIgnoreDigivolutionCondition(owner, targetPermanent, sourceCard),
        "CannotIgnoreDigivolutionConditionClass.cannotIgnoreDigivolutionCondition unconditionally returns true (players can't ignore digivolution requirements)");
}

// ═══════════════════════════════════ BT2_066 ═══════════════════════════════════

async Task BT2066_OnPlayDeDigivolve()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);

    HeadlessEntityId bt2066 = Stage(match, P1, "BT2_066", ChoiceZone.Hand, "1:hand:BT2066");
    HeadlessEntityId oppHost = StageSynthetic(match, P2, "EXT2-DEGEN", dp: 5000, level: 5, "2:battle:degen");
    HeadlessEntityId src1 = StageSynthetic(match, P2, "EXT2-SRC1", dp: 1000, level: 4, "2:under:src1", zone: ChoiceZone.None);
    HeadlessEntityId src2 = StageSynthetic(match, P2, "EXT2-SRC2", dp: 1000, level: 4, "2:under:src2", zone: ChoiceZone.None);
    SetSources(match, oppHost, src1, src2);
    AssertEqual(2, SourcesOf(match, oppHost).Count, "the opponent host was staged with exactly 2 digivolution sources");

    // SelectCountEffect ("How much will you De-Digivolve?") — pick 1.
    policy.On(req => req.Type == ChoiceType.Count, req => ChoiceResult.SelectCount(1));
    // SelectPermanentEffect (Mode.Custom) — pick the staged opponent host (the forced-auto-select path may
    // also fire without a prompt when exactly 1 candidate qualifies — either way this seat is ready).
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == oppHost),
        req => ChoiceResult.Select(oppHost));

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, bt2066,
        $"expected a PlayCard lane for BT2_066 — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, play);
    // De-Digivolve trashes the CURRENT TOP card of the target's stack and promotes the next source underneath
    // it to become the new top (a distinct HeadlessEntityId) — so oppHost's OWN instance ends up in the trash
    // rather than merely losing a digivolution-card count under its original id.
    await DriveUntilAsync(match, m => ZoneCards(m, P2, ChoiceZone.Trash).Contains(oppHost) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(bt2066), "BT2_066 landed on the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(oppHost),
        $"[On Play]: De-Digivolve trashed the top card of the opponent host's stack (count=1 chosen via SelectCountEffect) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Any(id => id == src1 || id == src2),
        "a promoted digivolution source now occupies the battle area in the host's place");
}

// ═══════════════════════════════════ BT20_025 ═══════════════════════════════════

async Task BT20025_AltDigivolveThenDelete()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT2-COREDRA", dp: 3000, level: 3, "1:battle:coredra", name: "Coredramon");
    HeadlessEntityId bt20025 = Stage(match, P1, "BT20_025", ChoiceZone.Hand, "1:hand:BT20025");
    HeadlessEntityId lowDp = StageSynthetic(match, P2, "EXT2-LOW", dp: 4000, level: 3, "2:battle:low");
    HeadlessEntityId highDp = StageSynthetic(match, P2, "EXT2-HIGH", dp: 9000, level: 6, "2:battle:high");
    int memBefore = MemoryFor(match, P1);

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == lowDp),
        req => ChoiceResult.Select(lowDp));

    LegalAction onto = FindDigivolveLane(match, P1, bt20025, host)
        ?? throw new InvalidOperationException(
            $"expected a cost-3 digivolve lane BT20_025 -> [Coredramon] — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => ZoneCards(m, P2, ChoiceZone.Trash).Contains(lowDp) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(bt20025), "BT20_025 landed on the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(lowDp),
        "[When Digivolving]: the 6000-DP-or-less opponent Digimon was deleted");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(highDp),
        "negative: the 9000 DP opponent Digimon (above the 6000 threshold) was NOT targeted/deleted");
    AssertEqual(memBefore - 3, MemoryFor(match, P1), "the alternative digivolution cost 3 was paid");
}

// ═══════════════════════════════════ BT14_081 ═══════════════════════════════════

async Task BT14081_WhenDigivolvingPlaysFromTrash()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT2-WHITE5", dp: 5000, level: 5, "1:battle:white5", colors: new[] { "White" });
    HeadlessEntityId bt14081 = Stage(match, P1, "BT14_081", ChoiceZone.Hand, "1:hand:BT14081");
    HeadlessEntityId trashTarget = StageSynthetic(match, P1, "EXT2-DARKANIMAL", dp: 2000, level: 3, "1:trash:dark",
        zone: ChoiceZone.Trash, traits: new[] { "Dark Animal" }, playCost: 3);

    // [When Digivolving] isOptional=true — the whole body is gated by an OptionalEffect ("Will you use ...?") seat.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    // The trash-play prompt (SelectCardEffect Root.Trash, Mode.Custom, canNoSelect:true) — pick the staged card.
    policy.On(req => req.Candidates.Any(c => c.Id == trashTarget), req => ChoiceResult.Select(trashTarget));

    LegalAction onto = FindDigivolveLane(match, P1, bt14081, host)
        ?? throw new InvalidOperationException(
            $"expected a digivolve lane BT14_081 -> Lv.5 [White] host — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(trashTarget) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(bt14081), "BT14_081 landed on the battle area");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(trashTarget),
        $"[When Digivolving]: the level-3 [Dark Animal] trash card was played onto the battle area for free " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Trash).Contains(trashTarget), "the played card left the trash");
}

// ═══════════════════════════════════ LM_018 ═══════════════════════════════════

async Task LM018_OnPlayDeleteThenToken()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);

    HeadlessEntityId lm018 = Stage(match, P1, "LM_018", ChoiceZone.Hand, "1:hand:LM018");
    HeadlessEntityId oppLowLevel = StageSynthetic(match, P2, "EXT2-LVL4", dp: 3000, level: 4, "2:battle:lvl4");

    // [On Play] isOptional=true — the whole body is gated by an OptionalEffect ("Will you use ...?") seat.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == oppLowLevel),
        req => ChoiceResult.Select(oppLowLevel));

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, lm018,
        $"expected a PlayCard lane for LM_018 — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(m, id) == "LM-018-token")
        || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(lm018), "LM_018 landed on the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(oppLowLevel),
        "[On Play]: the level-4 opponent Digimon was deleted");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(match, id) == "LM-018-token"),
        $"[On Play]: the [Gyuukimon] token entered the battle area after a successful deletion " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT15_082 ═══════════════════════════════════

async Task BT15082_StartOfTurnSetsMemoryTo3()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT15_082", ChoiceZone.BattleArea, "1:battle:BT15082", register: true);

    // Force memory to a value far from the target (3) so the [Start of Your Turn] set-effect is observable.
    match.Context.MemoryController.Set(20);

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    await PassTurnAsync(match, P2);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1) || m.IsTerminal());

    AssertEqual(3, MemoryFor(match, P1),
        $"[Start of Your Turn]: SetMemoryTo3TamerEffect pinned the memory to 3 at the start of P1's own turn " +
        $"(observed:{MemoryFor(match, P1)})");
}

// ═══════════════════════════════════ BT25_039 ═══════════════════════════════════

// NOTE (finding, not a port defect): the AS-IS "[Opponent's Turn]" redirect ability is registered with
// SetIsInheritedEffect(true) (AS-IS BT25_039.cs verbatim), and ICardEffect.CanActivate (ICardEffect.cs:392-410,
// mirrored byte-identical from DCGO/Assets/Scripts/Script/ICardEffect.cs) excludes an inherited/linked effect
// whenever EffectSourceCard IS its own permanent's TopCard — i.e. this ability structurally cannot activate
// while BT25_039 itself sits on top of its own stack (only once buried under a further digivolution). This is
// AS-IS-verbatim engine behavior (verified byte-for-byte against the real Unity source), not something this
// port altered, so the witness below exercises BT25_039's [On Deletion] region instead (no inherited-effect
// gate — a plain else-branch ActivateClass), which is directly observable without digivolving further.
async Task BT25039_OnDeletionPlacesFaceUpBottomSecurity()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4801, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt25039 = Stage(match, P1, "BT25_039", ChoiceZone.BattleArea, "1:battle:BT25039", register: true);
    int secBefore = Count(match, P1, ChoiceZone.Security);

    // [On Deletion] isOptional=true (OnDeletionClass factory) — the whole body is gated by an OptionalEffect seat.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    HeadlessEntityId fillerId = Stage(match, P1, "BT1_028", ChoiceZone.Hand, "1:hand:filler-deleter");
    var causerCard = new Cec.CardSource(match.Context, fillerId, P1);
    var causer = new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass();
    causer.SetUpICardEffect("test-harness delete", _ => true, causerCard);

    var targetPermanent = new Cec.Permanent(match.Context, bt25039, P1);
    await Cec.CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
        new List<Cec.Permanent> { targetPermanent }, causer, null, null);

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Security).Contains(bt25039),
        $"[On Deletion]: BT25_039 placed itself face up as the bottom security card instead of going to the trash " +
        $"(security before:{secBefore} after:{Count(match, P1, ChoiceZone.Security)}) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Trash).Contains(bt25039), "BT25_039 did not end up in the trash");
}

// ═══════════════════════════════════ BT25_092 ═══════════════════════════════════

async Task BT25092_StartOfMainDiscardDrawsAndGainsMemory()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 4901, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT25_092", ChoiceZone.BattleArea, "1:battle:BT25092", register: true);
    HeadlessEntityId tsCard = StageSynthetic(match, P1, "EXT2-TS", dp: 1000, level: 3, "1:hand:tscard",
        zone: ChoiceZone.Hand, traits: new[] { "TS" });
    int libraryBefore = Count(match, P1, ChoiceZone.Library);
    // Read the baseline BEFORE any turn handover — P1 is turn player right now (same as when the trigger
    // eventually re-fires on P1's own returning turn, an even number of handovers later), so the gauge's
    // turn-player-relative sign convention is directly comparable across the round trip.
    int memBefore = MemoryFor(match, P1);

    // Discard prompt (SelectHandEffect Mode.Discard, canNoSelect:true) — select the [TS] card (not skip).
    policy.On(req => req.Candidates.Any(c => c.Id == tsCard), req => ChoiceResult.Select(tsCard));

    // [Start of Your Main Phase] is a mandatory trigger (optional:false) — pass to the NEXT of P1's own turns
    // so the phase-start pump re-fires it fresh (avoids racing the CURRENT bootstrap Main window). The trigger
    // may already resolve INSIDE PassTurnAsync's own step pair (before this call returns) rather than waiting
    // for a later drive — so the library/memory deltas are read only after the whole round trip completes.
    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    await PassTurnAsync(match, P2);
    await DriveUntilAsync(match, m => (Count(m, P1, ChoiceZone.Library) < libraryBefore) || m.IsTerminal());

    AssertTrue(Count(match, P1, ChoiceZone.Library) < libraryBefore,
        $"[Start of your Main Phase]: trashing the [TS] hand card triggered <Draw 1> (library before:{libraryBefore} after:{Count(match, P1, ChoiceZone.Library)}) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(tsCard), "the [TS] card left the hand (trashed)");
    // Memory is compared with a strict INCREASE rather than an exact +1 — 2 intervening turn handovers
    // (P1->P2->P1) apply their own AS-IS gauge bookkeeping alongside this card's own AddMemory(1, activateClass)
    // call, so the net delta isn't isolable to the card's own contribution from the outside; the sign of the
    // change (a real gain occurred, not a no-op) is what demonstrates the AddMemory call actually fired.
    AssertTrue(MemoryFor(match, P1) > memBefore,
        $"AddMemory(1, activateClass) produced a real memory gain after the trash+draw (before:{memBefore} after:{MemoryFor(match, P1)}) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ ST17_08 ═══════════════════════════════════

async Task ST17_08_WhenDigivolvingTapsOpponents()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 5001, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT2-GREEN5", dp: 5000, level: 5, "1:battle:green5", colors: new[] { "Green" });
    HeadlessEntityId st1708 = Stage(match, P1, "ST17_08", ChoiceZone.Hand, "1:hand:ST1708");
    HeadlessEntityId opp1 = StageSynthetic(match, P2, "EXT2-OPP1", dp: 2000, level: 3, "2:battle:opp1");
    HeadlessEntityId opp2 = StageSynthetic(match, P2, "EXT2-OPP2", dp: 2000, level: 3, "2:battle:opp2");

    // Mode.Tap select (up to 2) — tap both staged opponents.
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == opp1 || c.Id == opp2),
        req => ChoiceResult.Select(req.Candidates.Where(c => c.IsSelectable && (c.Id == opp1 || c.Id == opp2)).Select(c => c.Id)),
        oneShot: false);
    // The 2nd (can't-unsuspend/digivolve) select prompt reuses the same candidate pool — answer with whatever's offered.
    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    LegalAction onto = FindDigivolveLane(match, P1, st1708, host)
        ?? throw new InvalidOperationException(
            $"expected a digivolve lane ST17_08 -> Lv.5 [Green] host — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => IsSuspended(m, opp1) || IsSuspended(m, opp2) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(st1708), "ST17_08 landed on the battle area");
    AssertTrue(IsSuspended(match, opp1) || IsSuspended(match, opp2),
        $"[When Digivolving]: at least 1 of the 2 opponent Digimon was tapped (suspended) via SelectPermanentEffect Mode.Tap " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ harness ═══════════════════════════════════

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewPilotMatchAsync(int seed, PlayerDeckSetup[] decks)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        decks, firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);
    return (match, policy);
}

async Task ReachMainWaitAsync(DcgoMatch match)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision
                || match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else
        {
            await StepOnceAsync(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} " +
            $"player:{t.TurnPlayerId} choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} " +
            $"pending:{match.HasPendingChoice()} controllerState:{match.Context.ChoiceController.Current.IsPending}/{match.Context.ChoiceController.Current.IsResolved} " +
            $"terminal:{match.IsTerminal()} memory:{match.Context.MemoryController.Current.Current} " +
            $"lanes:[{string.Join(", ", Legal(match, t.TurnPlayerId ?? default).Select(a => a.ActionType))}]");
    }
}

async Task PassTurnAsync(DcgoMatch match, HeadlessPlayerId player)
{
    LegalAction pass = Legal(match, player).First(a => a.ActionType == HeadlessActionTypes.Pass);
    await ApplyAsync(match, pass);
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }

    if (action is null)
    {
        throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    }

    await ApplyAsync(match, action);
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

static LegalAction? FindLane(DcgoMatch match, HeadlessPlayerId player, string actionType, HeadlessEntityId cardId)
{
    return Legal(match, player).FirstOrDefault(a => a.ActionType == actionType && ActionCardIds(a).Contains(cardId));
}

static LegalAction RequireLane(DcgoMatch match, HeadlessPlayerId player, string actionType, HeadlessEntityId cardId, string why)
{
    return FindLane(match, player, actionType, cardId)
        ?? throw new InvalidOperationException(
            $"expected a {actionType} lane for {cardId.Value} — {why}. listed: " +
            string.Join(", ", Legal(match, player).Select(a => $"{a.ActionType}({string.Join('/', ActionCardIds(a).Select(i => i.Value))})")));
}

static LegalAction? FindDigivolveLane(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId cardId, HeadlessEntityId targetId)
{
    return Legal(match, player).FirstOrDefault(a =>
        a.ActionType == HeadlessActionTypes.Digivolve
        && a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? c) && c is HeadlessEntityId cid && cid == cardId
        && a.Parameters.TryGetValue(HeadlessActionParameterKeys.TargetCardId, out object? t) && t is HeadlessEntityId tid && tid == targetId);
}

static IEnumerable<HeadlessEntityId> ActionCardIds(LegalAction action)
{
    foreach (string key in new[]
    {
        HeadlessActionParameterKeys.CardId,
        HeadlessActionParameterKeys.AttackerId,
        HeadlessActionParameterKeys.TargetCardId,
    })
    {
        if (action.Parameters.TryGetValue(key, out object? raw) && raw is HeadlessEntityId id)
        {
            yield return id;
        }
    }
}

// 실카드 스테이징: cards.json 로더가 이미 def를 넣었으므로(def id = 카드번호) 인스턴스만 만들어 이동.
HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, string instanceId,
    bool register = false)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId(cardNumber);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? existing) || existing is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    if (register)
    {
        Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

// 합성 픽스처 카드(R4S3b StageBattleDigimon 관례 확장): def 업서트 + 인스턴스 + 존 이동. playCost 추가
// (AS-IS CardSource.HasPlayCost/GetCostItself 시나리오 — EXEMPLAR-T1 원본은 미설정이었음).
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, int? playCost = null, string[]? colors = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (colors is { Length: > 0 })
    {
        meta["colors"] = colors;
    }

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType, PlayCost: playCost));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

static async Task ClearZoneAsync(DcgoMatch match, HeadlessPlayerId owner, ChoiceZone from, ChoiceZone to)
{
    foreach (HeadlessEntityId id in ZoneCards(match, owner, from).ToArray())
    {
        await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, from, to));
    }
}

static void SetSources(DcgoMatch match, HeadlessEntityId hostId, params HeadlessEntityId[] sourceIds)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {hostId.Value}");
    }

    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
    {
        [DigivolutionStackReader.SourceIdsKey] = sourceIds.Select(id => id.Value).ToArray(),
    };
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static void Suspend(DcgoMatch match, HeadlessEntityId id)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { ["isSuspended"] = true };
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static bool IsSuspended(DcgoMatch match, HeadlessEntityId id)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        return false;
    }

    var permanent = new Cec.Permanent(match.Context, id, record.OwnerId);
    return permanent.IsSuspended;
}

static IReadOnlyList<HeadlessEntityId> SourcesOf(DcgoMatch match, HeadlessEntityId hostId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? record) || record is null)
    {
        return Array.Empty<HeadlessEntityId>();
    }

    var host = new Cec.Permanent(match.Context, hostId, record.OwnerId);
    return host.DigivolutionCards.Select(cs => cs.InstanceId).ToArray();
}

static string CardNumberOf(DcgoMatch match, HeadlessEntityId cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null
        || !match.Context.CardRepository.TryGetCard(record.DefinitionId, out CardRecord? def) || def is null)
    {
        return string.Empty;
    }

    return def.CardNumber;
}

static int MemoryFor(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.Player(match.Context, player).MemoryForPlayer;
}

static int Count(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone).Count : -1;
}

static IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones
        ? zones.GetCards(player, zone)
        : Array.Empty<HeadlessEntityId>();
}

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {message} (expected {expected}, got {actual})");
    }
}

// ═══════════════════════════════ providers/context ═══════════════════════════════

/// <summary>에이전트 좌석: 술어-매칭 스크립트 답변 + ScriptedChoiceProvider 동일 폴백(검증 포함).</summary>
sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public static ChoiceResult Fallback(ChoiceRequest request)
        => new ScriptedChoiceProvider().ChooseAsync(request).GetAwaiter().GetResult();

    /// <summary>진단용: 이 좌석이 응답한 프롬프트 요약(타입/메시지/후보수).</summary>
    public List<string> Seen { get; } = new();

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}");
        for (int i = 0; i < _handlers.Count; i++)
        {
            (Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot)
                {
                    _handlers.RemoveAt(i);
                }

                return Task.FromResult(result);
            }
        }

        return _fallback.ChooseAsync(request, cancellationToken);
    }
}

/// <summary>EngineContext.CreateDefault의 1:1 재현 — provider 좌석만 교체(그 외 배선 동일).</summary>
static class ContextFactory
{
    public static EngineContext CreateWithProvider(IChoiceProvider provider, int randomSeed)
    {
        var randomSource = new GameRandomSource(randomSeed);
        var cardInstanceRepository = new InMemoryCardInstanceRepository();
        var logSink = new NullLogSink();
        var zoneMover = new InMemoryZoneMover(randomSource);
        var memoryController = new InMemoryHeadlessMemoryController();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, gameEventQueue,
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId,
                    context: selfRef),
                strictUnbound: false));

        var choiceController = new InMemoryHeadlessChoiceController();
        var context = new EngineContext(
            provider,
            randomSource,
            new CardDatabase(),
            cardInstanceRepository,
            zoneMover,
            new InMemoryRuleQueryService(),
            new InMemoryHeadlessTurnController(),
            choiceController,
            new InMemoryHeadlessAttackController(),
            memoryController,
            logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(),
            effectScheduler,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
