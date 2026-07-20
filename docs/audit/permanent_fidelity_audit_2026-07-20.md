# 미러 `Permanent` 효과-접기 충실도 전수 감사

- 날짜: 2026-07-20
- 범위: read-only (코드 수정 없음). grep 전량 `--binary-files=text`.
- 대상: 미러 `src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs` (4,519줄) vs AS-IS `DCGO/Assets/Scripts/Script/Permanent.cs` (4,187줄).
- 목적: 미러 `Permanent` **클래스 프로퍼티/게터**가 AS-IS처럼 **부여·지속 효과를 접는지**(base/metadata만 읽지 않는지)를 게터 단위로 전수 판정. 선행 감사 `idhelper_coverage_audit_2026-07-20.md`는 경로 A id-헬퍼 5종만 검사했고, `Permanent` 클래스 게터 자체는 전수하지 않았음.

## 방법

1. AS-IS `Permanent.cs` 게터가 스캔-접기 하는 효과 인터페이스를 grep 수집 (`I[A-Z]…Effect`, 빈도순): `IChangeDPEffect`(30) · `IChangeSAttackEffect`(18) · `IChangeLinkMaxEffect`(14) · `IChangeBaseDPEffect`(12) · `ICanNotBeDestroyedByBattleEffect`(10) · `ITreatAsDigimonEffect`·`IRushEffect`·`IRebootEffect`·`ICollisionEffect`·`ICanNotMoveEffect`·`IBlockerEffect`(각6) · `IChangePermanentLevelEffect`(4) 외 30여 종.
2. 브레이스-매칭 스크립트로 두 파일의 public 멤버 게터 본문을 추출, 각 게터가 참조하는 효과 인터페이스 집합 + `EffectList(EffectTiming…)` 순회-스캔 여부를 판정해 조인.
3. 순수-스캔(EffectName 매칭) 키워드 게터(HasPierce/HasAlliance 등)는 인터페이스가 없으므로 `foreach … in … EffectList` 순회 여부로 접기 판정.
4. commons id-헬퍼(`LevelOf`·`CurrentDp`·`IsSuspended` 등)가 미러 폴딩 게터에 위임하는지 base 직접 읽는지 대조 — LevelOf 외 추가 우회 탐색.

## 핵심 결과

**미러 `Permanent` 클래스의 효과-접기 게터는 전부 충실(3a).** AS-IS가 효과를 접는 게터 중, 미러에서 base/metadata만 읽어 효과를 놓치는 클래스 게터는 **0건**. AS-IS 대비 미러 게터가 없는 4종은 (3종=충실 재하우징, 1종=휴면 미포팅)으로 분해되며, 그중 활성 충실도 결함은 0.

즉 **잠복 3b는 미러 `Permanent` 클래스 게터층에는 없고, commons id-헬퍼층의 `LevelOf` 1건(선행 감사 등재)뿐이다.** 이번 감사는 클래스 게터층이 깨끗함을 확증하고, id-헬퍼 3b가 `LevelOf` 단일임을(추가 우회 없음) 재확인한다.

---

## 효과-접기 대상 게터 판정표 (미러 클래스 게터)

범례: 3a=미러 게터가 AS-IS처럼 효과 접음 / 무관=효과로 안 바뀌는 순수 상태·구조.

| 게터 | 접는 효과 IF | AS-IS | 미러 | 판정 |
|---|---|---|---|:---:|
| `DP` / `GetDP` | IChangeDPEffect (+ImmuneFromDPMinus 게이트) | 접음 | :376/:197 접음 | 3a |
| `BaseDP` | IChangeBaseDPEffect | 접음 | :2181 접음 | 3a |
| `HasDP` | IDontHaveDPEffect | 접음 | :141 접음 | 3a |
| `Level` | IChangePermanentLevelEffect | 접음 | :558 접음 | 3a |
| `SecurityAttackChanges` | IChangeSAttackEffect | 접음 | :2486 접음 | 3a |
| `Strike_AllowMinus` | IChangeSAttackEffect | 접음 | :2583 접음 | 3a |
| `InvertSecutiryValue` | IInvertSAttackEffect | 접음 | :2426 접음 | 3a |
| `ImmuneFromDPMinus` | IImmuneFromDPMinusEffect | 접음 | :2379 접음 | 3a |
| `LinkedMax` | IChangeLinkMaxEffect | 접음 | :2780 → `LinkHelpers.ResolveLinkedMax` → `NewModelContinuousScan.FoldLinkedMax`(LinkHelpers.cs:79) 접음 | 3a |
| `HasBlocker` | IBlockerEffect (+Collision 게이트) | 접음 | :818 접음 | 3a |
| `IsUnblockable` / `CanBlock` | ICannotBlockEffect | 접음 | :794/:3193 접음 | 3a |
| `HasJamming` | ICanNotBeDestroyedByBattleEffect(name="Jamming") | 접음 | :910 접음 | 3a |
| `HasIceclad` | IIcecladEffect | 접음 | :967 접음 | 3a |
| `HasReboot` | IRebootEffect | 접음 | :1048 접음 | 3a |
| `HasRush` | IRushEffect | 접음 | :1143 접음 | 3a |
| `HasCollision` | ICollisionEffect | 접음 | :1495 접음 | 3a |
| `HasPierce`·`HasRaid`·`HasAscension`·`HasFortitude`·`HasBlitz`·`HasEvade`·`HasMindLink`·`HasBarrier`·`HasAlliance`·`HasPartition`·`HasScapegoat` | ActivateICardEffect + EffectName 순회-스캔 | 접음 | :1014·:1120·:1258·:1283·:1310·:1338·:1368·:1396·:1427·:1566·:1588 순회-스캔 | 3a |
| `RetaliationCount`·`HasOnDeletionEffect` | EffectName/이펙트 순회 | 접음 | :1231·:1610 순회 | 3a |
| `IsDigimon` | ITreatAsDigimonEffect | 접음 | :618 접음 | 3a |
| `CanMove` | ICanNotMoveEffect | 접음 | :3073 접음 | 3a |
| `CanSuspend` | ICanNotSuspendEffect | 접음 | :1817 접음 | 3a |
| `CanUnsuspend` | ICanNotUnsuspendEffect | 접음 | :3022 접음 | 3a |
| `CanAttack` / `CanAttackTargetDigimon` | ICan(Not)AttackTargetDefendingPermanentEffect | 접음 | :3157/:3285 접음 | 3a |
| `CanSwitchAttackTarget` | ICanNotSwitchAttackTargetEffect | 접음 | :3745 접음 | 3a |
| `CanBeDestroyed` | ICanNotBeDestroyedEffect | 접음 | :2801 접음 | 3a |
| `CanBeDestroyedByBattle` | ICanNotBeDestroyedByBattleEffect | 접음 | :3449 접음 | 3a |
| `CanBeDestroyedBySkill` | ICanNotBeDestroyedBySkillEffect | 접음 | :3528 접음 | 3a |
| `CanBeRemoved` | ICanNotBeRemovedEffect | 접음 | :3591 접음 | 3a |
| `CanSelectBySkill` | ICanNotSelectBySkillEffect | 접음 | :2851 접음 | 3a |
| `CannotReturnToHand` | ICannotReturnToHandEffect | 접음 | :743 접음 | 3a |
| `ImmuneFromDeDigivolve` | IImmuneFromDeDigivolveEffect | 접음 | :2884 접음 | 3a |
| `ImmuneFromStackTrashing` | IImmuneFromStackTrashingEffect | 접음 | :2918 접음 | 3a |
| `CanSubstituteForDigiXrosCondition` | ICanSelectDigiXrosEffect | 접음 | :3644 접음 | 3a |
| `CanSubstituteForAssemblyCondition` | ICanSelectAssemblyEffect | 접음 | :3695 접음 | 3a |
| `CanDeclareSkill(List)` | ICardEffect 순회 | 접음 | :2962 순회 | 3a |
| `IsToken`·`IsTamer`·`IsOption`·`IsSuspended`·`oldIsTapped_playCard`·`IsDestroyedByBattle`·`willBeRemoveField`·`DigivolutionCards`·`cardSources`·`StackCards`·`Level*/DP*/Cost*JustBefore/After…` | — | 접기 없음 | 접기 없음 | 무관 |

### 미러 클래스 게터에 없는(재하우징/미포팅) AS-IS 폴딩 게터 4종

| AS-IS 게터 | 접는 효과 IF | 미러 소재 | 판정 |
|---|---|---|:---:|
| `Levels_ForJogress(CardSource)` (:3554) | IAddJogressLevelsEffect | `CardSource.Levels_ForJogress` (CardSource.cs:955-990) — 동일 인터페이스 순회-접기 | **3a (충실 재하우징)** |
| `LinkedMax` (:896) | IChangeLinkMaxEffect | `LinkHelpers.ResolveLinkedMax` → `FoldLinkedMax` (NewModelContinuousScan.cs) | **3a (충실 재하우징)** |
| `CannotReturnToLibrary(ICardEffect)` (:785) | ICannotReturnToLibraryEffect | `NewModelContinuousScan.HasCannotReturnToLibrary` (:1720-1745) — 순회-접기 | **3a (충실 재하우징)** |
| `Names_ForDNA(CardSource)` (:3611) | IAddDNANamesEffect | **미포팅** (인터페이스 정의만 CardEffectInterfaces.cs에 존재, 게터·소비자 전무) | **휴면 (아래)** |

---

## 3b(충실도 결함) 목록 = 수리 원장

### 미러 `Permanent` 클래스 게터층: **활성 3b = 0건**

효과-접기 대상 게터 전부가 미러에서 접기를 수행. base-only로 회귀한 클래스 게터 없음.

### commons id-헬퍼층: **3b = 1건 (선행 감사 기등재, 추가 우회 없음)**

| 항목 | 유형 | 근본 원인 | 발화 등급 |
|---|---|---|---|
| `CardEffectCommons.LevelOf(card,id)` (:4753) + 파생 `TopCardHasLevel` (:4749) | **게터 우회** | `Permanent.Level`(효과 접음)을 우회, `instance/def.Metadata`의 `level` 키만 읽어 `IChangePermanentLevelEffect` 미접기 | **포팅 시 발화 확정** — 미러 카드 4장이 `LevelOf(` 사용 중(ST2_03·BT1_068·ST4_01·BT2_095 계열); 레벨 변경 카드 포팅 순간 발화 |

**추가 우회 없음 확인**: 레벨/코스트 극값 술어 `IsMinLevel`·`IsMaxLevel`(:4247, `IsLevelExtremum` 경유)·`IsMinLevelBoard`(:3977)는 전부 **폴딩 게터 `permanent.Level`을 사용**(base LevelOf 아님). DP 극값 술어 `IsDpExtremum`도 `permanent.BaseDP`/`TopCard.HasDP`(폴딩) 사용. `CurrentDp`(:4561)는 `Permanent.DP` 위임(3a). `IsSuspended`(:4320)·`HasNoDigivolutionCards`·`HasCannotReturnToLibrary`는 접을 효과 없는 순수 상태/스캔(3a). → **id-헬퍼 3b는 `LevelOf` 단일.**

### 별도 — `Names_ForDNA` 미포팅 (3b 아님, 휴면)

- AS-IS에서 `IAddDNANamesEffect`를 구현하는 카드 **0장**(`DCGO/Assets/Scripts/CardEffect/` 전수 0건), `Names_ForDNA` 소비자 **0건**(Permanent.cs 정의부 외 참조 없음) — AS-IS에서도 미발화 휴면 기능.
- 미러엔 게터 자체가 없어 "base 읽기"조차 없음(우회할 소비자 부재). 발화 등급 = **이론적/휴면**(최저). 미러 소비자가 생기기 전엔 3b 자격 미성립. 향후 IAddDNANamesEffect 카드 포팅 시 게터 신설 필요 정도로만 기록.

---

## 결론 — 내일 수리 스코프

- **미러 `Permanent` 클래스 게터층 = 클린.** 효과-접기 게터 전부 3a. 이 층 수리 대상 **0건**. (선행 감사가 우려한 "클래스 프로퍼티가 효과를 접는가" 질문의 답 = **전부 접는다**.)
- **수리 원장 3b = 1건**, 전부 id-헬퍼층: **`LevelOf`(+`TopCardHasLevel`)**.
  - 수리 방향: `LevelOf`를 `CurrentDp`처럼 `new Permanent(card.Context, id, owner).Level` 위임으로 re-point. **caveat(선행 감사 인용)**: `Permanent.Level`은 no-level에 -1 sentinel, `LevelOf`는 unknown에 0 반환 + `TopCardHasLevel = LevelOf>0`. sentinel(-1 vs 0) 화해 필요 → 자명 re-point 아님, 강모델 수리.
  - 발화 등급: 포팅 시 발화 확정(카드 4장 이미 소비). 우선순위 최상.
- **휴면 기록 1건**: `Names_ForDNA`/`IAddDNANamesEffect` — AS-IS도 카드 0장·소비자 0. 게터 미포팅 상태 유지, IAddDNANamesEffect 카드 포팅 착수 시 CardSource-경유 폴딩 게터 신설(Levels_ForJogress 선례) 필요. 지금 수리 대상 아님.

> 요약: 이번 전수는 **미러 Permanent 클래스 게터의 효과-접기 충실도가 완전함**을 확증. 잠복 충실도 결함은 클래스 게터가 아니라 그를 우회하는 commons id-헬퍼 `LevelOf` 단일이며, 이는 선행 감사와 동일 결론(추가 우회 0). 내일 수리 = LevelOf re-point 1건.
