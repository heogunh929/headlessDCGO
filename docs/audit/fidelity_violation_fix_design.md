# 위반 조치방안 설계 + AS-IS 매칭 검증 보고서 (2026-07-02)

> **✅ 구현 완료 (같은 날, 같은 세션)**: A1(G3.5-D1 +1) · A2(G9-044 +3, ignore-플래그 폴딩 포함) · A3(G9-061 신설 5, 스텁 5종 구현) · A4(G3.5-C14 +3, **삭제시점 키워드 스냅샷** 부수 구현) · B1(F68 PRE 재작성, `ArmorPurgeTopAsync`) · B2(`AttackPhase.PiercingSecurity` 파킹, D1 +1) · B3(G3.5-D2 +3, sink Delete 라우팅+`isDpZero`) · B4(G3.5-B7 +5, RevealAndSelect v2) · B5(G3.5-CVA2 +3, `DefenderCondition`·Degenerate·조합게이트) · C1(G9-058 +1) · C2(G9-057 +1) · C3(Sacrifice 가드) · C4(Iceclad 파서) · C6(Save canSkip). probe 결과: B5 IDegeneration Lv3 플로어 = AS-IS 존재 확인(1:1). 잔여 debt는 fidelity_debt.md "위반 조치 구현" 절.

> **입력**: [fidelity_violation_audit2.md](fidelity_violation_audit2.md) (A급 4 · B급 9 · C급 10).
> **방법**: 각 항목에 대해 ① AS-IS 원본 코드 재확인(설계 세부 조사 3축 — 인용 라인 명기) → ② 포트 측 배선 지점 확정 → ③ 조치 설계 → ④ **설계 ↔ AS-IS 매칭 검증표**. 조사 근거가 부족한 결정은 내리지 않았고, 미모델 의존 항목은 STOP/debt로 분리했다.
> **공통 종료 기준**: 항목별 동작-단언 테스트(매칭/비매칭) + `bash scripts/run-tests.sh` green + `tools/RuleAudit` 0. 커밋은 지시 시.

---

## A1. Piercing 빈-시큐리티 즉시 패배 제거 (INVENTED 삭제)

**AS-IS**: `Pierce.cs:20-42 CanActivatePierce` — `Owner.Enemy.SecurityCards.Count >= 1`일 때만 Pierce 발동. `AttackProcess.cs:459` 시큐리티 체크 자체가 `SecurityCards.Count >= 1` 게이트. 패배는 **직접 공격** no-security 경로(`AttackProcess.cs:423 EndGame`)에서만.

**설계**: `AttackPipeline.cs:215-218`의 `MarkLose(...)` 블록 삭제 → 시큐리티 0이면 **그냥 return**(Pierce 미발동). 직접 공격 경로의 기존 MarkLose는 유지(그쪽이 AS-IS EndGame 대응).

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| 시큐리티 0 → Pierce no-op | `CanActivatePierce` count>=1 게이트 |
| 패배 판정은 직접 공격 경로만 유지 | `AttackProcess.cs:423` (직접 공격 시에만 EndGame) |

**테스트**: Pierce 공격자가 시큐리티 0 상대 디지몬을 전투 삭제 → ① 게임 계속(패배 아님), ② 시큐리티 체크 미발생. 대조: 직접 공격 + 시큐리티 0 → 기존 패배 유지.

---

## A2. AddSelfDigivolutionRequirement 레벨 게이트 복원 (~111장)

**AS-IS** (`AddDigivolutionRequirement.cs:51-82 GetEvoCost` 전문 확인):
- 평가 순서: ① ignoreRequirement 단락 → ② 색 게이트 → ③ **레벨 게이트** → ④ `CardCondition && PermanentCondition` → ⑤ `costEquation() ?? digivolutionCost`.
- 레벨 게이트 정확식: `ignoreLevel = (level<0 && minLevel<0 && maxLevel<0) || ignore-플래그`; 아니면 `HasLevel && (Level==level || (level<0 && (minLevel<0||Level>=minLevel) && (maxLevel<0||Level<=maxLevel)))`. **exact가 range에 우선**(range는 level<0일 때만).
- 검사 대상 = **진화원(digivolving-FROM) permanent**의 TopCard (`permanent.TopCard.Level/HasLevel`).

**포트 배선 지점** (확정):
1. 팩토리 `CardPortingFramework.cs:3168-3174` — 이미 받는 `level/minLevel/maxLevel`을 `AddedDigivolutionRequirementPredicateEffect` ctor로 전달.
2. `AddedDigivolutionRequirementPredicateEffect`(`:601-671`) — 필드 3개 추가 + `ToBinding`에 키 emit (`DigivolveAction.AddedEvolutionLevelKey/MinLevelKey/MaxLevelKey` 신설).
3. **단일 choke point** `DigivolveAction.AddedPredicateActive`(`:477-492`) — 이미 `new Permanent(context, targetInstanceId, targetOwner)`를 구성하므로 predicate 평가 **직전에** 레벨 게이트 삽입:
   ```csharp
   if (!LevelGatePasses(values, permanent)) return false;   // AS-IS 순서: 레벨 게이트가 조건 평가의 외곽
   // LevelGatePasses: 셋 다 미설정 → true; else permanent.TopCard.HasLevel &&
   //   (Level==level || (level<0 && range))  — AS-IS 식 그대로
   ```
   이 지점은 `MatchesAddedDigivolutionRequirement`(적법성)와 `TryGetAddedDigivolutionCost`(비용) **양쪽에 공통 도달** — AS-IS의 "게이트 통과 후에만 비용 반환" 구조와 일치.
- ignore-플래그 분기(`CanIgnoreDigivolutionRequirement`): 포트에 대응 게이트가 있으면 폴딩, 없으면 **debt 기록**(레벨 게이트 자체와 별개 기능 — G9-052 UseRequirements에서 ignore-color 게이트를 다룬 전례 확인 후 결정).

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| 게이트를 predicate 평가 앞에 | GetEvoCost: 레벨 게이트가 CardCondition/PermanentCondition의 외곽 가드(:69-74) |
| exact 우선, range는 level<0에서만 | `:71-72` 식 그대로 |
| HasLevel 필수 (no-level 카드 불통과) | `:70` `permanent.TopCard.HasLevel &&` |
| 진화원 permanent에 평가 | GetEvoCost의 `permanent` = 소스; 포트 choke point의 Permanent도 target(=소스)로 동일 확인 |
| 비용도 같은 게이트 뒤 | `:74-76` 게이트 안에서만 return cost |

**테스트**: exact `level:4` — Lv4 소스 수락 / Lv3·Lv5 거부; `minLevel:5`(EX6_073형) — Lv5·6 수락 / Lv4 거부(**술어만 매칭해도 거부** = flatten 회귀 단언); no-level 소스 거부; 셋 다 -1 → 기존 동작 회귀(G9-044/052 유지).

---

## A3. 뷰레이어 연속효과 폴딩 4종 (레벨·색·특성; 25장)

**AS-IS** (전 폴드 사이트 재확인):
- 인터페이스 5종은 전부 **누산기-변환 Func**: `GetCardLevel(level, cs)→level`, `GetCardColors(list, cs)→list`, `ChangTraits(list, cs)→list`, `GetPermanentLevel(level, p)→level`, `GetBaseCardColors(list, cs)→list`. 타게팅은 카드 클로저 내부(`if (cardSource == card) ...`), on/off는 `CanUseCondition`. `EffectTiming.None` 연속.
- **스캔 범위가 프로퍼티마다 다름** (1:1 필수):
  | 프로퍼티 | 시드 | 스캔 범위 |
  |---|---|---|
  | CardSource.Level(=TreatedLevel) | printed(무레벨→sentinel) | **자기 효과만** |
  | CardSource.CardColors | **BaseCardColors 완성본**(base 먼저) | 자기(비-permanent일 때만) + **양 플레이어 전 필드** → Distinct |
  | CardSource.BaseCardColors | printed | 동일 2-영역 → Distinct |
  | CardSource.CardTraits | Form+Attribute+Type | **자기 효과만**, Distinct 없음 |
  | Permanent.Level | TopCard.Level(무레벨→sentinel) | **전 필드 permanent + 플레이어 효과** |
- sentinel `1145140`은 **비교 소비자 0**(전부 `HasLevel` 가드 경유) → 포트 `-1` sentinel은 `HasLevel` 의미(printed 기준)만 유지하면 등가.

**설계** (포트의 기존 폴드 전례 2종을 템플릿으로 — `CardNames`의 `addedCardName` 폴드 `:176-197`, `ContinuousDpGate.ResolveDp`):
1. **어댑터 클래스 5종을 스켈레톤 자리에서 구현**(구조 미러 — 파일 위치·클래스명·`SetUp...` 시그니처 1:1): `ChangeCardLevelClass`·`ChangePermanentLevelClass`·`ChangeCardColorClass`·`ChangeBaseCardColorClass`·`ChangeTraitsClass`. 각각 저장하는 것은 **변환 Func 그대로**(binding values 키 신설: `view.changeCardLevel` 등, 값 = `Func<CardSource,int,int>` 등) + `ConditionKey`(CanUseCondition).
2. **getter 폴딩** — `CardPortingFramework.cs`:
   - `CardSource.Level`: printed 시드(무레벨 → `-1` 유지) → **자기 소스 binding만**(`SourceEntityId == InstanceId` 또는 target 포함) 순회하며 `level = f(this, level)`.
   - `CardSource.BaseCardColors`(신설)·`CardColors`: printed 시드 → base-change 폴드 → change 폴드. 스캔 = 원본대로 **전 필드**(registry의 해당 키 전 binding — 포트 registry는 필드 이탈 시 binding 제거되므로 "전 필드 스캔"과 등가) + 자기-비-permanent 분기, `Distinct`.
   - `CardSource.CardTraits`: printed 시드 → 자기 소스 binding 폴드, Distinct 없음.
   - `Permanent.Level`: `TopCard.Level` 시드 → `view.changePermanentLevel` 전 binding 폴드(`f(permanent, level)`).
3. **파생 멤버 자동 전파**: `HasCardColor/EqualsTraits/ContainsTraits/IsLevelN`은 getter 파생이라 무수정. `HasLevel`은 **printed 기준 유지**(AS-IS `CEntity_Base.HasLevel` = printed 플래그; 폴드가 HasLevel을 바꾸지 않음).
4. 팩토리는 만들지 않음 — **AS-IS도 팩토리가 없고 카드가 클래스를 직접 생성**하므로, 클래스 직접 생성이 구조 미러.

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| Func 자체 저장(값/술어 분해 금지) | 인터페이스가 누산기-변환 Func(:69,:76,:168,:289,:296) |
| 프로퍼티별 스캔 범위 차등 | TreatedLevel(:947 self-only) vs CardColors(:446 전 필드) vs Traits(:2581 self-only) vs Permanent.Level(:48 전 필드+플레이어) |
| base→change 2단(색) | `CardColors` 시드가 `BaseCardColors`(:449) |
| HasLevel = printed 유지 | `CEntity_Base.HasLevel`(:317) — 폴드 무관 |
| `-1` sentinel 유지 | `1145140` 비교 소비자 0 확인(전부 HasLevel 가드) |
| registry binding = "전 필드 효과" 등가 | 원본 스캔 대상은 필드 permanent의 EffectList — 포트 registry는 입장 시 등록·이탈 시 제거로 동일 집합 |
| Distinct: 색만, traits 없음 | `:483` vs `:2604` |

**주의(순서)**: 원본 폴드 순서 = EffectList 순회 순서(등록순). 포트도 registry 등록순 순회 — 다중 변경 효과 공존 시 순서 민감 카드(현재 25장 중 확인된 충돌 없음)는 테스트에 순서 케이스 1건 포함.

**테스트**: ① BT17_068형(자기 레벨→6, IsBeingRevealed 조건부) — 조건 on/off에 따라 Level 6/printed; ② ChangePermanentLevel — IsMinLevel/IsMaxLevel·A2 레벨 게이트가 folded 레벨을 읽음(**A2와 연동 단언**); ③ 색 변경 — `HasCardColor`·진화 색 요구 판정 변화 + Distinct; ④ base-색 vs 색 2단 순서; ⑤ 특성 부여 — `ContainsTraits` 매칭(Overclock trait 게이트로 소비자 단언); ⑥ 무레벨 카드 + 레벨 폴드 시드 sentinel 경로.

---

## A4. Partition 조건 모델 복원 (13장)

**AS-IS** (Partition.cs 전문 확인):
- `PartitionCondition` = {Level:int, Color, Color2, Name, 3모드 플래그} — 생성자 3형: (level,color) / (level,color,color2) / (name). **항상 조건 2개**, [0]→그룹1·[1]→그룹2.
- 필터: 색 모드 = `HasCardColor(색)[||색2] && HasLevel && Level == 조건.Level`(**exact**); 이름 모드 = `EqualsCardName(name)`(레벨 무시).
- 활성 게이트: 배틀에리어 + `DigivolutionCards.Count>=2` + **각 그룹 비어있지 않음**.
- 선택: 단일-카드 그룹 상호배제 조정(`Except`) 후 **각 그룹에서 1장씩**(그룹 크기 1이면 자동), 선택 겹침 방지(`secondSources.Except(selected)`), 2장 확정 시 free 플레이(ETB on).

**포트 배선 지점** (확정): grant(`PartitionSelfEffect :3266`)가 조건 리스트를 버림; 소비 측은 `FragmentRemainingKey` 카운터(2→0)가 유일한 per-pick 인덱스.

**설계**:
1. **조건 모델 신설**: `PartitionCondition` 클래스를 Commons에 1:1 미러(같은 필드·생성자 3형). `PartitionSelfEffect(cardSourceConditions)`가 이를 `List<PartitionCondition>`로 받아 binding values `partition.conditions`에 저장(D1 Decoy 술어 저장과 동일 패턴).
2. **후보 분리**: `DeletionReplacementTiming`에 `FindPartitionGroupCandidates(context, record, groupIndex)` 신설 — 저장된 conditions[groupIndex]로 소스 필터(색 모드: `HasCardColor && HasLevel && Level==L`(A3 폴딩 레벨/색 사용); 이름 모드: 카드명). 조건 미저장(구 grant) → 기존 flat 동작 유지(회귀 없음).
3. **활성 게이트 교체**(`:209`): flat `>=2` → `그룹0 후보>0 && 그룹1 후보>0` (+기존 by-battle/own-effect/once 게이트 유지).
4. **pick 인덱스 연결**: `remaining=2`가 pick#1(그룹0), `remaining=1`이 pick#2(그룹1) — `GetTargets(PartitionOption)`이 remaining 값으로 그룹 선택, pick#2 후보에서 pick#1 선택 카드 제외(AS-IS `Except`). 단일-카드 상호배제(양 그룹이 같은 1장을 가리키는 경우)는 게이트에서 "서로 다른 2장 배정 가능"으로 판정.
5. 적용은 기존 `TryPartitionPlaySourceAsync`(free 플레이, ETB) 재사용 — AS-IS `PlayPermanentCards(payCost:false, activateETB:true)` 대응 확인됨.

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| 조건 2개 고정, [0]/[1]→그룹 | Partition.cs:66-117 |
| 색 모드 exact Level | `:80` `HasLevel && Level ==` |
| 이름 모드 레벨 무시 | `:89-91` |
| 게이트 = 각 그룹 비공 | `CanActivateCondition :145-159` |
| 그룹당 1장 + 겹침 방지 | PartitionClass `:145-150 Except` + 사전 조정 `:161-170` |
| free 플레이 ETB | `PlayPermanentCards(payCost:false, activateETB:true) :153-162` |

**테스트**: BT16_012형((4,Red)/(4,Yellow)) — ① 그룹별 후보 분리(pick#1=Red Lv4만, pick#2=Yellow Lv4만·pick#1 제외), ② 한 그룹 빈 경우 옵션 미표시, ③ Red Lv4 1장+Yellow Lv4 1장이 **같은 카드**(불가능하지만 dual-색 카드 케이스) 상호배제, ④ 이름 모드, ⑤ 조건 미저장 grant 회귀.

---

## B1. ArmorPurge → PRE 치환 전환 + WhenTopCardTrashed

**AS-IS** (ArmorPurge.cs 전문 확인): `willBeRemoveField=false`로 삭제 **취소**(PRE) — top 카드만 제거·트래시(**토큰은 트래시 안 함** :54-57), 새 top `SetChangedLocationTime()`(연속효과 재유도), `WhenTopCardTrashed` 발화(:69-79). 게이트 = 배틀에리어 + `DigivolutionCards>=1`.

**포트 확정 사실**: 현재 POST(트래시 도달 요구, `:266-270`); `DeDigivolveHelpers.DeDigivolveAsync`가 동일 promote-under primitive이고 **WhenTopCardTrashed도 emit**하지만 레벨 플로어·`cannotBeDeDigivolved` 면역이 있음(ArmorPurge에는 없어야 함).

**설계**:
1. `ArmorPurgeOption`을 `PostOptions`에서 **`PreOptions`로 이동**(게이트: 키워드 + `SourceIds>=1`) — sink의 defer 경로(`HasPreOption`)가 자동으로 삭제를 보류(= AS-IS `willBeRemoveField=false` 대기).
2. 적용 신설 `TryArmorPurgeReplaceAsync`: top 카드만 트래시(**토큰이면 제거만**) → `sources[0]` promote(기존 코드 재사용, **레벨 플로어·DeDigivolve 면역 미적용**) → `pendingDeletion` 클리어(홀더 생존) → `WhenTopCardTrashed` emit(대상 = 트래시된 top).
3. 기존 POST 경로 제거(중복 창 원인). `armorPurged` once-마커 유지 여부는 AS-IS에 없음 — **제거**(AS-IS는 매 삭제 시 발동 가능; 확인: CanActivate에 once 게이트 없음).

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| PRE(삭제 취소) | `:63 willBeRemoveField=false` |
| top만 트래시, 토큰 제외 | `:52-57` |
| OnDeletion 미발화(생존) | 삭제 취소이므로 destroy 이벤트 자체가 없음 |
| WhenTopCardTrashed 발화 | `:69-79` |
| 레벨 플로어 없음 | ArmorPurge.cs에 해당 게이트 부재(DeDigivolve 전용) |
| once-마커 제거 | CanActivateArmorPurge(:9-20)에 once 게이트 없음 |

**테스트**: ① ArmorPurge 홀더 삭제 → PRE 창 → 수락 시 top 트래시·소스 승격·홀더 생존·**OnDeletion 미발화 단언**·WhenTopCardTrashed 발화; ② 같은 삭제에서 Fortitude/Ascension POST 창 안 열림(생존이므로); ③ 거절 시 정상 삭제; ④ 토큰 top → 트래시 존에 없음.

---

## B2. 전투 트리거 드레인 순서 (battle → 드레인 → 생존 확인 → 시큐리티)

**AS-IS** (`AttackProcess.cs:444-465`): battle → `TriggeredSkillProcess`(전 트리거 해소) → `AttackingPermanent.TopCard == null`이면 종료 → `DoSecurityCheck && Security>=1`이면 체크.

**포트 확정 사실**: 드레인은 `GameFlowProcessor.AutoProcessAsync`뿐이고 파이프라인은 battle→피어싱을 **같은 호출에서 직행**. 단 `RequiresDeletionReplacement` 조기 반환(=phase 파킹 후 공용 루프 재진입) 선례가 있음.

**설계**: 피어싱 분기를 동일 패턴으로 파킹 — `AttackPhase.PendingPiercing`(신설) 세팅 후 return → 공용 루프가 `AutoProcessAsync`(드레인) 수행 → 다음 `AdvanceAsync`에서 `PendingPiercing` 재개 시 **공격자 생존 재확인**(battle-area 존재 + TopCard 유효; AS-IS `TopCard==null → End`) 후 `ApplyPiercingSecurityAsync`. 시큐리티-디지몬 배틀 앞 OnSecurityCheck 스킬 순서(SecurityResolver 내부)는 **후속 서브항목**으로 분리(동일 파킹 패턴 적용 가능성 조사 후) — 본 설계는 battle→pierce 구간만.

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| battle 후 트리거 전부 해소 | `TriggeredSkillProcess(true,null)` 위치 |
| 재개 시 공격자 생존 재확인 | `if (TopCard == null) { State=End; yield break; }` |
| 파킹 패턴 재사용 | 포트 자체 선례(`RequiresDeletionReplacement`, AttackPipeline:146-151) |

**테스트**: OnKnockOut로 시큐리티 +1 하는 효과 + Piercing — AS-IS 순서면 추가된 시큐리티가 체크 대상(수 변화 단언); OnEndBattle로 공격자 삭제 → 피어싱 미발생.

---

## B3. DP≤0 sweep을 정식 삭제 경로로

**AS-IS** (AutoProcessing.cs 확인 — **2계열 구분**):
- `DigimonLackDPProcess`(DP≤0 디지몬, :469-484): `DestroyPermanentsClass(..., {"DPZero": true}).Destroy()` — **효과 삭제와 동일 경로**(WhenPermanentWouldBeDeleted/WhenRemoveField/OnDestroyedAnyone/OnLeaveFieldAnyone 전부 발화, `isDPZero` 플래그 전파).
- `TrashNoDPPermanentProcess`(무DP permanent, :439-465): **직접 트래시**(destroy 아님, 트리거 없음).

**포트 확정 사실**: lethal-DP sweep이 raw zone-move(마커·PRE·Fortitude 전무). sink는 context에서 전 재료로 생성 가능(`MatchStateMutationSink.DeleteKind` 적용).

**설계**:
1. `GameFlowProcessor.RuleProcessAsync`의 lethal-DP 분기 → sink 생성 후 `Delete` mutation 적용(+ 신설 `IsDpZeroKey=true`를 mutation payload/record 메타로 전파 — AS-IS `isDPZero` 대응). sink의 기존 경로가 자동으로: 삭제-방지 체크·PRE defer(Evade/ArmorPurge(B1 후)/Scapegoat/Fragment — **Decoy는 제외**: by-enemy-effect 조건이 rule-삭제에 미성립, deleterId 없음으로 자연 배제)·`deletedByEffect`... 주의: rule 삭제는 "효과 삭제" 아님 — `DeletedByEffectKey` 대신 **`DeletedByRuleKey`(신설)** 스탬프. Decode/Partition의 `!byBattle` 게이트는 AS-IS상 DP-zero도 통과(AS-IS 트리거는 IsByBattle만 제외) — 매칭 확인: Decode `!IsByBattle`(Decode.cs:54-55)이므로 DP-zero 삭제에서 발동 **가능**이 1:1.
2. 무DP(printed DP 자체가 없는) 케이스는 현행 직접 트래시가 이미 1:1(`TrashNoDPPermanentProcess` 대응) — 변경 없음(현재 sweep이 이 케이스를 덮는지 확인만; `HasLethalDp`가 "defined DP" 요구라 무DP는 미포함 = 현재 미처리라면 별도 debt).

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| DP≤0 = 정식 destroy 경로 | `DigimonLackDPProcess → DestroyPermanentsClass` |
| isDPZero 플래그 전파 | `{"DPZero":true}` + `OnDeletionHashtable(isDPZero)` |
| byEffect 아님(별도 rule 마커) | 삭제 hashtable에 cardEffect 없음(rule 발) |
| Decode/Partition 발동 가능 | Decode `!IsByBattle`만 제외(:54-55) |
| 무DP는 직접 트래시 유지 | `TrashNoDPPermanentProcess`가 destroy 미경유(:459-460) |

**테스트**: -DP 연속효과로 0 → ① Fortitude 홀더 재생, ② Evade PRE 창, ③ OnDestroyedAnyone 발화, ④ `DeletedByBattleKey` 미설정(Decode 발동 가능), ⑤ Decoy 창 안 열림.

---

## B4. Reveal 플로우 6건 (`RevealAndSelect` v2)

**AS-IS 재확인 요점**: ① 두 helper 모두 `CanTargetCondition`으로 필터; ② ProcessForAll은 **선택 없음**(전 매칭 자동 처리); ③ 남은 카드 top/bottom 배치는 **플레이어가 전량 순서 지정**(단일 SelectCard, maxCount=전량, canEndNotMax:false — 선택 순서 = 배치 순서; **top은 Reverse 후 삽입**, 1장이면 무프롬프트); ④ `DeckTopOrBottom`은 top/bottom 이지선다 후 순서; ⑤ `isOpponentDeck`→상대 라이브러리; ⑥ 다중 조건(`SelectCardConditionClass[]` + mutualConditions, BT10-096형)은 조건별 순차 패스.

**설계** (`RevealAndSelect.cs` 확장 — 요청 단계별):
1. **후보 필터**: `RequestChoice(..., Func<HeadlessEntityId,bool>? selectCondition)` — 비매칭 카드는 `IsSelectable:false`로 **표시는 유지**(원본도 전 리빌 카드를 보여주고 매칭만 선택 가능), `maxCount = min(max, 매칭 수)`.
2. **ProcessForAll 모드**: `RevealAndProcessAllAsync(context, player, count, condition, mode, remainingTo)` 신설 — **초이스 없이** 매칭 전부에 mode 적용(mandatory), 남은 카드는 3의 순서 규칙으로. 기존 선택형 진입점과 분리(카드 포팅 시 helper 대응이 1:1로 갈라짐: ProcessForAll↔ProcessAll, AndSelect↔RequestChoice).
3. **남은 카드 순서**: 남은 카드 ≥2 && 목적지가 DeckTop/DeckBottom이면 **순서 지정 서브초이스**(단일 요청, maxCount=전량, canEndNotMax:false, `SelectedIds` 순서 = 배치 순서 — 포트 ChoiceResult가 선택 순서 보존함을 전제로 검증 테스트 포함). bottom = 선택순 그대로 append, **top = 선택순 Reverse 후 삽입**(AS-IS :573-577). 1장이면 무프롬프트.
4. **DeckTopOrBottom**: `RevealDestination.DeckTopOrBottom` 추가 — 남은 카드 처리 전에 top/bottom 이지선다 초이스 1회 → 3으로 위임.
5. **상대 덱**: `isOpponentDeck` 파라미터 — 리빌·이동 대상 라이브러리를 상대로.
6. **다중 조건 패스**: 조건 배열 + mutualConditions는 **STOP/debt** — 사용 카드(BT10-096형)가 포팅 큐에 오를 때 조건별 순차 RequestChoice 루프로 확장(설계 골격만 기록). 이유: mutual-조건 상호작용의 AS-IS 대조가 카드 실물 없이 과설계 위험.

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| 비매칭 = 표시하되 선택 불가 | `SetUp(canTargetCondition: ...)` — 리빌은 전체 공개 |
| ProcessForAll = 무선택 mandatory | `:10-175` 선택 부재, `.Filter(cond)` 전량 처리 |
| 순서 = 단일 전량-선택의 선택순 | `:485+ maxCount=count, canEndNotMax:false` |
| top은 Reverse | `:573-577 topCards.Reverse()` |
| 1장 무프롬프트 | `:478-483` |
| top/bottom 이지선다 | `:618-650` |
| 상대 덱 | `:25,:246 isOpponentDeck → Enemy` |

**테스트**: ① 필터 — 비매칭 선택 불가·max 클램프; ② ProcessForAll — skip 액션 부재 + 매칭 전량 처리(**mandatory 단언**); ③ bottom 순서 — 3장 순서 지정 → 덱 끝 3장 순서 단언; ④ top 순서 — Reverse 반영 단언; ⑤ 1장 무프롬프트; ⑥ top-or-bottom 분기; ⑦ 상대 덱 리빌.

---

## B5. SelectPermanentEffect 4건

**AS-IS 재확인 요점**: Degenerate = `IDegeneration(selected, _degenerationCount, effect)`; Attack = 선택된 각 공격자에 `SelectAttackEffect.SetUp(attacker, ()=>_canAttackPlayer, _defenderCondition, effect)` 순차 실행(+`_canNoSelect`면 공격 강제); PutSecurity는 매 배치 전 `CanAddSecurity`; `CanEndSelect`에 조합 술어 `_canEndSelectCondition(permanents)`.

**설계**:
1. **Degenerate**: `SetUp`에 `degenerationCount` 추가(기본 1) → `BuildMutation`이 `DeDigivolveKind` mutation(count 포함) 반환 — primitive(`DeDigivolveHelpers`) 기존재 확인됨(레벨 플로어·WhenTopCardTrashed 포함 = AS-IS IDegeneration 대응은 **별도 대조 1건**: IDegeneration에도 Rookie 플로어 있는지 확인 후 count 의미 확정; 불일치 시 mutation에 플로어 플래그).
2. **Attack**: `SetUp`에 `defenderCondition`/`canAttackPlayer` 수용 + `EffectAttackOptions`에 `Func<HeadlessEntityId,bool>? DefenderCondition`(nullable, 기본 null=전부) 추가 → `EffectDrivenAttack.GetTargets`가 후보에 적용. Attack 모드 적용은 선택 공격자마다 `EffectDrivenAttack.RequestChoice` — **순차 실행은 1공격자=1초이스**로: 다중 선택 시 남은 공격자를 메타 큐로 이어가는 파킹 패턴(사용 카드 대부분 maxCount 1 — AD1_009·BT25_018 확인 — 1차는 단일 공격자만 지원 + 다중은 debt).
3. **PutSecurity 게이트**: K2와 동일 결론 — `CanAddSecurity` 제한 효과(`CannotAddSecurityClass`)가 미포팅 스켈레톤이므로 **폴딩 지점만 마련**(sink `AddToSecurityKind` 적용 전 훅 주석 + debt 참조). 신규 구현 없음.
4. **조합 술어**: `SetUp`에 `canEndSelectCondition: Func<IReadOnlyList<HeadlessEntityId>,bool>?` 추가 → ChoiceResult 검증 시 선택 집합에 평가, 불통과면 choice 거부(재선택). 초이스 인프라의 결과-검증 훅 존재 여부 probe 후, 없으면 resolve 측(EffectChoiceHelpers.ResolveAsync)에서 검증-재요청 루프.

**매칭 검증**:
| 설계 결정 | AS-IS 앵커 |
|---|---|
| Degenerate = de-digivolve(count) | `:1005 IDegeneration(selected, count, effect)` |
| defenderCondition→타깃 후보 | `:1009-1027 SetUp(defenderCondition:...)` |
| canAttackPlayer 스냅샷 | `()=>_canAttackPlayer` Func 전달(:1019) |
| 조합 술어 = 종료 가능 조건 | `CanEndSelect :220-238` |
| PutSecurity 게이트는 라틴트 | 원본 게이트 존재(:976,:987)·포트 제한효과 미포팅(K2 동일) |

**테스트**: ① Degenerate — 선택 후 소스 N장 트래시·top 강등(WhenTopCardTrashed); ② Attack — defenderCondition 매칭 디지몬만 후보 + canAttackPlayer=false면 플레이어 제외; ③ 조합 술어 — 동색 2장 거부·이색 2장 수락.

---

## C군 — 소형/라틴트 조치

| # | 항목 | 조치 | AS-IS 앵커 | 구분 |
|---|---|---|---|---|
| C1 | Fragment `trashValue` | grant에 값 저장(`fragment.trashValue`) → `FragmentCost()`가 메타보다 grant 값 우선 | `CanActivateFragment(p, trashValue)` / FragmentProcess | 즉시 |
| C2 | CanNotAffected `permanentCondition` | `ContinuousImmunityEffect`에 target-술어 저장 + `BlocksOpponentEffect`가 **대상**에 평가(D1 패턴) | `CanNotAffectedClass.CanNotAffect = CardCondition(target) && SkillCondition` | 즉시(라틴트지만 D1 인프라 재사용으로 저비용) |
| C3 | 희생 대상 가드 | `SacrificeAsync`에 `CannotBeDeletedKey`+연속 삭제-방지 체크; Fragment 게이트에 `CanBeDestroyedBySkill` 상당 | Scapegoat.cs:416(DeleteP...AccordingToResult) / Fragment.cs:438-444 | 즉시(중첩 치환 재귀는 debt) |
| C4 | Iceclad source-count 타입 | `SourceCount`가 `DeletionReplacementGate.ReadSourceIds`(양 타입 수용) 재사용 | 포트 내 일관성(원본은 리스트 직접) | 즉시 |
| C5 | Counter 2-pass | OnCounter emit을 비-[Counter]→[Counter] 2회로 분리 — [Counter] 마커가 포트 효과 모델에 없으면 **STOP+debt** | `AttackProcess.cs:266-296` | probe-first |
| C6 | Save 술어/optional | 타깃 스텝 `canSkip:true`(AS-IS canNoSelect:true) + 카드 술어는 F68D 조건 서비스로(기존 seam) | Save.cs:218-257 | 즉시(canSkip)·seam(술어) |
| C7 | dual 카드 CardKinds | CardType 복수화(`CardKinds` 리스트) — 데이터 로더·전 소비자 파급 → **대량 포팅 선결 아님, debt** | `CardSource.cs:3547 CardKinds` | STOP+debt |
| C8 | link vs 진화원 구분 | `sourceIds`와 별도 `linkedIds` 도입 — Link 서브시스템(G9-056) 확장 설계 필요 | `Permanent.cs:892 DigivolutionOrLinkCards` / `LinkedCards` | STOP+debt(설계 별건) |
| C9 | isLinkedEffect/rootCardEffect 수명주기 | 별도 표적 감사 후 설계(감사 권고대로) | `SetIsLinkedEffect`/`SetRootCardEffect` | 별도 감사 |
| C10 | CardNames rename/remove·DP LinkedDP/Boost/0-floor | ①`IChangeCardNamesEffect` 변환-Func 폴드로 확장(A3와 동일 패턴) ②ContinuousDpGate에 LinkedDP·DPBoost·0-floor 존재 검증 후 부족분 폴딩 | `CardSource.cs:1442-1459` / `Permanent.cs:639,653-662` | A3 후속 |

---

## 우선순위·의존 관계

```
A1 (독립, 1줄급) ─────────────────────────────┐
A2 (독립) ── A3 레벨 폴드가 들어오면 자동 강화 ┤
A3 (독립, 25장+전 판정 기반) ── A4·C10이 의존 ┤→ 최종 전체 green 게이트
A4 (A3의 색/레벨 폴드 의존 — 색 모드 정확성) ─┤
B3 (B1의 PRE ArmorPurge와 상호작용 — B1 먼저) ┤
B1 → B3 → B2 (삭제/트리거 계열 순서) ─────────┤
B4, B5 (독립, 공용 플로우 — 대량 포팅 선결) ──┘
C1·C2·C3·C4·C6 즉시 / C5 probe / C7·C8·C9 debt·별도
```

**권장 실행 순서**: A1 → A2 → A3 → A4 → B1 → B3 → B2 → B4 → B5 → C(즉시군) → debt 기록.

## 실행 대화문 (복붙용)
```
위반 조치 진행. docs/audit/fidelity_violation_fix_design.md 순서대로(A1 → A2 → A3 → A4 → B1 → B3 → B2 → B4 → B5 → C즉시군).
각 항목: 설계 문서의 AS-IS 앵커 재확인 → 매칭 검증표대로 구현(발명·flatten 금지) → 동작-단언 테스트(매칭/비매칭+회귀) + bash scripts/run-tests.sh green + tools/RuleAudit 0. 이전 항목 green 후 다음. probe-first 항목(A2 ignore-플래그, B5 IDegeneration 플로어, C5 [Counter] 마커)은 확인 후 불가 시 STOP+fidelity_debt. 커밋은 내가 지시할 때.
```
