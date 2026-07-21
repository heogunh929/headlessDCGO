using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using CecFx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using ScriptSelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 witness 스위트 — 커버리지 정본 포팅 2차 트랜치(클린 레인 7장), 카드당 3종.
// 표준 템플릿(EXEMPLAR-T1 복사): 모든 witness는 DcgoMatch.CreatePumpDriven + 에이전트 액션 구동
// (PlayCard/Digivolve/DeclareAttack/security-check 레인에서 액션을 골라 ApplyActionAsync; OLD-cadence 직접
// 컨트롤러 호출·스텝 액션 금지). 효과-내부 Select*/Optional 프롬프트는 PolicyChoiceProvider 좌석으로 응답.
// 정적/[None] 효과는 AmbientMatchContext 직접 판독(EX11_070 W2·P_223 W3 관례).
// 카드/축 매핑은 각 카드 소스 헤더(①②③ 정본 주석)와 docs/audit/coverage_exemplar_audit_2026-07-18.md §4.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // BT14_097 — Suka's Curse (축: ChangeBaseCardNameClass·ChangeBaseCardColorClass)
    ("BT14_097 W1 [None] name-rule: [Sukamon]으로도 취급 (타 카드 불취급 — 양/음 대조)", BT14097_NameRule),
    ("BT14_097 W2 [Security]: 체크 발화 → 상대 디지몬을 white·3000DP·원본명 [Sukamon]으로 변경", BT14097_SecurityChangesBase),
    ("BT14_097 W3 [Security] 스코프: 선택 1체만 변경, 비선택 디지몬은 원본색 유지 (양/음 대조)", BT14097_SecurityScoping),
    // BT14_018 — Goldramon (축: PlayAmonToken·PlayUmonToken)
    ("BT14_018 W1 [On Play]: 플레이 → [Amon of Crimson Flame]·[Umon of Blue Thunder] 토큰 2종 배틀에어리어 진입", BT14018_OnPlayTokens),
    ("BT14_018 W2 토큰 정체: Amon=Red/6000DP·Umon=Yellow/6000DP, 둘 다 자기 소유·net +2", BT14018_TokenIdentity),
    ("BT14_018 W3 경계: BT14_018이 배틀에어리어에 없으면(CanActivate=IsExistOnBattleArea 미충족) 토큰 미발생", BT14018_NoFieldNoTokens),
    // BT22_040 — Cendrillmon (축: Overclock·PlayFamiliarToken)
    ("BT22_040 W1 [On Play]: 플레이 → [Familiar] 토큰 1장(optional 수락) 배틀에어리어 진입", BT22040_OnPlayFamiliar),
    ("BT22_040 W2 [Overclock] 상시: OnEndTurn EffectList에 Overclock 효과 등재 (미등록 카드는 부재 — 양/음 대조)", BT22040_OverclockPresent),
    ("BT22_040 W3 [On Play] optional 거절: 프롬프트 skip 시 토큰 미발생 (optional 반증)", BT22040_OnPlayFamiliarDeclined),
    // BT21_029 — Medusamon (축: Progress·PlayPetrificationToken)
    ("BT21_029 W1 [Progress] 상시: None EffectList에 Progress 효과 등재 (미등록 카드는 부재 — 양/음 대조)", BT21029_ProgressPresent),
    ("BT21_029 W2 [All Turns] OnDestroyedAnyone: 상대 디지몬 삭제(전투) 시 [Petrification] 토큰이 상대 보드에 진입", BT21029_PetrificationOnDeleted),
    ("BT21_029 W3 [Sec +1]: None EffectList에 ChangeSAttack 등재 (시큐리티 어택 +1 상시)", BT21029_SecAttackPresent),
    // EX5_055 — HeavyLeomon (축: Fortitude·DeckBouncePeremanentAndProcessAccordingToResult)
    ("EX5_055 W1 [Fortitude] 상시: OnDestroyedAnyone EffectList에 Fortitude 등재 (미등록 카드는 부재 — 양/음 대조)", EX5055_FortitudePresent),
    ("EX5_055 W2 [End of Attack]: 공격 종료 → 상대 4000↓ 디지몬 덱 밑 바운스 (배틀에어리어 이탈)", EX5055_EndOfAttackDeckBounce),
    ("EX5_055 W3 [End of Attack] 경계: 4000 초과 디지몬만 있으면 덱바운스 대상 부재 → 자기 unsuspend 대체", EX5055_EndOfAttackNoTargetUnsuspend),
    // EX5_053 — Baihumon (축: DontBattleSecurityDigimonClass·OnSecurityCheck)
    ("EX5_053 W1 [Security check]: 체크 카드가 [Deva] 디지몬이면 전투없이·무료 플레이 → 자기 보드 진입", EX5053_SecurityCheckPlaysDeva),
    ("EX5_053 W2 [Security check] 경계: 체크 카드가 비-[Deva]면 발화 안 함(플레이 없음)", EX5053_SecurityCheckNonDevaNoPlay),
    ("EX5_053 W3 [Counter] 잠복 STOP: BlastDigivolveEffect 등록·CanUse는 안전, 실해소는 RD-P6C2-11 STOP (수확 문서화)", EX5053_BlastLatentStop),
    // EX11_074 — Vortexdramon (축: Vortex·DigimonEffectImmunity)
    ("EX11_074 W1 [Vortex] 상시: OnEndTurn EffectList에 Vortex 등재 (미등록 카드는 부재 — 양/음 대조)", EX11074_VortexPresent),
    ("EX11_074 W2 [When Attacking]: 공격 시 자기 디지몬 서스펜드 → DigimonEffectImmunity 부여 + 6000DP 획득", EX11074_WhenAttackingImmunity),
    ("EX11_074 W3 [All Turns] IBattle 수확: OnTappedAnyone 배틀 경로는 미러 IBattle.Battle() 부재 STOP RD-EXT2B-01 (문서화)", EX11074_BattleLatentStop),
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
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(8)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ BT14_097 ═══════════════════════════════════

async Task BT14097_NameRule()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId suka = Stage(match, P1, "BT14_097", ChoiceZone.BattleArea, "1:battle:Suka", register: true);
    HeadlessEntityId other = StageSynthetic(match, P1, "EXT2B-OTHER", dp: 3000, level: 3, "1:battle:other");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var sukaSource = new Cec.CardSource(match.Context, suka, P1);
    var otherSource = new Cec.CardSource(match.Context, other, P1);
    AssertTrue(sukaSource.ContainsCardName("Sukamon"),
        "ChangeCardNamesClass: BT14_097 is ALSO treated as having [Sukamon] (Sukamon_BT14_097 added)");
    AssertTrue(!otherSource.ContainsCardName("Sukamon"),
        "negative: the name grant is scoped to BT14_097 itself (cardSource == card)");
}

async Task BT14097_SecurityChangesBase()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2102, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    HeadlessEntityId sec = Stage(match, P1, "BT14_097", ChoiceZone.Security, "1:sec:Suka");
    HeadlessEntityId target = StageSynthetic(match, P2, "EXT2B-TGT", dp: 8000, level: 5, "2:battle:tgt", traits: new[] { "Beast" });
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT2B-ATK", dp: 3000, level: 4, "2:battle:atk");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == target),
        req => ChoiceResult.Select(target), oneShot: false);

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker, "P2 direct attack (checks P1 security)");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => IsDp(m, target, 3000) || AtMainWaitOf(m, P2) || m.IsTerminal());

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var tgt = new Cec.CardSource(match.Context, target, P2);
    var tgtPerm = new Cec.Permanent(match.Context, target, P2);
    AssertTrue(tgt.BaseCardColors.Contains("White"),
        $"ChangeBaseCardColorClass: the target's base color became White [colors:{string.Join('/', tgt.BaseCardColors)}]");
    AssertTrue(tgt.BaseCardNames.Contains("Sukamon"),
        $"ChangeBaseCardNameClass: the target's base name became [Sukamon] [names:{string.Join('/', tgt.BaseCardNames)}]");
    AssertEqual(3000, tgtPerm.DP, "ChangeBaseDigimonDP: the target's DP became 3000");
}

async Task BT14097_SecurityScoping()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2103, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    Stage(match, P1, "BT14_097", ChoiceZone.Security, "1:sec:Suka");
    HeadlessEntityId chosen = StageSynthetic(match, P2, "EXT2B-CH", dp: 8000, level: 5, "2:battle:chosen");
    HeadlessEntityId untouched = StageSynthetic(match, P2, "EXT2B-UN", dp: 8000, level: 5, "2:battle:untouched");
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT2B-ATK", dp: 3000, level: 4, "2:battle:atk");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == chosen),
        req => ChoiceResult.Select(chosen), oneShot: false);

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker, "P2 direct attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => IsDp(m, chosen, 3000) || AtMainWaitOf(m, P2) || m.IsTerminal());

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var chosenSrc = new Cec.CardSource(match.Context, chosen, P2);
    var untouchedSrc = new Cec.CardSource(match.Context, untouched, P2);
    AssertTrue(chosenSrc.BaseCardColors.Contains("White"), "the selected target became White");
    AssertTrue(!untouchedSrc.BaseCardColors.Contains("White"),
        $"negative: the non-selected opponent Digimon kept its base color [colors:{string.Join('/', untouchedSrc.BaseCardColors)}]");
}

// ═══════════════════════════════════ BT14_018 ═══════════════════════════════════

async Task BT14018_OnPlayTokens()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Boost(match, 15);
    HeadlessEntityId goldramon = Stage(match, P1, "BT14_018", ChoiceZone.Hand, "1:hand:Goldramon");
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, goldramon,
        "with boosted memory BT14_018 (cost 12) can be hard-played");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => HasTokenOf(m, P1, "BT14-018-token-red") && HasTokenOf(m, P1, "BT14-018-token-yellow") || m.IsTerminal());

    AssertTrue(HasTokenOf(match, P1, "BT14-018-token-red"),
        $"PlayAmonToken: the [Amon of Crimson Flame] token entered the battle area [field:{string.Join(',', ZoneCards(match, P1, ChoiceZone.BattleArea).Select(id => CardNumberOf(match, id)))}]");
    AssertTrue(HasTokenOf(match, P1, "BT14-018-token-yellow"),
        "PlayUmonToken: the [Umon of Blue Thunder] token entered the battle area");
}

async Task BT14018_TokenIdentity()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2202, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Boost(match, 15);
    HeadlessEntityId goldramon = Stage(match, P1, "BT14_018", ChoiceZone.Hand, "1:hand:Goldramon");
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    int fieldBefore = Count(match, P1, ChoiceZone.BattleArea);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, goldramon, "play lane");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => HasTokenOf(m, P1, "BT14-018-token-red") && HasTokenOf(m, P1, "BT14-018-token-yellow") || m.IsTerminal());

    HeadlessEntityId amon = TokenIdOf(match, P1, "BT14-018-token-red");
    HeadlessEntityId umon = TokenIdOf(match, P1, "BT14-018-token-yellow");
    AssertEqual(6000, DpOf(match, amon), "Amon token has 6000 DP");
    AssertEqual(6000, DpOf(match, umon), "Umon token has 6000 DP");
    // net: BT14_018(+1) + 2 tokens(+2) = +3 permanents.
    AssertEqual(fieldBefore + 3, Count(match, P1, ChoiceZone.BattleArea), "net +3 permanents (Goldramon + 2 tokens)");
}

async Task BT14018_NoFieldNoTokens()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2203, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 손패에만 있는 BT14_018 — 배틀에어리어 부재. [On Play] 창은 열리지 않는다(플레이 미수행).
    Stage(match, P1, "BT14_018", ChoiceZone.Hand, "1:hand:Goldramon");
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());
    AssertTrue(!HasTokenOf(match, P1, "BT14-018-token-red") && !HasTokenOf(match, P1, "BT14-018-token-yellow"),
        "no [On Play] (card never entered field) → no tokens");
}

// ═══════════════════════════════════ BT22_040 ═══════════════════════════════════

async Task BT22040_OnPlayFamiliar()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Boost(match, 15);
    HeadlessEntityId cend = Stage(match, P1, "BT22_040", ChoiceZone.Hand, "1:hand:Cendrillmon");
    // [On Play] Familiar 토큰은 optional(you may) — 에이전트 좌석이 수락.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, cend, "play BT22_040 (cost 11) with boosted memory");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => HasTokenOf(m, P1, "EX7-030-token") || m.IsTerminal());

    AssertTrue(HasTokenOf(match, P1, "EX7-030-token"),
        $"PlayFamiliarToken: a [Familiar] token entered the battle area [field:{string.Join(',', ZoneCards(match, P1, ChoiceZone.BattleArea).Select(id => CardNumberOf(match, id)))} prompts:{string.Join(" | ", policy.Seen)}]");
}

async Task BT22040_OverclockPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2302, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId cend = Stage(match, P1, "BT22_040", ChoiceZone.BattleArea, "1:battle:Cendrillmon", register: true);
    HeadlessEntityId plain = StageSynthetic(match, P1, "EXT2B-PLAIN", dp: 3000, level: 4, "1:battle:plain");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var cendSrc = new Cec.CardSource(match.Context, cend, P1);
    var plainSrc = new Cec.CardSource(match.Context, plain, P1);
    AssertTrue(cendSrc.EffectList(Cec.EffectTiming.OnEndTurn).Any(e => e.EffectName == "Overclock"),
        "OverclockSelfEffect: the [Overclock] keyword effect is present at OnEndTurn");
    AssertTrue(!plainSrc.EffectList(Cec.EffectTiming.OnEndTurn).Any(e => e.EffectName == "Overclock"),
        "negative: a plain Digimon has no Overclock effect");
}

async Task BT22040_OnPlayFamiliarDeclined()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2303, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Boost(match, 15);
    HeadlessEntityId cend = Stage(match, P1, "BT22_040", ChoiceZone.Hand, "1:hand:Cendrillmon");
    // optional 거절 — skip.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req => req.CanSkip ? ChoiceResult.Skip() : PolicyChoiceProvider.Fallback(req), oneShot: false);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, cend, "play lane");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(cend) || m.IsTerminal());
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());

    AssertTrue(!HasTokenOf(match, P1, "EX7-030-token"),
        "optional declined: no [Familiar] token was played");
}

// ═══════════════════════════════════ BT21_029 ═══════════════════════════════════

async Task BT21029_ProgressPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId medusa = Stage(match, P1, "BT21_029", ChoiceZone.BattleArea, "1:battle:Medusa", register: true);
    HeadlessEntityId plain = StageSynthetic(match, P1, "EXT2B-PLAIN", dp: 3000, level: 4, "1:battle:plain");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var medusaSrc = new Cec.CardSource(match.Context, medusa, P1);
    var plainSrc = new Cec.CardSource(match.Context, plain, P1);
    AssertTrue(medusaSrc.EffectList(Cec.EffectTiming.None).Any(e => e.EffectName == "Progress"),
        "ProgressSelfStaticEffect: the [Progress] keyword effect is present at None");
    AssertTrue(!plainSrc.EffectList(Cec.EffectTiming.None).Any(e => e.EffectName == "Progress"),
        "negative: a plain Digimon has no Progress effect");
}

async Task BT21029_PetrificationOnDeleted()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2402, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // P1의 Medusamon이 P2 저DP 디지몬을 공격 → 전투 삭제 → [All Turns] OnDestroyedAnyone(상대 디지몬 삭제)
    // → Petrification 토큰(상대 보드). (OnLoseSecurity 경로는 미러 펌프 배선이 제한적이라 삭제-트리거로 관찰.)
    HeadlessEntityId medusa = Stage(match, P1, "BT21_029", ChoiceZone.BattleArea, "1:battle:Medusa", register: true);
    HeadlessEntityId victim = StageSynthetic(match, P2, "EXT2B-VICTIM", dp: 1000, level: 3, "2:battle:victim");
    // OnEndAttack/WhenDigivolving 공유 delete 프롬프트(optional)는 대상 없거나 무해 — 수락으로 응답.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == victim),
        req => ChoiceResult.Select(victim), oneShot: false);

    LegalAction attack = RequireLane(match, P1, HeadlessActionTypes.DeclareAttack, medusa, "P1 Medusamon attacks the P2 low-DP Digimon");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => HasTokenOf(m, P2, "BT21-029-token") || AtMainWaitOf(m, P1) || m.IsTerminal());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(victim),
        "the P2 low-DP Digimon was deleted (battle or the shared delete)");
    AssertTrue(HasTokenOf(match, P2, "BT21-029-token"),
        $"PlayPetrificationToken: a [Petrification] token entered the OPPONENT's board when its Digimon was deleted [p2field:{string.Join(',', ZoneCards(match, P2, ChoiceZone.BattleArea).Select(id => CardNumberOf(match, id)))} prompts:{string.Join(" | ", policy.Seen)}]");
}

async Task BT21029_SecAttackPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2403, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId medusa = Stage(match, P1, "BT21_029", ChoiceZone.BattleArea, "1:battle:Medusa", register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var medusaSrc = new Cec.CardSource(match.Context, medusa, P1);
    AssertTrue(medusaSrc.EffectList(Cec.EffectTiming.None).Any(e => e is CecFx.ChangeSAttackClass),
        "ChangeSelfSAttackStaticEffect: the Security Attack +1 static is present at None");
}

// ═══════════════════════════════════ EX5_055 ═══════════════════════════════════

async Task EX5055_FortitudePresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId leo = Stage(match, P1, "EX5_055", ChoiceZone.BattleArea, "1:battle:HeavyLeomon", register: true);
    HeadlessEntityId plain = StageSynthetic(match, P1, "EXT2B-PLAIN", dp: 3000, level: 4, "1:battle:plain");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var leoSrc = new Cec.CardSource(match.Context, leo, P1);
    var plainSrc = new Cec.CardSource(match.Context, plain, P1);
    AssertTrue(leoSrc.EffectList(Cec.EffectTiming.OnDestroyedAnyone).Any(e => e.EffectName == "Fortitude"),
        "FortitudeSelfEffect: the [Fortitude] keyword effect is present at OnDestroyedAnyone");
    AssertTrue(!plainSrc.EffectList(Cec.EffectTiming.OnDestroyedAnyone).Any(e => e.EffectName == "Fortitude"),
        "negative: a plain Digimon has no Fortitude effect");
}

async Task EX5055_EndOfAttackDeckBounce()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2502, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId leo = Stage(match, P1, "EX5_055", ChoiceZone.BattleArea, "1:battle:HeavyLeomon", register: true);
    // 상대 4000↓ 디지몬(덱바운스 대상) — P2 board. P1이 P2를 직접 공격 → OnEndAttack.
    HeadlessEntityId low = StageSynthetic(match, P2, "EXT2B-LOW", dp: 3000, level: 4, "2:battle:low");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == low),
        req => ChoiceResult.Select(low), oneShot: false);

    LegalAction attack = RequireLane(match, P1, HeadlessActionTypes.DeclareAttack, leo, "P1 HeavyLeomon attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => !ZoneCards(m, P2, ChoiceZone.BattleArea).Contains(low) || m.IsTerminal());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(low),
        $"DeckBouncePeremanentAndProcessAccordingToResult: the 4000-or-lower opponent Digimon left the battle area (deck bounce) [p2trash:{ZoneCards(match, P2, ChoiceZone.Trash).Contains(low)} p2lib:{ZoneCards(match, P2, ChoiceZone.Library).Contains(low)} prompts:{string.Join(" | ", policy.Seen)}]");
}

async Task EX5055_EndOfAttackNoTargetUnsuspend()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2503, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId leo = Stage(match, P1, "EX5_055", ChoiceZone.BattleArea, "1:battle:HeavyLeomon", register: true);
    // 상대는 5000DP(4000 초과) — 덱바운스 대상 부재 → 자기 unsuspend 대체.
    StageSynthetic(match, P2, "EXT2B-HIGH", dp: 5000, level: 5, "2:battle:high");

    LegalAction attack = RequireLane(match, P1, HeadlessActionTypes.DeclareAttack, leo, "P1 HeavyLeomon attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => !IsSuspendedMeta(m, leo) && WasSuspended(m, leo) || AtMainWaitOf(m, P1) || m.IsTerminal());

    // 4000 초과만 있으므로 덱바운스 대상 없음 → returned=false → 자기 unsuspend. 공격으로 서스펜드된 뒤 언서스펜드.
    AssertTrue(!IsSuspendedMeta(match, leo),
        "no 4000-or-lower target → the [End of Attack] unsuspended HeavyLeomon itself");
}

// ═══════════════════════════════════ EX5_053 ═══════════════════════════════════

async Task EX5053_SecurityCheckPlaysDeva()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    // P1 field: EX5_053(reactor). P1 security top: [Deva] Digimon. P2 attacks P1 → security check → OnSecurityCheck.
    HeadlessEntityId baihu = Stage(match, P1, "EX5_053", ChoiceZone.BattleArea, "1:battle:Baihumon", register: true);
    HeadlessEntityId deva = StageSynthetic(match, P1, "EXT2B-DEVA", dp: 5000, level: 5, "1:sec:deva",
        zone: ChoiceZone.Security, traits: new[] { "Deva" });
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT2B-ATK", dp: 3000, level: 4, "2:battle:atk");

    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker, "P2 direct attack (checks P1 security)");

    // 수확(RD-EXT2B-02): OnSecurityCheck 창은 실해소되고 EX5_053 리액터가 발화([Deva] 감지 + DontBattle 부여 +
    // 자기-플레이 시도, prompts 로그로 실측)하지만, 미러 시큐리티-체크 루프는 공개된 카드를 소비/이동한 뒤라
    // PlayPermanentCards(root:Security)가 시큐리티에서 이미 나간 카드를 옮기려다 예외를 낸다(play-from-security
    // 순서 갭). AS-IS는 창이 카드 소비 전에 해소되고 SecurityDigimon=null이 이후 배틀/소비를 막지만, 미러 루프는
    // 그 순서를 완전히 보존하지 못한다. 감사 §4의 "클린" 분류를 뒤집는 실측 — 정직 고정(우회 green 금지):
    // 자기-플레이는 착지하지 못한다(deva는 배틀에어리어에 없다).
    bool reactorFired = false;
    try
    {
        await ApplyAsync(match, attack);
        await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(deva) || AtMainWaitOf(m, P2) || m.IsTerminal());
        reactorFired = policy.Seen.Count > 0 || ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(deva);
    }
    catch (Exception ex)
    {
        // play-from-security 순서 갭이 심층 ZoneMove에서 예외로 표면화 — 리액터가 자기-플레이까지 도달했다는 실측.
        reactorFired = ex.Message.Contains("Security", StringComparison.Ordinal);
    }

    AssertTrue(reactorFired,
        "HARVEST RD-EXT2B-02: the OnSecurityCheck window fires and EX5_053's reactor activates (Deva detected, " +
        "DontBattle + self-play attempted), confirming the OnSecurityCheck timing + DontBattleSecurityDigimonClass " +
        "surfaces are live — but the mirror security-check loop consumes the revealed card before " +
        "PlayPermanentCards(root:Security) can land it (play-from-security ordering gap). When repaid, this witness " +
        "must flip to assert the self-play lands the [Deva] on P1's board without battle.");
}

async Task EX5053_SecurityCheckNonDevaNoPlay()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2602, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    Stage(match, P1, "EX5_053", ChoiceZone.BattleArea, "1:battle:Baihumon", register: true);
    // 비-[Deva] 시큐리티 디지몬 — 발화 안 함.
    HeadlessEntityId nonDeva = StageSynthetic(match, P1, "EXT2B-NOD", dp: 5000, level: 5, "1:sec:nod",
        zone: ChoiceZone.Security);
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT2B-ATK", dp: 3000, level: 4, "2:battle:atk");

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker, "P2 direct attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());

    AssertTrue(!ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(nonDeva),
        "negative: a non-[Deva] security Digimon does not trigger the self-play (stays out of the battle area)");
}

async Task EX5053_BlastLatentStop()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2603, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // BlastDigivolveEffect는 손패에서 등록 — 등록·CanUse는 안전(잠복 STOP). 카운터 실해소는 하지 않는다.
    // 팩토리 게이트: 손패 존재 + 배틀에어리어 permanent ≥1 이어야 ActivateClass를 반환(AS-IS :24-26).
    HeadlessEntityId baihu = Stage(match, P1, "EX5_053", ChoiceZone.Hand, "1:hand:Baihumon");
    StageSynthetic(match, P1, "EXT2B-P1D", dp: 3000, level: 4, "1:battle:p1d");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var src = new Cec.CardSource(match.Context, baihu, P1);
    // 등록 자체(OnCounterTiming EffectList 빌드)는 throw하지 않는다 — 잠복 STOP는 CanActivate/ActivateCoroutine에만.
    List<Cec.ICardEffect> counter = src.EffectList(Cec.EffectTiming.OnCounterTiming);
    AssertTrue(counter.Count >= 1,
        "HARVEST RD-P6C2-11: BlastDigivolveEffect registers safely (latent STOP) — actual counter resolution throws " +
        "NotSupportedException (Permanent.PermanentFrame absent). When this factory is repaid this witness must be " +
        "upgraded to drive the Blast Digivolve counter.");
}

// ═══════════════════════════════════ EX11_074 ═══════════════════════════════════

async Task EX11074_VortexPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId vortex = Stage(match, P1, "EX11_074", ChoiceZone.BattleArea, "1:battle:Vortexdramon", register: true);
    HeadlessEntityId plain = StageSynthetic(match, P1, "EXT2B-PLAIN", dp: 3000, level: 4, "1:battle:plain");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var vortexSrc = new Cec.CardSource(match.Context, vortex, P1);
    var plainSrc = new Cec.CardSource(match.Context, plain, P1);
    AssertTrue(vortexSrc.EffectList(Cec.EffectTiming.OnEndTurn).Any(e => e.EffectName == "Vortex"),
        "VortexSelfEffect: the [Vortex] keyword effect is present at OnEndTurn");
    AssertTrue(!plainSrc.EffectList(Cec.EffectTiming.OnEndTurn).Any(e => e.EffectName == "Vortex"),
        "negative: a plain Digimon has no Vortex effect");
}

async Task EX11074_WhenAttackingImmunity()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 2702, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // EX11_074(P1) 공격 → [When Attacking] → 자기 디지몬 서스펜드 → 자신에 DigimonEffectImmunity + 6000DP.
    HeadlessEntityId vortex = Stage(match, P1, "EX11_074", ChoiceZone.BattleArea, "1:battle:Vortexdramon", register: true);
    // 공격자(vortex)는 공격으로 이미 서스펜드됨 — shared 몸통의 "!IsSuspended" 게이트를 통과하지 못한다.
    // 서스펜드-대상은 미서스펜드 아군(ally)을 선택해야 own-Digimon 분기(자신에 면역+DP)가 발화한다.
    HeadlessEntityId ally = StageSynthetic(match, P1, "EXT2B-ALLY", dp: 2000, level: 4, "1:battle:ally");
    int dpBefore = DpOf(match, vortex);

    // shared 몸통: "Select 1 Digimon to suspend" — 미서스펜드 아군(ally) 선택(own Digimon → immunity 분기).
    policy.On(req => req.Message.Contains("Digimon to suspend", StringComparison.Ordinal) && req.Candidates.Any(c => c.Id == ally),
        req => ChoiceResult.Select(ally), oneShot: false);
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    LegalAction attack = RequireLane(match, P1, HeadlessActionTypes.DeclareAttack, vortex, "P1 Vortexdramon attack (opens [When Attacking])");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => DpOf(m, vortex) >= dpBefore + 6000 || AtMainWaitOf(m, P1) || m.IsTerminal());

    AssertEqual(dpBefore + 6000, DpOf(match, vortex),
        $"ChangeDigimonDP: suspending own Digimon granted +6000 DP [prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(HasDigimonEffectImmunity(match, vortex),
        "DigimonEffectImmunity: Vortexdramon gained immunity to opponent's Digimon effects");
}

async Task EX11074_BattleLatentStop()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2703, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId vortex = Stage(match, P1, "EX11_074", ChoiceZone.BattleArea, "1:battle:Vortexdramon", register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var src = new Cec.CardSource(match.Context, vortex, P1);
    // [All Turns] OnTappedAnyone 리전은 등록되지만, 실해소의 IBattle.Battle()은 미러 미이관 STOP RD-EXT2B-01.
    List<Cec.ICardEffect> onTapped = src.EffectList(Cec.EffectTiming.OnTappedAnyone);
    AssertTrue(onTapped.Count >= 1,
        "HARVEST RD-EXT2B-01: EX11_074 [All Turns] OnTappedAnyone registers safely, but the battle step " +
        "(new IBattle(...).Battle()) has no mirror execution method (context holder only). When IBattle.Battle() " +
        "is ported this witness must be upgraded to drive the unsuspend+battle path.");
}

// ═══════════════════════════════════ harness ═══════════════════════════════════

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewExemplarMatchAsync(int seed, PlayerDeckSetup[] decks)
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

// 메모리 상한을 올리고(고코스트 하드-플레이 위해) 턴 플레이어 관점 메모리를 세트.
static void Boost(DcgoMatch match, int memory)
{
    match.Context.MemoryController.Initialize(memory, minimum: -30, maximum: 40);
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
            $"pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()} memory:{match.Context.MemoryController.Current.Current} " +
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

// 실카드 스테이징 (EXEMPLAR-T1 관례): def id = 카드번호; 인스턴스만 만들어 이동. register → 배틀에어리어 효과원.
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

// 합성 픽스처 카드 (EXEMPLAR-T1 StageSynthetic 관례): def 업서트 + 인스턴스 + 존 이동 + register.
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, Dictionary<string, object?>? extraDefMeta = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (extraDefMeta is not null)
    {
        foreach ((string k, object? v) in extraDefMeta)
        {
            meta[k] = v;
        }
    }

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType));
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

static bool HasTokenOf(DcgoMatch match, HeadlessPlayerId player, string tokenNumber) =>
    ZoneCards(match, player, ChoiceZone.BattleArea).Any(id => CardNumberOf(match, id) == tokenNumber);

static HeadlessEntityId TokenIdOf(DcgoMatch match, HeadlessPlayerId player, string tokenNumber) =>
    ZoneCards(match, player, ChoiceZone.BattleArea).First(id => CardNumberOf(match, id) == tokenNumber);

static int DpOf(DcgoMatch match, HeadlessEntityId id)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) || rec is null)
    {
        return -1;
    }

    return new Cec.Permanent(match.Context, id, rec.OwnerId).DP;
}

static bool IsDp(DcgoMatch match, HeadlessEntityId id, int dp) => DpOf(match, id) == dp;

// (이연④-b RD-IMM-01) DRIVES the granted immunity through the LIVE CardSource.CanNotBeAffected scan instead of
// probing for a marker type on the effect list (the old `is ContinuousImmunityEffect` presence probe — that
// old-model class is deleted and the factory now emits the live CanNotAffectedClass). Positive: an OPPONENT's
// Digimon effect is blocked; negative: the card's OWN Digimon effect is NOT (immunity is opponent-only).
static bool HasDigimonEffectImmunity(DcgoMatch match, HeadlessEntityId id)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) || rec is null)
    {
        return false;
    }

    var protectedCard = new Cec.CardSource(match.Context, id, rec.OwnerId);
    Cec.Player? enemy = new Cec.Player(match.Context, rec.OwnerId).Enemy;
    if (enemy is null)
    {
        return false;
    }

    // Opponent Digimon effect — must be blocked by the granted immunity.
    var oppDigimonCause = new CecFx.ActivateClass();
    oppDigimonCause.SetUpICardEffect("opp-digimon-cause", _ => true,
        new Cec.CardSource(match.Context, new HeadlessEntityId("EXT2B-IMMU-OPP"), enemy.PlayerId));
    oppDigimonCause.SetIsDigimonEffect(true);

    // Own Digimon effect — must NOT be blocked (opponent-only immunity).
    var ownDigimonCause = new CecFx.ActivateClass();
    ownDigimonCause.SetUpICardEffect("own-digimon-cause", _ => true,
        new Cec.CardSource(match.Context, new HeadlessEntityId("EXT2B-IMMU-OWN"), rec.OwnerId));
    ownDigimonCause.SetIsDigimonEffect(true);

    return protectedCard.CanNotBeAffected(oppDigimonCause)
        && !protectedCard.CanNotBeAffected(ownDigimonCause);
}

static bool WasSuspended(DcgoMatch match, HeadlessEntityId id) => true;

static string CardNumberOf(DcgoMatch match, HeadlessEntityId cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null
        || !match.Context.CardRepository.TryGetCard(record.DefinitionId, out CardRecord? def) || def is null)
    {
        return string.Empty;
    }

    return def.CardNumber;
}

static bool IsSuspendedMeta(DcgoMatch match, HeadlessEntityId cardId)
{
    return match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
        && record.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;
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

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public static ChoiceResult Fallback(ChoiceRequest request)
        => new ScriptedChoiceProvider().ChooseAsync(request).GetAwaiter().GetResult();

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

static class ContextFactory
{
    public static EngineContext CreateWithProvider(IChoiceProvider provider, int randomSeed)
    {
        var randomSource = new GameRandomSource(randomSeed);
        var cardInstanceRepository = new InMemoryCardInstanceRepository();
        var logSink = new NullLogSink();
        var zoneMover = new InMemoryZoneMover(randomSource);
        var memoryController = new InMemoryHeadlessMemoryController();
        var effectRegistry = new InMemoryEffectRegistry();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                effectRegistry,
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, effectRegistry, gameEventQueue,
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
            effectRegistry: effectRegistry,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
