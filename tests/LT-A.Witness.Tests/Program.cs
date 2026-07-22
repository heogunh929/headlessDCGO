using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Cfx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using Script = HeadlessDCGO.Engine.Assets.Scripts.Script;
using CBT2051 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Green.BT2_051;
using CAD1012 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Blue.AD1_012;
using CBT16033 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT16.Yellow.BT16_033;
using CBT15078 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.Purple.BT15_078;
using CBT25043 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Yellow.BT25_043;
using CBT15102 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.White.BT15_102;
using CBT19061 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Black.BT19_061;
using CEX8026 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Blue.EX8_026;
using CBT25096 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Blue.BT25_096;
using CBT25034 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Yellow.BT25_034;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// LT-A witness 스위트 — coverage-exemplar 트랜치 10장(포팅 성공분), 카드당 1개 이상.
// 각 subtest는 그 카드가 set-cover에서 선정된 PRIMARY covered element을 직접 구동한다:
//   kind-class/fold 카드는 공개 behavior 메서드(술어/fold)를 pos+neg 대조로 평가하고,
//   ActivateClass/키워드 카드는 해당 timing에서 covered 효과가 생산되는지 + CanActivate/CanUse 게이트/연관 fold를 구동한다.
// 템플릿: PILOT-S3.Witness.Tests(DcgoMatch.CreatePumpDriven + 합성/실 카드 스테이징 + AmbientMatchContext).
// (STOP: BT7_087 — IsPlaceToTrashDueToNotHavingDP write-surface 부재로 tranche에서 STOP, 이 스위트 제외.)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT2_051 W1 (CanAttackTargetDefendingPermanent): 자기+녹색테이머 게이트 → 서스펜드 안 된 상대 디지몬 공격대상 지정 TRUE, 서스펜드 상대는 FALSE(대조)", BT2051_CanAttackUnsuspendedDefender),
    ("AD1_012 W1 (AddAssemblyCondition/CanNotSwitchAttackTarget): GetAssemblyCondition reduceCost=4·2원소 술어 + CanNotBeSwitchAttackTarget(self)=TRUE/타 퍼머넌트 FALSE", AD1012_AssemblyAndCanNotSwitch),
    ("BT16_033 W1 (ArmorPurge/OnSecurityCheck): OnSecurityCheck ActivateClass가 sec≥3에서 CanActivate=TRUE(메모리), 보드 공백에서 FALSE + ArmorPurge가 WhenPermanentWouldBeDeleted에 생산", BT16033_SecurityCheckGateAndArmorPurge),
    ("BT15_078 W1 (AddDetail/AddSkill): OnEnterFieldAnyone 부여 효과 생산·CanActivate(배틀 존재) TRUE + PierceSelfEffect(OnDestroyedAnyone) 생산", BT15078_AddSkillEffectProducedAndActivatable),
    ("BT25_043 W1 (ArtsDigivolve/TrashSecurity): ArtsDigivolve 효과(None) 생산 + Shared WD/WA ActivateClass가 WhenDigivolving/OnAllyAttack에 생산", BT25043_ArtsDigivolveAndSharedEffects),
    ("BT15_102 W1 (SelectDigiXros): BeforePayCost 'Placing 1 [Dark Masters]…' ActivateClass 생산 + [Dark Masters] 트래시 존재 시 CanActivate=TRUE, 부재 시 FALSE(대조)", BT15102_BeforePayCostSelectDigiXrosGate),
    ("BT19_061 W1 (ChangeCardNamesForDigiXros): ChangeCardNamesForDigiXros fold — 자기 카드에 'Sparrowmon' 추가 TRUE, 타 카드는 미변경(대조)", BT19061_ChangeCardNamesForDigiXrosFold),
    ("EX8_026 W1 (CanNotSuspendClass): CanNotSuspend(상대 디지몬)=TRUE·CanNotSuspend(자기 디지몬)=FALSE(대조) + CanUse 메모리≥1 게이트", EX8026_CanNotSuspendPredicateAndMemoryGate),
    ("BT25_096 W1 (TrashDigivolutionCards): HasFaceDownDigivolutionCards fold — face-down 진화원 테이머 TRUE·미-flip 테이머 FALSE(대조) + BeforePayCost ActivateClass 생산", BT25096_FaceDownFoldAndBeforePayCost),
    ("BT25_034 W1 (Ascension): Ascension 효과(OnDestroyedAnyone, name=='Ascension') 생산 + Barrier(WhenPermanentWouldBeDeleted) + When-Trashed 무료플레이(OnDiscardSecurity) ActivateClass 생산", BT25034_AscensionProducedAndWhenTrashedGate),
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

// ═══════════════════════════════════ BT2_051 ═══════════════════════════════════

async Task BT2051_CanAttackUnsuspendedDefender()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7101);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "BT2_051", ChoiceZone.BattleArea, "1:battle:BT2051", register: true);
    StageSynthetic(match, P1, "EXT-GTAMER", dp: 0, level: 0, "1:battle:gtamer", cardType: "Tamer", colors: new[] { "Green" });
    HeadlessEntityId oppUnsusp = StageSynthetic(match, P2, "EXT-OPPU", dp: 4000, level: 4, "2:battle:oppu");
    HeadlessEntityId oppSusp = StageSynthetic(match, P2, "EXT-OPPS", dp: 4000, level: 4, "2:battle:opps");
    SetSuspended(match, oppSusp, true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);
    var kc = (Cfx.CanAttackTargetDefendingPermanentClass)First(new CBT2051().CardEffects(Cec.EffectTiming.None, card), "CanAttackTargetDefendingPermanentClass");

    AssertTrue(kc.CanUse(new Hashtable()), "CanUse TRUE: own turn + a Green Tamer is on the owner's battle area");
    AssertTrue(kc.CanAttackTargetDefendingPermanent(Perm(match, self, P1), Perm(match, oppUnsusp, P2), kc),
        "this Digimon MAY attack an UNSUSPENDED opponent Digimon (the covered CanAttackTargetDefendingPermanent element)");
    AssertTrue(!kc.CanAttackTargetDefendingPermanent(Perm(match, self, P1), Perm(match, oppSusp, P2), kc),
        "control: a SUSPENDED opponent Digimon is not opened up (defenderCondition requires !IsSuspended)");
}

// ═══════════════════════════════════ AD1_012 ═══════════════════════════════════

async Task AD1012_AssemblyAndCanNotSwitch()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7201);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "AD1_012", ChoiceZone.BattleArea, "1:battle:AD1012", register: true);
    HeadlessEntityId sag = StageSynthetic(match, P1, "EXT-SAG", dp: 6000, level: 6, "1:hand:sag", name: "WereGarurumon: Sagittarius Mode", zone: ChoiceZone.Hand);
    HeadlessEntityId garu = StageSynthetic(match, P1, "EXT-GARU", dp: 4000, level: 4, "1:hand:garu", name: "Garurumon", zone: ChoiceZone.Hand);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);
    List<Cec.ICardEffect> noneEffects = new CAD1012().CardEffects(Cec.EffectTiming.None, card);

    var assembly = (Cfx.AddAssemblyConditionClass)First(noneEffects, "AddAssemblyConditionClass");
    Cec.AssemblyCondition? cond = assembly.GetAssemblyCondition(card);
    AssertTrue(cond is not null, "AddAssemblyCondition produces a real AssemblyCondition for this card");
    AssertEqual(4, cond!.reduceCost, "the Assembly reduces cost by 4");
    AssertEqual(2, cond.elements.Count, "the Assembly has 2 elements ([WereGarurumon: Sagittarius Mode] + [Garurumon])");

    var sagCard = new Cec.CardSource(match.Context, sag, P1);
    var garuCard = new Cec.CardSource(match.Context, garu, P1);
    AssertTrue(cond.elements[0].CardCondition(sagCard), "element 1 matches the [WereGarurumon: Sagittarius Mode] card");
    AssertTrue(cond.elements[1].CardCondition(garuCard), "element 2 matches the [Garurumon] card");
    AssertTrue(!cond.elements[0].CardCondition(garuCard), "control: element 1 does NOT match [Garurumon]");

    var canNotSwitch = (Cfx.CanNotSwitchAttackTargetClass)First(noneEffects, "CanNotSwitchAttackTargetClass");
    AssertTrue(canNotSwitch.CanNotBeSwitchAttackTarget(Perm(match, self, P1)),
        "this Digimon's attack target can't be changed (the covered CanNotSwitchAttackTarget element)");
    HeadlessEntityId other = StageSynthetic(match, P1, "EXT-OTHER012", dp: 3000, level: 3, "1:battle:other012");
    AssertTrue(!canNotSwitch.CanNotBeSwitchAttackTarget(Perm(match, other, P1)),
        "control: a different permanent is unaffected");
}

// ═══════════════════════════════════ BT16_033 ═══════════════════════════════════

async Task BT16033_SecurityCheckGateAndArmorPurge()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7301);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "BT16_033", ChoiceZone.BattleArea, "1:battle:BT16033", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);

    // ArmorPurge (a covered element) is produced at WhenPermanentWouldBeDeleted while on the battle area.
    var armorPurge = First(new CBT16033().CardEffects(Cec.EffectTiming.WhenPermanentWouldBeDeleted, card), "ActivateClass");
    AssertEqual("Armor Purge", armorPurge.EffectName, "ArmorPurge keyword effect is produced at WhenPermanentWouldBeDeleted");

    // OnSecurityCheck (the covered timing): the [Your Turn] "gain memory (sec>=3) / recover (sec<=2)" ActivateClass.
    for (int i = 0; i < 3; i++)
    {
        StageSynthetic(match, P1, "EXT-SEC" + i, dp: 0, level: 0, "1:sec:bt16-" + i, zone: ChoiceZone.Security);
    }

    var onSecCheck = First(new CBT16033().CardEffects(Cec.EffectTiming.OnSecurityCheck, card), "ActivateClass");
    AssertTrue(onSecCheck.CanActivate(new Hashtable()),
        "with 3 security cards the memory-gain branch is legal (CanActivate TRUE — the OnSecurityCheck covered element)");

    // Control: once this Digimon leaves the battle area the OnSecurityCheck effect can no longer activate.
    match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.BattleArea, ChoiceZone.Trash)).GetAwaiter().GetResult();
    var onSecCheckOff = First(new CBT16033().CardEffects(Cec.EffectTiming.OnSecurityCheck, card), "ActivateClass");
    AssertTrue(!onSecCheckOff.CanActivate(new Hashtable()),
        "control: off the battle area the IsExistOnBattleArea gate closes (CanActivate FALSE)");
}

// ═══════════════════════════════════ BT15_078 ═══════════════════════════════════

async Task BT15078_AddSkillEffectProducedAndActivatable()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7401);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "BT15_078", ChoiceZone.BattleArea, "1:battle:BT15078", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);

    // [All Turns] "when an effect plays an opponent Digimon → grant [On Deletion] Lose 1 memory" (AddSkill/AddDetail).
    var grant = First(new CBT15078().CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card), "ActivateClass");
    AssertEqual("All Opponents digimon, gain Memory -1", grant.EffectName, "the AddSkill/AddDetail granting effect is produced at OnEnterFieldAnyone");
    AssertTrue(grant.CanActivate(new Hashtable()), "CanActivate TRUE while BT15_078 is on the battle area");

    var pierce = First(new CBT15078().CardEffects(Cec.EffectTiming.OnDestroyedAnyone, card), "ActivateClass");
    AssertTrue(pierce is not null, "the inherited [Pierce] effect is produced at OnDestroyedAnyone");
}

// ═══════════════════════════════════ BT25_043 ═══════════════════════════════════

async Task BT25043_ArtsDigivolveAndSharedEffects()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7501);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "BT25_043", ChoiceZone.BattleArea, "1:battle:BT25043", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);

    // ArtsDigivolve keyword (the covered element) lives on the None arm as an OptionResolutionClass.
    List<Cec.ICardEffect> noneEffects = new CBT25043().CardEffects(Cec.EffectTiming.None, card);
    AssertTrue(noneEffects.Any(e => e.GetType().Name == "OptionResolutionClass"),
        "ArtsDigivolveEffect is produced on the None arm (the covered ArtsDigivolve element)");

    // Shared WD/WA (TrashSecurityAndProcessAccordingToResult) is wired via CardEffectFactory
    // .ActivateClassesForSharedEffects, whose [When Digivolving] arm materialises on OnEnterFieldAnyone
    // (factory dialect, CardEffectFactory.cs:1819) and whose [When Attacking] arm on OnAllyAttack.
    List<Cec.ICardEffect> wd = new CBT25043().CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    List<Cec.ICardEffect> wa = new CBT25043().CardEffects(Cec.EffectTiming.OnAllyAttack, card);
    AssertTrue(wd.Any(e => e is not null && e.EffectName.Contains("unsuspend")), "the shared [When Digivolving] recover+trash-security-unsuspend ActivateClass is produced (OnEnterFieldAnyone)");
    AssertTrue(wa.Any(e => e is not null && e.EffectName.Contains("unsuspend")), "the shared [When Attacking] variant is produced on OnAllyAttack");
}

// ═══════════════════════════════════ BT15_102 ═══════════════════════════════════

async Task BT15102_BeforePayCostSelectDigiXrosGate()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7601);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "BT15_102", ChoiceZone.Hand, "1:hand:BT15102", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);

    var beforePay = First(new CBT15102().CardEffects(Cec.EffectTiming.BeforePayCost, card), "ActivateClass");
    AssertEqual("Placing 1 [Dark Masters] to get Play Cost -4", beforePay.EffectName,
        "the SelectDigiXros cost-reduction ActivateClass is produced at BeforePayCost (the covered SelectDigiXros element)");
    AssertTrue(!beforePay.CanActivate(new Hashtable()),
        "control: with no [Dark Masters] in trash/battle the reduction cannot activate");

    StageSynthetic(match, P1, "EXT-DM", dp: 11000, level: 6, "1:trash:dm", zone: ChoiceZone.Trash, traits: new[] { "Dark Masters" });
    AssertTrue(beforePay.CanActivate(new Hashtable()),
        "with a [Dark Masters] card in the trash the placement-for-cost-reduction becomes legal (CanActivate TRUE)");
}

// ═══════════════════════════════════ BT19_061 ═══════════════════════════════════

async Task BT19061_ChangeCardNamesForDigiXrosFold()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7701);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "BT19_061", ChoiceZone.BattleArea, "1:battle:BT19061", register: true);
    HeadlessEntityId other = StageSynthetic(match, P1, "EXT-OTHER061", dp: 3000, level: 3, "1:battle:other061", name: "Shoutmon");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);
    var kc = (Cfx.ChangeCardNamesForDigiXrosClass)First(new CBT19061().CardEffects(Cec.EffectTiming.None, card), "ChangeCardNamesForDigiXrosClass");

    List<string> forSelf = kc.ChangeCardNamesForDigiXros(new List<string> { "Shoutmon" }, card);
    AssertTrue(forSelf.Contains("Sparrowmon"),
        "for a DigiXros this card is ALSO treated as [Sparrowmon] (the covered ChangeCardNamesForDigiXros element)");

    var otherCard = new Cec.CardSource(match.Context, other, P1);
    List<string> forOther = kc.ChangeCardNamesForDigiXros(new List<string> { "Shoutmon" }, otherCard);
    AssertTrue(!forOther.Contains("Sparrowmon"), "control: a different card's names are unchanged");
}

// ═══════════════════════════════════ EX8_026 ═══════════════════════════════════

async Task EX8026_CanNotSuspendPredicateAndMemoryGate()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7801);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "EX8_026", ChoiceZone.BattleArea, "1:battle:EX8026", register: true);
    HeadlessEntityId oppDigi = StageSynthetic(match, P2, "EXT-OPPD026", dp: 4000, level: 4, "2:battle:oppd026");
    HeadlessEntityId ownDigi = StageSynthetic(match, P1, "EXT-OWND026", dp: 4000, level: 4, "1:battle:ownd026");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);
    var kc = (Cfx.CanNotSuspendClass)First(new CEX8026().CardEffects(Cec.EffectTiming.None, card), "CanNotSuspendClass");

    AssertTrue(kc.CanNotSuspend(Perm(match, oppDigi, P2)),
        "an opponent's Digimon can't suspend (the covered CanNotSuspend element)");
    AssertTrue(!kc.CanNotSuspend(Perm(match, ownDigi, P1)),
        "control: the owner's own Digimon is unaffected");

    match.Context.MemoryController.Set(0);
    AssertTrue(!kc.CanUse(new Hashtable()), "CanUse FALSE while memory < 1");
    match.Context.MemoryController.Set(1);
    AssertTrue(kc.CanUse(new Hashtable()), "CanUse TRUE once the owner has >= 1 memory");
}

// ═══════════════════════════════════ BT25_096 ═══════════════════════════════════

async Task BT25096_FaceDownFoldAndBeforePayCost()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 7901);
    await ReachMainWaitAsync(match);

    HeadlessEntityId self = Stage(match, P1, "BT25_096", ChoiceZone.Hand, "1:hand:BT25096", register: true);

    // Tamer WITH a face-down digivolution source (the fold this card gates on).
    HeadlessEntityId tamerFace = StageSynthetic(match, P1, "EXT-TAMERF", dp: 0, level: 0, "1:battle:tamerf", cardType: "Tamer");
    HeadlessEntityId faceSrc = StageSynthetic(match, P1, "EXT-FACESRC", dp: 0, level: 0, "1:under:facesrc", zone: ChoiceZone.None);
    SetSources(match, tamerFace, faceSrc);
    SetFlipped(match, faceSrc, true);

    // Tamer WITHOUT a face-down source (control).
    HeadlessEntityId tamerPlain = StageSynthetic(match, P1, "EXT-TAMERP", dp: 0, level: 0, "1:battle:tamerp", cardType: "Tamer");
    HeadlessEntityId plainSrc = StageSynthetic(match, P1, "EXT-PLAINSRC", dp: 0, level: 0, "1:under:plainsrc", zone: ChoiceZone.None);
    SetSources(match, tamerPlain, plainSrc);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);

    AssertTrue(Perm(match, tamerFace, P1).HasFaceDownDigivolutionCards,
        "a Tamer with a face-down bottom source reports HasFaceDownDigivolutionCards TRUE (the read-side fold this card gates on)");
    AssertTrue(!Perm(match, tamerPlain, P1).HasFaceDownDigivolutionCards,
        "control: a Tamer with only a face-up source reports FALSE");

    var beforePay = First(new CBT25096().CardEffects(Cec.EffectTiming.BeforePayCost, card), "ActivateClass");
    AssertEqual("Reduce Play Cost -2", beforePay.EffectName,
        "the TrashDigivolutionCards-driven cost-reduction ActivateClass is produced at BeforePayCost");
}

// ═══════════════════════════════════ BT25_034 ═══════════════════════════════════

async Task BT25034_AscensionProducedAndWhenTrashedGate()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 8001);
    await ReachMainWaitAsync(match);

    // On the battle area so the on-permanent keyword effects resolve a real permanent.
    HeadlessEntityId self = Stage(match, P1, "BT25_034", ChoiceZone.BattleArea, "1:battle:BT25034", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, self, P1);

    var ascension = First(new CBT25034().CardEffects(Cec.EffectTiming.OnDestroyedAnyone, card), "ActivateClass");
    AssertEqual("Ascension", ascension.EffectName, "the Ascension keyword effect is produced at OnDestroyedAnyone (the covered Ascension element)");

    var barrier = First(new CBT25034().CardEffects(Cec.EffectTiming.WhenPermanentWouldBeDeleted, card), "ActivateClass");
    AssertEqual("Barrier", barrier.EffectName, "the inherited [Barrier] effect is produced at WhenPermanentWouldBeDeleted");

    // The [When Trashed] play arm is produced at OnDiscardSecurity (its free-play free-Angel/Iliad effect).
    var whenTrashed = First(new CBT25034().CardEffects(Cec.EffectTiming.OnDiscardSecurity, card), "ActivateClass");
    AssertEqual("Play 1 level 4 or lower [Angel] or [Iliad] card", whenTrashed.EffectName,
        "the [When Trashed] free-play arm is produced at OnDiscardSecurity");
}

// ═══════════════════════════════════ harness ═══════════════════════════════════

Cec.Permanent Perm(DcgoMatch match, HeadlessEntityId id, HeadlessPlayerId owner) => new(match.Context, id, owner);

static Cec.ICardEffect First(List<Cec.ICardEffect> effects, string typeName)
    => effects.Where(e => e is not null).First(e => e.GetType().Name == typeName);

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        MonoDecks("BT1_028", "BT1_028"), firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
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
            $"drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId}");
    }
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

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

// 실카드 스테이징(PILOT-S3 관례): def id = 카드번호(cards.json 로더가 넣음), 인스턴스만 만들어 이동.
HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, string instanceId, bool register = false)
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

// 합성 픽스처 카드(PILOT-S3 StageSynthetic 관례).
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

static void SetMetaFlag(DcgoMatch match, HeadlessEntityId id, string key, object? value)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [key] = value };
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static void SetSuspended(DcgoMatch match, HeadlessEntityId id, bool suspended) => SetMetaFlag(match, id, "isSuspended", suspended);

static void SetFlipped(DcgoMatch match, HeadlessEntityId id, bool flipped) => SetMetaFlag(match, id, "isFlipped", flipped);

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

// ═══════════════════════════════ providers/context (PILOT-S3 1:1) ═══════════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

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

