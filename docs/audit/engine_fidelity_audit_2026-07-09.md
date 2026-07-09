# 엔진 전체 AS-IS 1:1 충실도 감사 (2026-07-09)

5개 규칙-집행 서브시스템(restriction·immunity·continuous-DP·battle·digivolve)을 AS-IS(`DCGO/`)와 헤드리스(`src/HeadlessDCGO.Engine/`) 구조 대조. **★=작성자 직접 AS-IS 원본 재검증**, 그 외=감사자 보고(고신뢰).

## 종합 판정
- **restriction/immunity joint 마이그레이션(이번 작업)은 충실 확인**: split 진짜 제거, CanUse 게이트 존재, 그리고 AS-IS 그랜트가 `!CanNotBeAffected`를 **소비자 루프가 아니라 grant의 CanUseCondition에 내장**하는 것을 헤드리스 liveCondition/scopePredicate가 1:1 미러(감사자 확인). → 발명/shortcut 아님.
- **그러나 엔진은 아직 완전 1:1 아님**: 포팅 중 발견돼야 할 **기존 엔진 버그** 존재. 특히 continuous-DP에 **검증된 P0 수치 버그 2건**(내 작업과 무관).

---

## P0 — 실수치/실동작 버그 (즉시 상환 대상)

### ★ P0-DP-1: DP 0-clamp 누락 (수치 발산)
- AS-IS `Permanent.cs`: BaseDP 폴딩 후 `if(BaseDP<0) BaseDP=0`(중간 클램프, ~314) + 최종 `if(DP<0) DP=0`(~662). GetDP도 동일(491-494).
- 헤드리스 `ContinuousDpGate.ResolveDp`: `ModifierHelpers.Evaluate(BaseDp,...)` + `ResolveDp(...)` 둘 다 `minimumValue=int.MinValue`(0-floor 없음), 호출부 클램프 없음.
- 시나리오: base 1000, base-minus −3000, buff +2000 → AS-IS=clamp(−2000)→0,+2000=**2000**; 헤드리스=−2000+2000=**0**. 음수 DP가 배틀 비교로 유입.

### ★ P0-DP-2: isUpDown/NotIsUpDown 적용 순서 역전
- AS-IS DP getter: `isUpDown 그룹 먼저 → DP+=LinkedDP → NotIsUpDown 그룹`(628-651). 분류는 카드별 `IChangeDPEffect.IsUpDown()` 플래그(add/set 무관).
- 헤드리스 `ModifierHelpers.ModifierOrder`(568-577): Set→0, Add(isUpDown?2:1). Set()은 isUpDown=false 강제 → order 0(먼저). 즉 notUpDown/Set을 먼저, upDown을 나중 = **AS-IS와 완전 역전**.
- 시나리오: "DP 4000으로"(set/notUpDown) + "+2000"(upDown) → AS-IS: +2000(3000→5000)→set 4000=**4000**; 헤드리스: set 4000→+2000=**6000**.
- ⚠️ 수정 시 DP는 load-bearing → ModifierOrder를 `isUpDown 먼저`로 바꾸면 다수 카드/테스트 영향. isUpDown 태그가 포팅에서 정확히 세팅됐는지도 병행 확인 필요.

### P0-restr(narrow): printed player-scope "cannot block/attack"가 immunity 면제 누락
- AS-IS 정적 player-scope cannot-block/attack은 `!TopCard.CanNotBeAffected`로 면역 대상 면제(Permanent.cs:2194/2267/2290).
- 헤드리스 `ContinuousPlayerScopeRestrictionEffect.ToBinding`는 joint에 immunity 항 없음(그랜트 producer에만 있음). → printed player-scope × 상대효과-면역 조합서 over-strict(합법 블록/공격을 불허).
- narrow(정적 player-scope 제한 + 면역 대상 동시 필요)이나 wrong legal-action set.

---

## P1 — 구조 발산 / latent 동작 갭

### ★ P1-DP-3: 연속 DP 폴딩에 CanNotBeAffected 미적용
- AS-IS: 각 ChangeDP/ChangeBaseDP/InvertSAttack 후보를 `!TopCard.CanNotBeAffected(cardEffect)`로 게이트(Permanent.cs:229/257/534/567/595/1696/1713)한 뒤 폴딩.
- 헤드리스: 면역을 mutation SINK에만 중앙화. `ContinuousDpGate.ResolveDp`/`ContinuousScopeEvaluation.ApplicableEffects`는 면역 미조회(EffectInvalidation+condition만). DP는 read-time 계산값이라, 상대의 DP buff/debuff가 상대효과-면역 카드에도 도달.

### ★ P1-DP-4: base-DP-minus 면역 우회
- 헤드리스 `IsDpReduction`은 `Metric==Dp`만 매칭 → `BaseDp` 음수 modifier는 면역 필터 미적용. AS-IS는 `IChangeBaseDPEffect.IsMinusDP`로 base 감소도 면역(221-227).

### P1-DP-5: LinkedDP / DPBoost 미폴딩
- AS-IS: `DP += LinkedDP`(isUpDown↔NotIsUpDown 사이) + 말미 `Boosts`. 헤드리스 ResolveDp는 둘 다 없음 → 링크드 디지몬 배틀 DP서 LinkedDP 누락.

### P1-DP-6: HasDP / IsDigimon flatten
- `HasDP`: AS-IS는 TopCard→IsDigimon→(!HasDP&&IsDigiEgg)→`IDontHaveDPEffect.DontHaveDP(this)` 스캔. 헤드리스는 `DontHaveDpKey` 존재만 확인.
- `IsDigimon`: AS-IS는 IsFlipped→false, `IsDigimon||IsDigiEgg`→true, TreatAsDigimon 스캔. 헤드리스 `ContinuousKeywordGate.IsDigimon`은 **IsDigiEgg→true 누락** + IsFlipped 가드 누락.

### P1-DV-1/2/3/4: digivolve ignore 계열
- 플레이어-개시 Validate에 **ignore-LEVEL-only 분기 누락**(All/Color만; effect-path서 완화).
- **added-requirement 경로가 CannotIgnore negation 미체크**(P1-2): 상대 "cannot ignore digivolution requirement" 활성 중에도 added-ignore 경로 허용(AS-IS는 void).
- `CanIgnoreDigivolutionRequirement`가 AS-IS negation-only 스캔이 아니라 grant-flag 스캔으로 **역할 반전**(P1-3) → AddedLevelGate가 negation 대신 grant 참조.
- color-ignore가 negation에 과결합(P1-4): AS-IS는 `IIgnoreColorConditionEffect`를 CannotIgnore 게이트 없이 독립 스캔.

### P1-PG: IsPlayerRestricted(AddSecurity/Memory) CanUse 게이트 누락 + PlayerScope 태그 요구
- 헤드리스 `IsPlayerRestricted`(sink:841)는 `ConditionKey`(CanUse) 미평가 → 조건부 add-제한이 조건 false여도 적용(over-strict). 또 `PlayerScopeKey` 태그 요구 → 태그 없는 permanent-effect형 CannotAddMemory/Security 누락(AS-IS는 permanent+player 무조건 스캔).
- `CanAddMemory ≥10` 캡 / `IsSecurityLooking` 재확인 누락(P2급).

### P1-IMM/BAT: face-up 시큐리티 삭제/면역 population 누락
- AS-IS `CanBeDestroyedByBattle`/`CanNotBeAffected`는 필드 permanent + **face-up 시큐리티 카드 자체 효과** + player 3-population 스캔. 헤드리스는 단일 continuous 쿼리로 flatten → 시큐리티 카드 자체가 방출하는 배틀삭제-면역 미스캔(현재 latent, 해당 카드 미포팅).

---

## P2 — 구조 flatten (현재 런타임 무영향)

- ★ immunity context-less 폴백 양방향 발산: **프로덕션 死코드**(모든 실매치 싱크가 context 주입 확인). AS-IS는 context-less로 안 도니 no-op(return false)로 교체 권장.
- CanNotBeAffected 3영역(permanent/player/self-offfield) → 단일 스캔 flatten; off-field self 가드(`PermanentOfThisCard()==null`) 드롭.
- `CanBeDestroyed()` base pre-gate 2-tier → 단일 스캔 flatten.
- 배틀 삭제-대체: AS-IS 단일패스 would-be-deleted 창 vs 헤드리스 라운드-루프(nested-coroutine 부재로 **불가피한 아키텍처 차이**). Retaliation 인라인.
- 면역 per-add 게이트 vs RemoveAll; Piercing activated→bool; direct-attack "cannot attack player" 전용 플래그 split; CanBlock 면역 비대칭 flatten(masked); CanSelectBySkill이 player-scope까지 union(AS-IS는 permanent만); 공격 가드 순서 차이(합집합 동일).
- Piercing 이름 변형 `"Pierce"`/`"Piercing"` — **확인완료(2026-07-09)**: 헤드리스는 grant·read 모두 단일 정식명 `ContinuousKeywordGate.Piercing="Piercing"`로 정규화(KeywordBaseBatch1:339·PierceSelfEffect·PiercingStaticEffect). AS-IS의 dual-name은 effect-name("Pierce") vs keyword-name substrate 아티팩트로 `HasPierce`가 둘 다 수용하는 것; 헤드리스는 단일명 일관 사용이라 grant==read 매칭, 갭 없음. (Piercing-as-activated 기각과 함께 검증)

---

## 상환 진행 (2026-07-09)
- **★P0-DP-1 완료** (371/371·RuleAudit 0): `ContinuousDpGate.ResolveDp`에 이중 클램프(`Math.Max(0, effectiveBase)` + `Math.Max(0, final)`). ST3_16 테스트가 언클램프 −2000을 인코딩했던 것 → AS-IS-correct 0으로 수정.
- **★P0-DP-2 완료** (371/371·RuleAudit 0): `ModifierHelpers.ModifierOrder`를 **metric-aware**로. 근본원인=헤드리스가 3 metric의 상이한 AS-IS 순서를 단일 order로 flatten(DP=isUpDown-first, Cost=NotIsUpDown-first[CardSource.cs:848], SAttack=3-tier UpToConstant→UpDownValue→DownToConstant[Permanent.cs:1900-1930]). DP/BaseDp만 isUpDown-first로 교정, cost/SAttack은 Set-first 유지. 잔여: SAttack 3-tier를 2값 isUpDown로 근사(latent P2 — UpToConstant/DownToConstant 구분 불가).

## P1 상환 진행 (2026-07-09)
**완료(각 371/371):**
- ★P1-DP-3: `ContinuousDpGate.ResolveDp`에 일반 CanNotBeAffected 필터 추가 — modifier의 source에 대상이 면역이면 drop(self-source는 opponent 아니라 유지). AS-IS `!TopCard.CanNotBeAffected(cardEffect)` 미러.
- ★P1-DP-4: `IsDpReduction`을 `BaseDp` 음수도 매칭 → base-DP-minus 면역(ImmuneFromDPMinus) 적용. AS-IS Permanent.cs:221-227.
- ★P1-PG: `IsPlayerRestricted`에 CanUse(ConditionKey) 게이트 추가 — 조건부 add-제한이 조건 false여도 차단하던 것 교정.

**추가 완료:**
- ★P1-DP-5 (LinkedDP 폴딩): `ContinuousDpGate`가 host의 `linkedDp`(LinkHelpers)를 AS-IS 위치(isUpDown↔NotIsUpDown 사이, `ModifierHelpers.LinkedDpModifierId` 전용 order tier)로 주입. D1L 테스트에 폴딩(→5000)+set-덮어쓰기(→4000) 검증 추가. (DPBoost는 헤드리스 미표현 — 별도 feature.)
- ★P1-DP-6 (IsDigimon IsDigiEgg) **검증 후 수정 안 함(의도적 정합)**: 헤드리스 move-gate(dispatcher:112, GR-002+회귀)가 "hatched Digi-Egg는 digivolve 전 move 불가"를 IsDigimon(DigiEgg)=false로 구현. AS-IS는 IsDigimon(DigiEgg)=true지만 CanMove의 별도 breeding-frame 가드(Permanent.cs:2055+)로 egg-move 차단 = **같은 결과, 다른 구조**. DigiEgg는 breeding 전용이라 attack/block/battle 미도달 → IsDigimon=false 무해. IsDigiEgg→true로 고치면 GR-002 회귀 위험 → **변경 금지**. IsFlipped 가드는 latent(face-down 카드가 IsDigimon 호출부에 미도달).

**추가 완료:**
- ★P1-DV-2 (added-requirement 경로 negation 게이트): `AddedLevelGatePasses`의 ignore-level waive를 `CanIgnoreDigivolutionRequirement(grant) && !IsDigivolveIgnoreBlocked(negation)`으로 게이트 — AS-IS `ignore==Level/All && CanIgnoreDigivolutionRequirement`(AddDigivolutionRequirement.cs:64-72) 미러. CannotIgnore 활성 중 added-ignore 경로가 통과하던 것 차단(CannotIgnore 활성 시에만 더 제한적).

**미완(복잡/latent — 개별 신중 처리 필요, 대부분 narrow):**
- P1-DV-1 (player-Validate ignore-LEVEL-only 분기 누락): effect-driven path서 완화. narrow.
- P1-DV-3/4 (CanIgnore grant vs negation 네이밍·color-ignore가 IIgnoreColorConditionEffect[미negation-gate]인지 확인): grant×negation 로직은 P1-DV-2로 일관 적용됨; 네이밍/color 매핑은 설계 검증 필요. narrow(CannotIgnore+color 동시).
- **P0-restr (printed player-scope immunity)**: ★재검증 — ContinuousPlayerScopeRestrictionEffect가 다수 kind(Suspend·Digivolve·Return·Delete·Block·Attack)에 쓰이고 AS-IS는 kind별 CanNotBeAffected 체크 상이 → uniform 추가 시 Suspend 등 over-lenient. **정확한 per-kind immunity 테이블 필요 → uniform 수정 금지**. 현재 printed player-scope attack/block × 면역 subject 조합은 latent(해당 카드 미포팅으로 추정).
- P1-PG 잔여 (CanAddMemory ≥10 캡·IsSecurityLooking 재확인·PlayerScope 태그 요구), P1-IMM/BAT faceup 시큐리티 population: latent.

## 구조 복원 진행 (strict 기준, 2026-07-09)
**완료:**
- ★ProgressImmunity(C-15) 프로덕션 버그 + context-less 폴백 제거 + 死키 ImmunityFromOpponentOnlyKey 제거 (`43f91829`)
- ★direct-attack "플레이어 공격불가" 死 플래그 레이어 제거 → AS-IS 통합 null-defender 스캔 (`b47f2ffa`)
- ★배틀 삭제 per-add 게이트 복원(RemoveAll→add-전 게이트) (`b47f2ffa`)

**검증→cosmetic/결정점:**
- P1-DV-3 (CanIgnore 역할): 로직 매핑 1:1(헤드리스 grant-flags↔AS-IS `ignore` 속성, IsDigivolveIgnoreBlocked↔AS-IS CanIgnore negation; P1-DV-2로 `grant && !negation` 일관화) → 네이밍만 cosmetic, 수정 불요.
- ★**IsDigimon(IsDigiEgg) — 사용자 결정: AS-IS 1:1 유지, 수정 완료.** `ContinuousKeywordGate.IsDigimon`을 AS-IS 구조로 복원: IsFlipped(메타 isFlipped)→false, `IsCardType("Digimon") || IsCardType("DigiEgg")`→true. egg-move 차단은 move-gate의 `IsDigiEgg && DP<=0` 게이트가 담당(AS-IS Permanent.cs:2069-2071). 실 egg는 DP<=0라 GR-002 유지, DP>0 egg는 AS-IS대로 이동 가능. 회귀 371.

**검증→substrate 번역/수용으로 판정(수정 불요, 로직 동일):**
- CanNotBeAffected 3영역 flatten: AS-IS 3 루프는 Unity object-model(3 collection) 아티팩트, 헤드리스 레지스트리 단일 스캔이 그 substrate 번역(전 효과가 Scope 등록). off-field self 가드는 double-scan 방지용이라 레지스트리 모델 불필요.
- CanBeDestroyed base pre-gate: 헤드리스도 2-tier(Delete/Prevent 대체 + PreventBattleDeletion 플래그) 순서 유지.
- 공격 가드 순서: 순수 conjunction이라 순서 무영향(AND 피연산자 순서).
- HasDP: DontHaveDp producer가 ContinuousSelf/PlayerScopeRestrictionEffect 경유라 조건/스코프 술어가 ApplicableEffects 스코프-필터로 적용(AS-IS DontHaveDP(this) 술어 동치). IsDigimon/DigiEgg 게이트는 배틀서 Digimon만 DP 해소해 moot.

## #12 처리 (2026-07-09)
**기각 기준 정정(사용자 지시):** "현재 호출/producer 없음"은 기각 사유 **아님**. 기준은 **AS-IS에 그 메커니즘이 존재하는가** — 존재하면 카드 포팅 때 호출되니 **구축(=FAIL, PASS 아님)**, 부재면 발명 금지. [[strong-model-prebuild-latent-infra]]

**기각(AS-IS 메커니즘 부재 — 구축 시 발명):**
- **ignore-level-only**: AS-IS엔 continuous IgnoreLevelConditionClass 부재(effect-driven IgnoreRequirement.Level만, 헤드리스 effect-path가 처리). 별도 continuous 그랜트는 발명 → 원복 유지.
- **CanSelectBySkill over-scope**: AS-IS 메커니즘(permanent-scoped)은 존재하고 헤드리스도 보유; 헤드리스가 player-scope까지 스캔하는 건 "더 넓음"(발명이 아니라 과-scope). 저위험 note.

**구축 완료(각 신규 동작 테스트 + 회귀):**
- ★**DPBoost**: AS-IS Permanent.Boosts/AddBoost/RemoveBoost/DPBoost 메커니즘 존재(현재 미호출이나 구축 대상) → `DpBoostHelpers`(id→dp metadata, AS-IS upsert-by-ID) + ContinuousDpGate 말미 fold(NotIsUpDown 후, clamp 전). 테스트 DPB-DpBoostFold.
- ★**P0-restr kind별 immunity**: printed player-scope cannot-attack/block(producer 6196/6320)에 immunity 항 embed — AS-IS immunity-체크 kind(Attack 2267/2290·Block 2194)에만(suspend/move는 미체크라 제외). 테스트 P0R-PrintedPlayerScopeImmunity(면역 subject exempt).

**구축 완료(신규 세션 2026-07-09, 순차):**
- ★**SAttack 3-tier**: AS-IS `Strike_AllowMinus`/`Strike`가 IChangeSAttackEffect를 isUpDown()→CalculateOrder 3-tier(UpToConstant→UpDownValue→DownToConstant, Permanent.cs:1872-1930)로 버킷·순차 fold; switch에 UpValue/DownValue case 부재 → 그 tier는 collected-but-never-applied(drop). LinkedMax도 동일 switch(Permanent.cs:975-1000). 헤드리스 NumericModifier는 bool isUpDown(DP/Cost용 2-group 축)만이라 3-tier 표현 불가였음 → `CalculateOrder` enum(AS-IS 미러) + NumericModifier.CalcOrder 필드(default UpDownValue) 추가, ModifierOrder를 SecurityAttack/LinkedMax metric에 대해 3-tier 라우팅 + Evaluate에 UpValue/DownValue drop 미러, 포팅층 세팅(ChangeSecurityAttack factory `calcOrder` 인자 + 구조 dict `calcOrder` 키). 모든 실 생산자는 UpDownValue(양 factory 하드코딩)라 additive에 behaviorally inert=회귀중립(374/374), latent 구조만 구축. 테스트 SA3-SAttack3Tier(8검, fold순서·drop·invert·구조dict).
- ★**ActivatedTime 순서(continuous DP set 그룹)**: AS-IS는 DP NotIsUpDown/set 그룹만 `OrderBy(ActivatedTime)`(Permanent.cs:301/472) — 최신 활성 "DP becomes X"가 마지막 적용=승. static-DP 경로(`DpModifier.ActivatedOrder`/`DpCalculator`)는 이미 충실; **continuous 경로(`ModifierHelpers.Evaluate`, 타 카드 DP를 static base 위 fold)만 set을 Id 정렬**하는 갭이었음. `ActivatedTime`을 쓰는 4개 카드(BT25_104·BT3_014·ChangeOriginDP·TamerBecomes)는 전부 미포팅=latent. → NumericModifier에 `ActivationOrder`(long, default 0=MinValue analog) 추가, Evaluate/ReadModifiers 정렬에 `ActivationTieBreak`(DP/BaseDp tier-2=NotIsUpDown/set 그룹에만 ActivationOrder, 그 외 0=inert) 삽입 후 Id 최종 tie-break(AS-IS MinValue 동률 stable sort 미러), 포팅층 세팅(구조 dict `activatedOrder` 키 + fixedDp/fixedBaseDp 방출에 activatedOrder 반영). substrate 등록순 자동유도는 추측 회피 — 포팅층 명시 공급(기존 DpModifier 관례와 동형). default 0이라 회귀중립(375/375). 테스트 AT-ActivatedTimeSetDp(5검, later-wins·Id fallback·set-over-delta).
- ★**faceup 시큐리티 continuous-source population**(사용자 결정 "AS-IS와 동일한 구조로"=균일). AS-IS는 faceup 시큐리티(`player.SecurityCards` !IsFlipped)를 필드 permanent와 나란히 ~9 getter서 균일 스캔하는 continuous-source. **(1) substrate 선행구축**: 헤드리스에 시큐리티 카드 영속 faceup 상태 부재였음 → `Runtime.SecurityFaceState`(instance metadata `securityFaceUp` 키, AS-IS SetFace/SetReverse 미러; absent=face-down=AS-IS 기본) + sink가 AddToSecurity/Recover(batch continuation)서 stamp; read는 zone(Security)-gate라 leave-cleanup 불요. **(2) population 스캔**: `CardEffectRegistrar.BuildContinuousRequests`(register 없이 EffectTiming.None non-activated continuous 요청만 빌드; 트리거 미발화=AS-IS 필드-only 스캔 미러) + `ContinuousScopeEvaluation`이 양 플레이어 faceup 시큐리티 소스를 열거해 candidate에 적용(card-targeted OR player-scope coarse-filter, 이후 기존 ScopePredicate/condition 필터 공유). AS-IS live per-access 스캔 미러=라이프사이클 무상태; faceup 없으면 early-out=회귀0(376/376). 모든 getter가 공유 `ContinuousScopeEvaluation` 경유라 DP·immunity·battle-deletion 균일. 테스트 SEC-FaceUpSecuritySource(10검, +DP fold·opponent-scope 제외·face-down/미stamp/zone-leave gate) + 픽스처 TfxSecurityDpBuff.

**남은 구축 대상(AS-IS 메커니즘 존재 → 순차 구축, 각 focused):**
- ~~**faceup 시큐리티 population**~~ ✅구축(위) — substrate(SecurityFaceState) 선행 + query-time enumeration, 균일 continuous-source
- ~~**deletion 단일-동시 창**~~ ✅구축(아래)
- **PG ≥10 캡 = 선행 인프라 blocked**: 헤드리스 memory 모델(`InMemoryHeadlessMemoryController`)이 clamp-only placeholder(파일 주석 "TODO: real memory handoff")라 per-player 관점(MemoryForPlayer) 부재 → ≥10 캡은 memory 서브시스템 완성 후 구축. (AS-IS Player.cs:1032 존재하나 placeholder 위 구축 불가.)

**구축 완료(신규 세션 2026-07-09, 계속):**
- ★**deletion 단일-동시 창**: AS-IS는 전 battle loser를 **하나의 `DestroyPermanentsClass(LoserPermanents, hashtable)`**(CardController.Battle:4705)로 동시 삭제 → 한 loser의 dying 효과 resolution 중 co-loser들이 아직 present(필드·바인딩 live). 헤드리스 `BattleResolver.FinalizeAsync`는 per-loser 인터리브 루프(window→cleanup→move)라 loser A를 완전 삭제(trash 이동+바인딩 drop) 후 B의 knock-out 창을 열어 **B 창서 A가 이미 소멸** = 동시성 발산. 감사는 "nested-coroutine 부재로 불가피"로 봤으나, `ResolveKnockOutWindowAsync`가 subject-scoped(자기 참가자 트리거만, 타 참가자 순서 무의존)라 **2-phase 재정렬**로 nested-coroutine 없이 미러: phase1=전 loser의 knock-out 창을 전원 필드-present·바인딩-intact 상태서 해소, phase2=전원 cleanup+trash 이동. 각 창은 여전히 어떤 cleanup보다 먼저(바인딩 drop 전) 해소돼 F-6.3/P1 불변 유지. 회귀중립(376/376, RuleAudit 20게임 상호파괴 포함 0위반·승패분포 동일). 발산 대상=tie 배틀서 co-loser의 pending/present 상태 읽는 dying 효과(latent).

**검증→기각(감사 주장을 AS-IS로 검증한 결과 substrate 번역=재구축 불요):**
- ★**Piercing-as-activated — 기각(검증된 substrate 번역)**. 감사자는 "AS-IS는 OnDetermineDoSecurityCheck activated(EffectName 'Pierce'), 헤드리스는 키워드 bool → 구조 발산"으로 봤으나, AS-IS Pierce의 유일한 resolution `PierceProcess()`(CardEffectCommons/KeyWordEffects/Pierce.cs)는 **`DoSecurityCheck=true` 하나뿐**(이벤트·상호작용·응답 surface 전무). 헤드리스 `ContinuousKeywordGate.HasKeyword(Piercing)`는 (a)레지스트리 키워드 바인딩 순회-스캔=AS-IS `EffectList(OnDetermineDoSecurityCheck)` Pierce 스캔과 동치 population, (b)`KeywordConditionPasses`=AS-IS CanUseCondition/CanTrigger condition, (c)`BattleResolver`가 won-battle(`attackerSurvives && defenderDeletedNow`)에 follow-up security check — activated의 resolution을 직접 수행. 스캔·condition·가드·결과 모두 동일, stacked-skill은 inert 플러밍 → [[result-equivalence-not-completion]]의 substrate 번역 허용조건 충족. Pierce를 stacked skill로 재구축 시 `DoSecurityCheck=true`로 해소되는 머신러리만 추가(over-engineering, 회귀위험, 행동차0). [[strong-model-prebuild-latent-infra]](강모델도 AS-IS 검증) — P1-DV-4와 동류.

## 잔여 latent-infra (미포팅 카드 의존 — 해당 카드 포팅 전 엔진에 구축 필요, 현재 동작 무영향)
[[result-equivalence-not-completion]] 기준으로 로컬 포팅 전 구축 대상. 각 트리거 카드가 없어 현재 회귀엔 안 잡힘:
- ~~**DPBoost**~~ ✅구축(위)
- ~~**SAttack 3-tier**~~ ✅구축(위) — CalculateOrder 3-tier + LinkedMax 동일 라우팅, latent 구조 완비
- ~~**NotIsUpDown ActivatedTime 순서**~~ ✅구축(위) — continuous DP set 그룹 ActivationOrder; 잔여=BT25_104 turn-start/location 앵커(포팅 시 explicit)
- ~~**faceup 시큐리티 population**~~ ✅구축(위) — SecurityFaceState substrate + query-time enumeration(BuildContinuousRequests), ~9 getter 균일
- **P0-restr kind별 immunity**: printed(정적) player-scope cannot-attack/block × 면역 subject
- **CanBlock permanent-vs-player 면역 비대칭**: printed per-permanent(타 카드 타깃) cannot-block
- **CanSelectBySkill permanent-only 스코프**: player-scope CannotBeSelectedBySkill 카드 (현재 producer 전무)
- **ignore-level-only**: IgnoreLevelRequirementKey 그랜트 카드 (effect-path는 이미 처리)
- **PG minors**: CanAddMemory ≥10 캡·IsSecurityLooking add-재확인·태그없는 permanent-effect형 CannotAddMem/Sec
- ~~**Piercing-as-activated**~~ **기각(검증된 substrate 번역, 위 참조)** — PierceProcess=DoSecurityCheck=true뿐, 헤드리스 키워드 게이트가 스캔·condition·결과 동일 미러; 재구축 불요
- ~~**deletion 단일-동시 창**~~ ✅구축(위) — `FinalizeAsync` 2-phase 재정렬(전 knock-out 창 → 전 이동), co-loser 동시-present 미러

## 검증으로 기각된 감사 주장 (auditor overreach)
- **P1-DV-4 (color-ignore가 negation에 과결합)** — **기각**. 감사자는 헤드리스 IgnoreColorRequirementKey를 AS-IS mechanism 2(IIgnoreColorConditionEffect, negation 무관)로 봤으나, 실제로는 mechanism 1(`ignore==Color && CanIgnoreDigivolutionRequirement`, CardSource.cs:596, **negation-gated**)에 대응. BT8_059(진화요구 무시불가)는 color도 negate하는 게 맞고, deliberate 테스트 FAILd-06가 이를 검증. negation 게이트 제거 시 FAILd-06 실패 → 원복. (감사 주장도 AS-IS+기존 deliberate 테스트로 검증 필요 사례.)

## 상환 우선순위 제안
1. **P0-DP-1 (0-clamp)** — 안전·명확, 즉시. 중간 BaseDP + 최종 DP 이중 클램프.
2. **P0-DP-2 (순서 역전)** — ModifierOrder를 isUpDown-먼저로. ⚠️load-bearing, 테스트 churn 예상, isUpDown 포팅 태그 병행 검증.
3. **P1-DP-3/4 (DP fold 면역·base-minus)** — 연속 DP 폴딩에 CanNotBeAffected + base-DP-minus 면역.
4. **P1-DV (ignore negation/level)** — added-path negation 게이트 + ignore-level 분기 + 역할 반전 정리.
5. **P1-PG (CanUse 게이트·PlayerScope 태그)**, **P0-restr(printed player-scope immunity)**.
6. **P2 정리** — context-less 폴백 no-op, 나머지는 fidelity debt 문서화 후 opportunistic.
