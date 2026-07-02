# 1:1 위반 전수 감사 2차 (2026-07-02) — "임의 구현" 색출

> **→ 조치방안 설계·AS-IS 매칭 검증 완료**: [**fidelity_violation_fix_design.md**](fidelity_violation_fix_design.md) (A1~A4·B1~B5 설계+검증표, C군 조치 구분, 실행 순서).

> **목적**: 이전 세션들(엔진 완성기·프리미티브 선행개발)이 AS-IS 1:1 원칙을 어기고 임의 구현/flatten/인자 무시한 지점의 전수 색출.
> **방법**: 병렬 감사 3축 — ① 팩토리 전수(인자 무시), ② 런타임 게이트(원본 프로세스 대조), ③ 뷰레이어·초이스(축소). 모든 항목 원본 코드 인용 대조. 최고영향 2건(A1·A2)은 포트 코드 직접 재확인함.
> **선행 확정 위반(이번 세션에 이미 수정)**: VortexCanAttackPlayers flatten(K1), Decoy permanentCondition 무시(D1), TreatAsDigimon 술어 drop(K4), PlayMindLinkTamer Digimon-only 필터(K5), registry-only HasKeyword의 player-scope source 오인, Ascension bottom 삽입(K2) — [fidelity_m4m5_design.md](fidelity_m4m5_design.md). **같은 패턴이 아래처럼 더 있었다.**

## A급 — 게임 결과 왜곡 / 대량 카드 영향

### A1. 🔴 Piercing 빈-시큐리티 = 즉시 패배 **발명** (INVENTED)
- 포트: `AttackPipeline.cs:215-218` — Piercing 시큐리티 체크에서 시큐리티 0이면 `MarkLose(defender, ...)`.
- AS-IS: `Pierce.cs:20-42 CanActivatePierce` — 시큐리티 **≥1일 때만** Pierce 발동, 0이면 아무 일 없음. 패배는 **직접 공격**의 no-security 경로(`AttackProcess.cs:423 EndGame`)에서만.
- 영향: 시큐리티 0인 상대의 디지몬을 Pierce가 전투로 이기면 **게임이 한 턴 일찍 잘못 끝남** — 늦게임 승자 왜곡. 포트 코드 직접 확인됨.

### A2. 🔴 `AddSelfDigivolutionRequirementStaticEffect` — `level/minLevel/maxLevel` 무시 (~111장)
- 포트: `CardPortingFramework.cs:3168-3174` — 시그니처는 받고 effect에 **미전달** ("accepted for fidelity" 주석). 직접 확인됨.
- AS-IS: `AddDigivolutionRequirement.cs` GetEvoCost — 레벨 범위는 `permanentCondition`과 **별개의 하드 게이트**:
  `if(ignoreLevel || (HasLevel && (Level==level || (minLevel/maxLevel 범위))))` 통과 후에야 조건 평가.
- 영향: exact `level:` 전달 109장 + `minLevel:` 2장(EX6_073, BT19_043). 예: EX6_073은 술어가 특성만 검사 → 포트에서는 **아무 레벨에서나** 대체 진화 가능.

### A3. 🔴 뷰레이어 연속효과 폴딩 누락 — Level·CardColors·CardTraits (+ Permanent.Level)
- 포트: `CardSource.Level/CardColors/CardTraits`, `Permanent.Level`이 **printed 메타만** 읽음 (`CardPortingFramework.cs:166,169,200,914`).
- AS-IS: `CardSource.cs`/`Permanent.cs`가 `IChangeCardLevelEffect`·`IChangePermanentLevelEffect`·`IChange(Base)CardColorEffect`·`IChangeTraitsEffect`를 **라이브 폴딩**. 대응 효과 클래스 6종(`ChangeCardLevelClass`·`ChangePermanentLevelClass`·`ChangeCardColorClass`·`ChangeBaseCardColorClass`·`ChangeTraitsClass` 등)이 포트에서 전부 **7줄 스켈레톤** — 폴딩이 딴 데 있는 게 아니라 미구현.
- 부수: no-level sentinel(원본 `1145140`) vs 포트 `-1`; `DualCardColors` 등 옵션 색 요구 분화 부재.
- 영향: 레벨/색/특성을 바꾸는 연속효과 카드 전부 — 진화 적법성·색 요구·특성 조건 판정 왜곡. **프리미티브 선행개발 원칙상 이 폴딩 seam은 강모델 선결 대상.**

### A4. 🔴 Partition 이중 위반 — `cardSourceConditions` drop + 색 그룹 분리 누락 (13장)
- 팩토리: `PartitionSelfEffect`(`:3266`)가 `cardSourceConditions`를 통째로 버리고 키워드만 grant. AS-IS Partition은 진화원을 조건별 두 그룹(`sourceOneCard`/`sourceTwoCard`)으로 나누는 **조건 리스트가 메커니즘의 정의**(활성 조건 = 양 그룹 비어있지 않음).
- 런타임: `ApplyPartitionSource`가 단일 풀에서 아무 2장 — AS-IS는 **색 그룹 1에서 1장 + 색 그룹 2에서 1장**(`Partition.cs:699-771`).
- 영향: 13장 전부(`cardSourceConditions`는 AS-IS 필수 인자) — 불법 색 조합 재생 가능.

## B급 — 트리거/순서/모델 변질

### B1. 🟠 ArmorPurge: PRE 치환 → POST 완전삭제로 변질 + `WhenTopCardTrashed` 미발화
- AS-IS(`ArmorPurge.cs:859-888`): top 카드만 트래시, `willBeRemoveField=false` — permanent는 **삭제되지 않음**(OnDeletion 미발화), `WhenTopCardTrashed` 발화.
- 포트: 완전 삭제 후 재조립(POST) → OnDeletion/OnDestroyed 오발화, 같은 삭제에 Fortitude/Ascension/Save 중복 창, WhenTopCardTrashed 영구 미발화.

### B2. 🟠 전투/시큐리티 트리거 드레인 순서 역전
- AS-IS: 전투 해결 → **OnEndBattle/OnKnockOut 트리거 드레인** → Pierce/시큐리티 체크(`AttackProcess.cs:444-465`); OnSecurityCheck 스킬 해결 → 시큐리티 디지몬 배틀(`CardController.cs:4108-4184`).
- 포트: 시큐리티 작업을 먼저 하고 큐 드레인은 파이프라인 종료 후 — 트리거가 시큐리티 결과에 영향 주는 카드에서 순서 오류.

### B3. 🟠 DP≤0 상태기반 삭제가 치환 키워드 전부 건너뜀
- 포트 `GameFlowProcessor.cs:168-182`: 트래시 직행 — `DeletedByEffect` 마커·POST 창·Fortitude 미경유.
- AS-IS: DP 0 삭제도 OnDeletion 이벤트 — Fortitude/Ascension/Save/Decode/Partition 대상.

### B4. 🟠 Reveal 플로우 6건 (RevealAndSelect vs 원본 RevealLibrary helpers)
1. **후보 필터 없음** — 원본은 `CanTargetCondition`으로 매칭 카드만 선택 가능; 포트는 전부 선택 가능(비매칭 카드 획득 가능).
2. **ProcessForAll이 선택형으로 변질** — 원본은 **전 매칭 카드 자동 처리(선택 없음, mandatory)**; 포트는 skip 가능한 초이스(mandatory→optional 룰 위반 방향).
3. **남은 카드 순서**: 원본은 플레이어가 순서 지정(+top은 `Reverse()`); 포트는 리빌 순서 고정.
4. **DeckTopOrBottom**(top/bottom 선택) 미지원 — BT18_068/061, BT20_095 표현 불가.
5. **상대 덱 리빌**(`isOpponentDeck`) 미지원 — 항상 자기 라이브러리.
6. **다중 조건 패스**(`SelectCardConditionClass[]` + mutualConditions, BT10-096형) 단일 버킷으로 붕괴.

### B5. 🟠 SelectPermanentEffect 4건
1. `Mode.Degenerate` = throw (디진화, AD1_009·BT25_038) + `degenerationCount` 인자 자체가 없음.
2. `Mode.Attack` = **무동작 no-op** — 원본은 SelectAttackEffect 서브플로우(강제 공격 카드 침묵 실패); `_defenderCondition`/`_canAttackPlayer` 표면 전체 부재.
3. `PutSecurity*`에 `CanAddSecurity` 게이트 누락(원본은 매 배치 전 검사).
4. 조합-유효성 술어(`CanEndSelect`의 `canEndSelectCondition(permanents)`) 미모델 — "서로 다른 색 2장" 류 불법 조합 허용.

## C급 — 소수 카드/라틴트/엣지

- **C1** `FragmentSelfEffect(trashValue)` 무시 — Fragment `<X>`의 X 소실 (EX10_034·EX11_044, 둘 다 3).
- **C2** `CanNotAffectedStaticEffect(permanentCondition)` 대상 술어 무시 — 현재 호출자 0(라틴트).
- **C3** 희생(Scapegoat/Decoy)이 희생 대상의 `CanBeDestroyed`/자체 치환을 우회; Fragment의 `CanBeDestroyedBySkill` 게이트 누락.
- **C4** Iceclad source-count가 `IEnumerable<string>`만 인식(타입 불일치 시 0/0).
- **C5** Counter 타이밍 2-pass([Counter] vs 비-[Counter] 순서) 단일 emit으로 붕괴.
- **C6** Save: 카드 술어 없을 때 아무 permanent 부착 + 대상 단계 강제(원본 canNoSelect:true).
- **C7** dual 카드(CardKinds 복수) 미모델 — `IsDigimon/IsOption` 상호배타.
- **C8** link 카드 vs 진화원 구분(`LinkedCards`/`IsLinked`) 부재 — "진화원 1장당" 카운트에 link 카드 혼입.
- **C9** `isLinkedEffect`/`rootCardEffect` 전반 무시 — 효과 **수명주기/출처**(link 소스 이탈 시 제거 등) 문제. 별도 표적 감사 필요.
- **C10** CardNames 폴딩이 additive-only(원본은 rename/remove도) · DP 폴딩에 LinkedDP/DPBoost/0-floor 포함 여부 미검증.

## 위반 아님 / 재평가

- `DigiXrosEffectFromNames`의 costReduction/canTargetCondition drop — **원본도 무시**(AS-IS 자체가 하드코딩). 단 cost-reduction 2 vs 포트 MemoryCost 0의 플레이 비용 적용 경로는 별도 검증 권고.
- `CanNotBeDestroyedByBattle` 4-인자 전투술어(EX8_068) — 감사는 IGNORED-PARAM으로 재보고했으나 **기존 M-2 판정 유지**: 술어 = `permanent==공격자||방어자`인데 전투 삭제는 참가자에게만 발생 → 평가 시점에 항상 참(trivial). 단 팩토리가 인자를 버리는 구조는 사실이므로 non-trivial 사용자 등장 시 위반 — 라틴트 기록.
- 문서화된 first-candidate LIMITATION 5곳 — F68 창 도입으로 대부분 대체됨(단 `DeletionReplacementGate.cs:16-19` 클래스 헤더가 auto-apply라고 낡은 서술 — 문서 갱신만).

## 클리어 확인 (충실 미러)

Evade(서스펜드 비용) · Barrier(by-battle+시큐리티≥1) · Jamming(시큐리티 디지몬 한정) · Fortitude · Decode 게이팅 · Partition 게이팅(선택 제외) · Decoy 스코프 · Scapegoat 필수 서브선택 · 전투 코어(동DP 상살·Iceclad 스위치·직접공격 패배) · Retaliation · 시큐리티 루프 · SelectPermanent 카운트 규칙·기본 모드 6종 · Reveal 목적지 primitive · IsToken/IsSuspended/DigivolutionCards 구조. 팩토리 클리어 범위: 3112-3160, 3179-3290(위반 제외), 3299-3488, 3490-3552, 3592-3907.

## 우선순위 제안

| 순위 | 항목 | 근거 |
|---|---|---|
| 1 | A1 Piercing 패배 발명 | 승자 왜곡, 수정 1줄급 |
| 2 | A2 레벨 게이트 복원 | ~111장 |
| 3 | A3 뷰레이어 폴딩 4종 | 향후 포팅 전반의 판정 기반, 스텁 6종 구현 필요(프리미티브급) |
| 4 | A4 Partition 조건 모델 | 13장, 메커니즘 자체 |
| 5 | B4 Reveal 6건 · B5 SelectPermanent 4건 | 대량 포팅 시 사용 빈도 최고인 공용 플로우 |
| 6 | B1–B3 트리거/순서 | 상호작용 정확성 |
| 7 | C군 | 소수/라틴트 — debt 기록 후 순차 |
