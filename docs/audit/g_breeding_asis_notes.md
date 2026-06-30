# GR-002 육성(Breeding) 룰 — 원본 DCGO 대조 메모

> AS-IS 미러 규칙에 따라 구현 전 원본 `DCGO/` Unity 소스에서 육성 규칙을 확인한 기록. 추측 없이 원본 코드를 근거로 함.

## 원본 규칙 (출처)

**`DCGO/Assets/Scripts/Script/Player.cs`**
```csharp
// 부화: 디지타마 1장 이상 AND 육성칸이 비어 있을 때만
public bool CanHatch => DigitamaLibraryCards.Count >= 1 && GetBreedingAreaPermanents().Count == 0;     // :1168
// 이동: 육성칸에 '움직일 수 있는' 퍼머넌트가 있고 + 빈 배틀칸이 1개 이상
public bool CanMove  => GetBreedingAreaPermanents().Count(p => p.CanMove) >= 1 && (빈 배틀칸 >= 1);      // :1172
```

**`DCGO/Assets/Scripts/Script/Permanent.cs` — `CanMove` (:2010–2078)**
이동 가능 핵심 조건:
```csharp
if (!IsDigimon) return false;                      // :2068  디지몬이어야 함
if (TopCard.IsDigiEgg && DP <= 0) return false;    // :2071  DP<=0 디지에그는 이동 불가
// + ICanNotMoveEffect (효과), 빈 배틀칸
```
`IsDigimon => CardKinds.Contains(CardKind.Digimon)` (`CardSource.cs:3460`) — **TopCard 기준**.

## 핵심 결론

1. **이동 대상은 Digimon이어야 한다.** 갓 부화한 lv2 디지에그(`IsDigiEgg && DP<=0`)는 이동 불가. → 육성칸에서 lv3로 **진화한 뒤에야** 이동.
2. **"부화/이동 턴 동시 금지" 같은 턴 플래그는 원본에 없다.** 상호배제는 순수 상태 기반(부화=빈 육성칸 / 이동=움직일 수 있는 디지몬). 따라서 *기존 lv3를 내보낸 뒤 새 알을 부화*하는 "이동→부화"는 **원본에서 합법**. → 제 초기 감사의 `BREED_HATCH_AND_MOVE_SAME_TURN` 체크는 원본과 어긋난 과한 판정이라 **제거**함.

## 헤드리스 적용 (GR-002 구현)

- `HeadlessLegalActionDispatcher.BuildBreedingActions`: `MoveBreedingToBattle`을 **육성칸 top 카드가 Digimon일 때만** 제시(`IsMovableBreedingDigimon` = CardType=="Digimon", + DP<=0 디지에그 제외). 단일-CardType 모델이라 `CardType=="Digimon"`이 `IsDigimon`에 대응.
- enforcement은 합법-액션 생성 + `LegalActionSetValidator`(crafted 액션 차단)로 이중화.
- 감사(`tools/RuleAudit`): `BREED_MOVE_NOT_DIGIMON`·`EGG_IN_BATTLE` = 0. `EGG_IN_BATTLE`는 **DigiEgg 타입만** 검사하도록 수정(테이머/옵션은 레벨 0이지만 배틀 상주가 합법 → 오탐 제거).
- 테스트: `tests/GR-002.BreedingMove.Tests`(알 이동불가 / 디지몬 이동가능) + `G2A-003` 갱신(육성 이동은 lv3 디지몬으로).

## ✅ 육성 내 진화 — 해소 (GR-004)

**이전 갭:** `DigivolveAction.GetLegalActions`가 진화 타겟을 `BattleArea`에서만 찾아 육성칸 디지에그를 lv3로 키우는 경로가 없었음(이동 게이트만 막으면 육성이 막다른 길).

**원본 확인:** 육성칸 디지에그 프레임은 유효한 진화 타겟(`CardController.cs:1291-1303` `isBreedingArea` 분기 + `IsExistOnBreedingAreaDigimon`). 데이터상으로도 lv3 카드가 `evolutionConditions:[{color, level:2, cost:0}]`를 가져 에그(lv2)→lv3 진화 조건(`색@2`)이 매칭됨.

**구현(GR-004) — `DigivolveAction`:**
1. `GetLegalActions`: 진화 타겟을 `BattleArea` + **`BreedingArea`** 양쪽에서 탐색.
2. `Validate`: 타겟이 배틀존 **또는 육성존**에 있으면 합법.
3. `ProcessAsync`: 진화를 **제자리(in-place)**로 — 새 top 카드가 타겟이 있던 존에 그대로 안착(`targetZone`). 육성 진화 결과는 육성칸에 유지(이전엔 Hand→BattleArea로 하드코딩돼 배틀로 텔레포트됐을 것).

**검증:**
- `tests/GR-004.BreedingDigivolve`: 부화 → 육성 진화(에그→lv3, 결과 육성칸 유지) → lv3 이동까지 풀 램프 단언.
- 감사·GR-003 게이트 위반 0(소환멀미 포함 — 육성→배틀 이동 lv3 공격 정상).
- 랜덤 self-play에서 라이브 발생 확인(육성-내-진화 4회 + 이동 3회).

→ 육성 메커닉(부화→육성 진화→이동) **완전 작동**. 전체 게이트 230/230.
