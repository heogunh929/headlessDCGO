# M-4 키워드-동작 · M-5 근사 · Decoy refinement — 설계 (AS-IS 대조 완료)

> 작성: 2026-07-02. 조사 방법: 원본(DCGO/Assets/Scripts) 키워드 5종 전수 대조 + 포트(src/) 배선 상태 전수 grep. 기준선: **291/291 green, RuleAudit 0** (HEAD `2ea1d06a`).
>
> **✅ 구현 완료 (2026-07-02, 같은 세션):** D1(G9-055 +4) · K1(GR-006 +4, G9-039 un-flatten) · K2(F68 top-삽입 단언; yes/no는 기존 POST 창으로 이미 충족 확인) · K3(C910 +1) · K4(G9-039 +2, 소비자 8곳 중 7곳 치환 — SecurityResolver 시큐리티-카드 판정은 AS-IS상 비적용이라 제외) · K5(G9-060 신설 6, MindLinkClass + PlayMindLinkTamer 교정) · M-5 close. 잔여 debt는 [fidelity_debt.md](fidelity_debt.md) "M-4/M-5 잔여 구현" 절.
> 상위 백로그: [fidelity_master_goals.md](fidelity_master_goals.md) M-4(키워드-동작)·M-5. 공통 규율: [[check-asis-before-implementing]] · [[fidelity-over-coverage]].

## 0. 정정 — master goals의 "소비자 0" 목록은 스테일

`Collision·Vortex·Ascension·TreatAsDigimon·MindLink 등: HasKeyword 소비자 0` (M-4 키워드-동작 줄) 중 3종은 이미 소비자 있음:

| 키워드 | 포트 소비자 | 실제 잔여 |
|---|---|---|
| Collision | `BlockTiming.cs:49,276` (S4/G3.5-C910) | 검증 갭 1건 (K3) |
| Vortex | `EndOfTurnEffectAttack.cs:54` → `EffectDrivenAttack` (GR-006) | 플레이어-타깃 경로 (K1) |
| Ascension | `DeletionReplacementGate.cs:376 TryAscensionAsync` (S3/G9-058) | 검증 갭 3건 (K2) |
| **TreatAsDigimon** | **0** (grant만, `CardPortingFramework.cs:3458`) | 전체 미배선 (K4) |
| **MindLink** | **0** (grant만, `CardPortingFramework.cs:3362`) | 전체 미배선 (K5) |

M-5 두 건은 AS-IS 대조 결과 **둘 다 현재 포트가 1:1** — 구현 불요, 항목 close (§6).

## 공통 종료 기준

- 구현 전 해당 항목 원본 코드 재확인(본 문서의 AS-IS 절 + 인용 파일), 추측 금지.
- 라이브 평가 미러(술어는 체크 시점에 평가, 저장-스냅샷은 AS-IS가 그럴 때만). 메타 브릿지·flattening 금지.
- 각 항목: 동작-단언 테스트(매칭/비매칭 대조 포함) + `bash scripts/run-tests.sh` green + `tools/RuleAudit` 0.
- 미모델 발견 시 STOP + [fidelity_debt.md](fidelity_debt.md) 기록. 커밋은 지시 시.

---

## D1. Decoy `permanentCondition` refinement (원본 사용 12장 — 전부 non-null 술어)

**AS-IS** (`CardEffectFactory/KeyWordEffects/Decoy.cs:51`, `CardEffectCommons/KeyWordEffects/Decoy.cs:10,25`):
- 술어는 **보호 대상**(삭제될 다른 permanent)에 평가된다. 홀더가 아님:
  `CanSelectPermanentCondition(p) = p != 홀더 && IsPermanentExistsOnOwnerBattleAreaDigimon(p, card) && (permanentCondition == null || permanentCondition(p))`
- 평가 시점: 라이브 2회 — 트리거 판정(`CanTriggerWhenPermanentRemoveField`) + 보호 대상 선택 필터.
- 적-효과 게이트: 원인 효과 소스 owner == 홀더 owner의 적.
- 홀더 측 조건은 별개: `CanActivateDecoy` = 홀더 배틀에리어 존재 && `CanBeDestroyedBySkill`. 홀더 삭제가 먼저, 성공 시에만 보호 대상 스페어.
- 사용 예: BT6_064(black && willBeRemoveField), EX3_046(D-Brigade), BT11_082(Bagra Army) — 12건 전부 술어 전달.

**현재 포트 갭**:
- `DecoySelfEffect`(`CardPortingFramework.cs:3253`)가 `permanentCondition`을 받고 **버림** → 술어 미저장.
- `DeletionReplacementGate.FindDecoyRedirect`(`:135`)의 `candidateCondition`은 **Decoy 후보(홀더)** 에 적용 — AS-IS 술어와 의미가 다르다(혼동 주의).
- `FindDecoyRedirect`에 `EngineContext` 없음(repository/zones/registry만) → 저장 술어를 대상 CardSource로 평가할 수단 없음.

**설계**:
1. **grant에 술어 저장**: Decoy grant 등록 시 `ScopePred(permanentCondition)` 어댑터(기존 `CardPortingFramework.cs:3270`)로 `Func<CardSource,bool>`을 grant 레코드 메타 `DecoyProtectPredicateKey`에 저장. `SelfKeywordByNameEffect`에 선택적 predicate 인자 추가(다른 by-name grant는 무영향, null 기본).
2. **평가 지점**: `FindDecoyRedirect`/`Candidates`에서 각 Decoy 후보의 저장 술어를 **보호 대상(target)** 에 평가 — 술어 null이면 현재와 동일(전부 보호) = 기존 12건 null-예외 없음이므로 신규 경로만 활성.
3. **context 스레딩**: `FindDecoyRedirect(..., EngineContext? context = null)` 추가, `ContinuousKeywordGate.ScopePredicatePasses`(`:108`) 패턴으로 `new CardSource(context, targetId, owner, owner)` 구성해 평가. 호출부는 G9-055에서 registry를 스레딩한 지점(sink·DeletionReplacementTiming)에 context 전달(가능하면 registry 파라미터를 context 경유로 통합).
4. AS-IS의 `p != 홀더`(자기 자신 보호 불가)와 "보호 대상 = 같은 owner 배틀에리어 **디지몬**" 조건이 포트에 이미 있는지 검증 — 없으면 함께 폴딩.

**테스트**: 술어형(예: 특정 색/특성 매칭) Decoy — ① 매칭 대상 삭제 → redirect 후보 인정, ② 비매칭 대상 삭제 → redirect 안 열림, ③ 술어 null 회귀(기존 G9-055 테스트 유지).
**범위 밖(기존 debt 유지)**: first-candidate 결정론(AS-IS는 "select 1" 선택 surface) — `DeletionReplacementGate.cs:132` LIMITATION 문서화됨.

---

## K1. Vortex — 플레이어-타깃 경로(VortexCanAttackPlayers) (원본 13장)

**AS-IS** (`CardEffectCommons/KeyWordEffects/Vortex.cs:7,19,56`, `CardEffectFactory/VortexCanAttackPlayers.cs`):
- `CanActivateVortex` = 공격 가능(`isVortex` 경로) && (공격 가능한 상대 **디지몬** 존재 **|| `IVortexCanAttackPlayersEffect` 활성**).
- `VortexProcess`: `canAttackPlayers`를 프로세스 시작 시 **1회 스냅샷** → `SelectAttackEffect.canAttackPlayerCondition`. 선택은 optional.
- `isVortex`가 우회하는 것은 **소환 멀미뿐**(`Permanent.cs:2244-2250`) — 미서스펜드 필수, 탭 정상.
- `VortexCanAttackPlayersStaticEffect(attackerCondition, ...)`: 공격자 술어 + `!attacker.TopCard.CanNotBeAffected(효과)` 가드(`VortexCanAttackPlayers.cs:77`), 전 플레이어 필드 스캔.

**현재 포트**: GR-006으로 끝단 공격창 자체는 LIVE(`EndOfTurnEffectAttack`). 서스펜드 제외 ✓(`:61`). 그러나 `VortexOptions.AllowPlayerTarget: false` **하드코딩**(`:29`). `VortexCanAttackPlayersStaticEffect` 팩토리는 존재(PRIM-W4 `CardPortingFramework.cs:3446`)하나 소비자 확인 필요 — **주석이 "grants Vortex"라 flatten 의심**(사실이면 FAIL 교정 대상).

**설계**:
1. **probe-first**: `VortexCanAttackPlayersStaticEffect`가 실제 emit하는 것 확인. Vortex 키워드로 flatten돼 있으면 전용 grant(예: `VortexCanAttackPlayersKey` + attackerCondition ScopePred 저장)로 교정.
2. `EndOfTurnEffectAttack.TryOpen`에서 Vortex 오퍼 직전에 활성 `VortexCanAttackPlayers` 이펙트 평가(attackerCondition(공격자) && CanUse && 공격자 `CanNotBeAffected` 아님) → `EffectAttackOptions.AllowPlayerTarget`를 동적으로. 오퍼 시점 1회 평가 = AS-IS 스냅샷 시맨틱 미러.
3. 타깃 없음 판정도 AS-IS 미러: 디지몬 타깃 0이어도 canAttackPlayers면 오퍼 열림.

**테스트**: ① Vortex 단독 → 디지몬 타깃만(플레이어 후보 없음), ② + VortexCanAttackPlayers 활성 → 플레이어 타깃 후보 추가(상대 디지몬 0이어도 오퍼), ③ attackerCondition 비매칭 공격자 → 플레이어 타깃 없음.

---

## K2. Ascension — 기배선 검증 갭 3건 (원본 2장: BT25_034·040)

**AS-IS** (`CardEffectCommons/KeyWordEffects/Ascension.cs:10,29,113`):
- 트리거: 홀더 permanent 삭제(`OnDestroyedAnyone`), Activate: 카드가 **실제 trash 도달**.
- 프로세스: `CanAddSecurity(activateClass)` 게이트 → owner **yes/no 선택**("Will you place this card in security?") → yes 시 `AddSecurityCard(card, true)` = 시큐리티 **top**.

**현재 포트** (`DeletionReplacementGate.cs:376 TryAscensionAsync`): 키워드 인식 ✓, trash 도달 체크 ✓. 갭:
1. **선택 없음** — 무조건 이동(AS-IS는 optional). 강제 발동 = 오작동.
2. `MoveAsync(Trash→Security)` 삽입 위치 미명시 — AS-IS는 top(`Insert(0)`). `AddToSecurityAsync(toTop:true)` 경유가 구조 미러(주석도 AS-IS `AddSecurityCard` 인용).
3. `CanAddSecurity` 상당(시큐리티 추가 제한) 게이트 미적용 — 포트에 해당 제한 게이트 존재 여부 probe, 없으면 STOP+debt.

**설계**: 호출부(양 삭제 경로)에서 S3 삭제-치환 옵션-게이팅과 동일 패턴으로 owner 선택 surface → yes 시 `AddToSecurityAsync(owner, cardId, faceDown, toTop:true)`. `AscendedKey` 마킹 유지.
**테스트**: ① yes → security[0]에 추가(순서 단언), ② no → trash 잔류, ③ (게이트 존재 시) 추가 불가 상태 → 오퍼 없음.

---

## K3. Collision — 기배선(S4) 검증 갭 (원본 35장)

**AS-IS** (`Permanent.cs:2396-2417`, `AttackProcess.cs:333,355`):
- 강제 블로커 부여 시 **defender별** `!TopCard.CanNotBeAffected(fakeCollisionClass)` 가드 — fake 효과 소스 = **공격자 TopCard**(즉 "상대 디지몬 효과에 영향받지 않음" 보유 디지몬은 강제블록 면제).
- 후보는 여전히 `CanBlock` 통과 필요; 부여되는 것은 `HasBlocker`와 no-block 옵션 제거.
- 엣지: `HasCollision` 스캔은 **face-up 시큐리티 카드의 OnCounterTiming 효과**도 포함(`Permanent.cs:3069-3087`).

**설계**:
1. `BlockTiming` 후보 생성(`:49` 인근)에서 Collision 강제 경로에 defender별 `ContinuousImmunityGate`(원인 소스 = 공격자 카드) 평가 추가 — 이미 적용돼 있으면 검증 테스트만.
2. 시큐리티-카드 효과 스캔 엣지: 해당 형태 카드 존재 census 후 결정 — 사용 0이면 debt note로만.

**테스트**: CanNotAffected(상대 디지몬 효과) 보유 수비 디지몬 → Collision 강제블록 대상에서 면제(다른 디지몬은 여전히 강제).

---

## K4. TreatAsDigimon — 중앙 chokepoint 신설 (원본 3건 사용, 소비자 0)

**AS-IS** (`Permanent.cs:3438-3503`, `CardEffectFactory/TreatAsDigimon.cs:10`):
- 단일 소비자 = `Permanent.IsDigimon` getter: native(`IsDigimon||IsDigiEgg`) → 자기 TopCard의 `ITreatAsDigimonEffect` → 전 필드 permanent(`EffectList_Added`) → 플레이어 이펙트, 전부 라이브 평가.
- `permanentCondition`은 **판정 대상 permanent**에 평가(`IsPermanentExistsOnBattleArea` 가드 포함).
- 사용: BT25_104, `TamerBecomesDigimonThatCanNotDigivolve.cs`(테이머를 디지몬 취급).

**현재 포트**: `private static bool IsDigimon(CardRecord)` 지역 헬퍼가 **산재** — `AttackPermanentAction.cs:345`, `BlockTiming.cs:292`, `SecurityResolver.cs:243`, `BattleResolver.cs:435`, `RaidAttackSwitch.cs:223`, `OverclockEffect.cs:161`, `AllianceAttackBoost.cs:233`, `HeadlessLegalActionDispatcher.cs:121`(CanMove). grant는 `SelfKeywordByNameEffect(TreatAsDigimon)` + `TreatAsDigimonStaticEffect(ScopePred)` 존재.

**설계 (구조 미러 — 원본처럼 단일 chokepoint)**:
1. 중앙 헬퍼 `ContinuousKeywordGate.IsDigimon(context, id)` = native CardType 체크 || `HasKeyword(TreatAsDigimon)`(ScopePredicatePasses로 **대상 카드**에 술어 평가).
2. 산재 헬퍼를 중앙 헬퍼 호출로 치환. context 없는 시그니처는 스레딩(S3의 registry 스레딩과 동일 패턴). **치환 우선순위**: 공격·블록·타깃 적격(AttackPermanentAction → BlockTiming → BattleResolver → RaidAttackSwitch) → SecurityResolver → dispatcher(CanMove). 한 파일씩 green 게이트.
3. native-fallback: context 못 받는 지점이 남으면 그 지점은 native만 유지 + debt 기록(어느 판정이 키워드 미인식인지 명시).

**테스트**: TreatAsDigimon 부여 테이머 — ① 공격 타깃 적격, ② 블로커 적격, ③ CanMove 판정에서 디지몬 취급; ④ 술어 비매칭 permanent 아님; ⑤ 키워드 없는 테이머 회귀(비적격).

---

## K5. MindLink — 프로세스 프리미티브 신설 (원본 11장, 소비자 0)

**AS-IS** (`CardEffectCommons/KeyWordEffects/MindLink.cs:8,17,38`):
- **키워드가 아니라 코루틴 프로세스** — 카드 스크립트가 `OnDeclaration`에서 `new MindLinkClass(tamer, digimonCondition, activateClass).MindLink()` 직접 호출.
- `CanSelectPermanentCondition(p)` = 테이머 배틀에리어 && p가 같은 owner 배틀에리어 && `!p.IsToken` && **p.DigivolutionCards에 face-up 테이머 0** && digimonCondition(p).
- 선택 optional(max 1) → `IPlacePermanentToDigivolutionCards`: 테이머 **permanent**를 선택 디지몬의 진화원 **bottom**에 배치.
- `HasMindLink`(`Permanent.cs:2923`)는 **UI 전용**(설명문 문자열 sniff, `PermanentDetail.cs:190`만 소비) — 게임 로직 아님, 포팅 불요.
- `IsLinked`/`IsLinkedEffect`는 **별개 Link 메커니즘**(G9-056에서 언실) — MindLink와 무관.
- 역방향(`PlayMindLinkTamerFromDigivolutionCards`)은 이미 포팅됨(PRIM-W4 `CardPortingFramework.cs:3552`).

**현재 포트**: `MindLinkSelfEffect` 키워드 grant만(라틴트). **AS-IS에 없는 형태이므로 load-bearing 금지** — 표시/식별용으로만 유지.

**설계 (구조 미러)**:
1. Commons `MindLinkClass(tamer, digimonCondition, activateClass)` + `MindLink()` 신설(원본 파일 위치·시그니처 1:1) — 포트의 SelectPermanent 초이스 + `AddDigivolutionCardsBottom`(DigivolutionStackHelpers) 재사용.
2. 선택 조건 4항 전부 술어로 1:1. **probe-first 2건**: ① 포트 진화원에 face-up/flipped 모델 존재 여부("face-up 테이머 0" 판정) — 없으면 STOP+debt, ② 배치 primitive의 단위 — AS-IS는 테이머 **permanent 통째**(그 카드 스택 포함) 배치이므로 포트 primitive가 카드 단위면 스택 전체 이동으로 처리.
3. optional 선택(`canNoSelect:true`) 미러 — 선택 안 함 허용.

**테스트**: ① 조건 매칭 디지몬 선택 → 진화원 bottom에 테이머 카드(순서 단언) + 테이머 permanent 필드 이탈, ② face-up 테이머 이미 보유 디지몬·토큰 제외, ③ digimonCondition 비매칭 제외, ④ 선택 안 함 가능(no-op), ⑤ 이후 `PlayMindLinkTamerFromDigivolutionCards`로 되꺼내기 왕복(기존 PRIM-W4 테스트와 연결).

---

## 6. M-5 종결 — 둘 다 검증 완료, 구현 불요

- **ReplaceBottomSecurity ✅ 1:1**: AS-IS 시큐리티 순서 = **index 0 top / `Last()` bottom**(`CardObjectController.cs:976 AddSecurityCard Insert(0)`, `CardController.cs:3945 security[0]` 브레이크, `CardEffectFactory.cs:645 Last()` 회수·`toTop:false` 배치). 포트 `CardPortingFramework.cs:2128 security[^1]`·`SecurityResolver.cs:116 security[0]` 동일 → "바닥=마지막 원소" 가정 **확인됨, 항목 close**. (선택 검증 1건: AS-IS는 ReduceSecurity/AddSecurity 트리거 창 경유 — 포트 sink의 security-감소/추가 트리거 발화 단언 테스트 권장.)
- **RevealLibrary ✅ 1:1**: AS-IS `RevealLibraryClass`(`RevealLibrary.cs:737`)는 라이브러리 **비파괴 read + UI/로그 + `IsBeingRevealed` 플래그**뿐(플래그 게임로직 소비자 0, 호출자가 클리어). 후속 행위(필터→핸드/트래시/덱)는 바깥 helpers(`RevealDeckTopCardsAndSelect` 등) 소관 — 포트는 `RevealAndSelect`/ChoiceType B-7로 보유. 풀정보 모델에서 정보성 no-op(`InformationalRevealEffect`)이 정확히 1:1 → **항목 close**.

## 7. 우선순위 제안 (오작동 심각도 × 카드 수)

| 순서 | 항목 | 근거 |
|---|---|---|
| 1 | **D1 Decoy 술어** | 사용 12장 **전부** 술어 전달 = 현재 전부 과보호(비매칭 대상도 스페어) — 실오작동 |
| 2 | **K1 Vortex player-target** | 13장, 창은 LIVE인데 플레이어-타깃 경로 결손 + flatten 의심 확인 |
| 3 | **K5 MindLink** | 11장 완전 inert, 프리미티브 신설 필요(선행개발 원칙상 강모델 몫) |
| 4 | **K2 Ascension 3갭** | 2장이나 "optional→강제"는 룰 위반 방향 오작동 |
| 5 | **K3 Collision 가드** | 35장 기반이지만 기배선, 갭은 면역 엣지 |
| 6 | **K4 TreatAsDigimon** | 3건 사용, 산재 치환 sweep이라 작업량 대비 빈도 낮음 |
| 7 | M-5 close + master goals 정정 | 문서만 |

## 실행 대화문 (복붙용)
```
M-4/M-5 잔여 진행. docs/audit/fidelity_m4m5_design.md 우선순위대로(D1 Decoy 술어 → K1 Vortex player-target → K5 MindLink → K2 Ascension → K3 Collision 가드 → K4 TreatAsDigimon → M-5 close).
각 항목: 설계 문서의 AS-IS 절 원본 재확인 → probe-first 항목 먼저 해소 → 라이브 술어 평가로 배선(메타 브릿지·flattening 금지) → 동작-단언 테스트(매칭/비매칭) + bash scripts/run-tests.sh green + tools/RuleAudit 0. 이전 항목 green 후 다음. 미모델은 STOP+fidelity_debt. 커밋은 내가 지시할 때.
```
