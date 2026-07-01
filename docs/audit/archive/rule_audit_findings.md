# 룰 정확성 감사 결과 (G13 freeze 전제)

> 도구: `tools/RuleAudit/`(진단용, 게이트 미포함). 실제 ST1/ST2/ST3 카드로 랜덤-합법 self-play 6게임(635스텝)을 돌리며, **라이브 상태를 직접 점검**해 DCGO 룰 불변식 위반을 잡는다(액션-id 파싱 비의존). 엔진이 *룰상 금지된 것을 허용*하는 지점을 찾는 게 목적.
>
> 기준: 커밋 `40388596` + 미커밋 G13-003. 카드 효과(ST1~3)는 1:1 충실하나, **게임 룰 엔진 층**에 갭이 있음(효과 포팅과 별개 트랙).

## 요약: 깨진 건 2개 서브시스템뿐, 나머지 8개 차원은 정상

| 차원 | 결과 | 비고 |
|---|---|---|
| 🔴 육성 이동 대상 lv | **30 위반** | lv2 디지에그를 배틀로 이동 |
| 🔴 육성 부화+이동 동시 | **25 위반** | 같은 턴 hatch+move |
| 🔴 배틀존 lv2/DigiEgg 상주 | **532 위반** | 위 이동의 하류 증상(잔류) |
| 🔴 메모리 음수 턴종료 | **34 위반** | mem ≤ -1인데 계속 플레이 |
| ✅ 공격 소환멀미(entered+Rush) | 0 | 게이트 작동 |
| ✅ 서스펜드 디지몬 공격금지 | 0 | 작동 |
| ✅ 공격 후 공격자 서스펜드 | 0 | 작동 |
| ✅ 메모리 범위 [-10,10] | 0 | 안 벗어남 |
| ✅ 턴 플레이어만 코스트-플레이/공격 | 0 | 소유권 정상 |
| ✅ 옵션 해소 후 배틀 잔류 안 함 | 0 | 트래시로 감 |
| ✅ 턴 시작 시 메모리 비음수(핸드오버 부호) | 0 | 정상 |
| ✅ 손/보안존 디지에그 누출 | 0 | 없음 |
| ➖ 진화 색@레벨 조건+비용 | (코드 게이팅) | 엔진이 강제(별도 불변식 미측정) |
| ➖ 직접공격 보안 소진 | 47/55 (정보) | 8건은 보안 0=직접패배/블록 추정, 미확정 |

## 🔴 확정 버그 1 — 육성(Breeding) 서브시스템

**증상:** `HatchDigitama`(빈 육성→lv2 알) 직후 같은 턴 `MoveBreedingToBattle`로 **lv2 알이 배틀존으로**. 한 번 들어가면 매 스텝 잔류(532).

**근본 원인:** `HeadlessLegalActionDispatcher.BuildBreedingActions`
```csharp
if (digitama > 0 && breeding == 0)  actions.Add(HatchDigitama);
if (breeding > 0)                   actions.Add(MoveBreedingToBattle); // 게이트가 "육성칸 비지 않음"뿐
```
빠진 룰:
1. 이동 대상은 **lv3+ 디지몬**이어야 함(lv2 디지에그/유아기는 이동 불가).
2. 부화/이동 **턴 제약**(같은 턴 동시 금지 — 룰 확인 필요).
3. 육성칸 내 **lv2→lv3 진화 흐름**이 정상 경로로 강제되는지(현재는 알을 그대로 내보냄).

**올바른 흐름:** 부화(lv2) → (육성칸에서) lv3로 진화 → **lv3만** 배틀로 이동.

## 🔴 확정 버그 2 — 메모리 음수가 턴을 끝내지 않음

**증상:** mem `0 → -2 → -5 → -8 → -10`까지 한 턴에 연속 플레이(34건).

**근본 원인:** 턴종료 평가 `HeadlessMainPhaseFlow.EvaluateAfterMemoryMutation`(mem ≤ -1 → MemoryPass)은 **독립 메모리 액션**(`SetMemory`/`AddMemory`/`PayMemory`)에서만 호출됨. 실제 플레이 경로 `PlayCardAction`·`DigivolveAction`·`OptionActivateAction`·`SpecialPlayAction`은 `MemoryController.Pay()`를 **직접 호출하고 평가를 안 탐** → 음수가 돼도 턴이 안 넘어감.

## 미확정 관찰 (후속 점검 후보)

- **직접공격 보안 소진 47/55**: 8건이 보안 소진 없이 끝남. 보안 0(=직접 패배)·블록·공격자 삭제면 정상이지만 **코드로 미확정** → 육성 수정 시 함께 타깃 점검 권장.
- **진화 조건**: 엔진이 `색@레벨`을 게이팅(코드 확인). 독립 불변식으론 미측정.

## 결론

룰 엔진이 전반적으로 죽은 게 아니다 — **공격/메모리범위/턴소유권/옵션/핸드오버 등 8개 차원은 정상**. 갭은 **(1) 육성 전 과정, (2) 메모리 음수 턴종료** 두 곳에 한정. 따라서 수정 범위는 **경계가 분명**하다. freeze 선언은 이 둘을 고치고 룰 불변식을 게이트에 영구 편입한 뒤로 미룬다.

---

## ✅ 해소 (GR-001/002/003) — 감사 위반 0, 게이트 229/229

| 항목 | 처리 | 결과 |
|---|---|---|
| 메모리 음수 턴종료 | GR-001: `HeadlessGameLoop`이 액션+효과 정착 후 `EvaluateAfterMemoryMutation` 호출(루프 단일 지점, 멱등) | `MEM_TURN_NOT_ENDED`=0 + `tests/GR-001.MemoryTurnEnd` |
| 육성 이동 게이트 | GR-002: `BuildBreedingActions`이 top 카드가 Digimon일 때만 Move 제시(AS-IS `Permanent.CanMove`) | `BREED_MOVE_*`·`EGG_IN_BATTLE`=0 + `tests/GR-002.BreedingMove` + `g_breeding_asis_notes.md` |
| 룰 불변식 게이트 | GR-003: `tests/GR-003.RuleInvariants`(랜덤 self-play, 11개 불변식=0 단언) | 영구 회귀 게이트 편입 |
| 감사 오탐 교정 | `EGG_OR_L2_IN_BATTLE`(lv<3) → `EGG_IN_BATTLE`(DigiEgg 타입만; 테이머/옵션 lv0 오탐 제거); `BREED_HATCH_AND_MOVE`(원본과 불일치) 제거 | — |

**보안 47/55 미확정 → 확정(정상).** 정밀 분류: 미소진 케이스 = 보안0(직접 패배) + **공격 해결 대기**(블로커 지정 윈도우). UNEXPLAINED 4건 모두 방어자 디지몬 4~6기 보유 + `AttackResolved`/`SecurityCheck` 이벤트 없음(`AttackDeclared`만) → 선언 직후 측정한 **타이밍 artifact**(보안은 이후 스텝에 소진). **silent skip 아님 — 보안 메커닉 정상.**

**~~남은 후속~~ ✅ 해소(GR-004):** 육성 내 진화(lv2→lv3) — `DigivolveAction`이 육성칸 타겟을 제시하고 진화를 제자리로 처리. 부화→육성 진화→이동 풀 램프 동작(`tests/GR-004.BreedingDigivolve`, self-play 라이브 확인). `g_breeding_asis_notes.md` 참조.

## ✅ 해소(GR-005) — 자기-정적 키워드(Blocker/Jamming/Piercing) 라이브 단절

**발견(20게임 감사):** 블로커 카드가 수비측에 79번 있었는데 `hasBlocker` 플래그는 0번 세팅 → 블록 윈도우 0/223공격. 라이브에서 블록이 전혀 작동 안 함.

**근본 원인:** 자기-정적 키워드는 EffectRegistry **바인딩**으로 등록되는데, 소비측(BlockTiming/BattleResolver/SecurityResolver)은 인스턴스 **메타 플래그**(`hasBlocker`/`hasPiercing`/`preventBattleDeletion`)를 읽음 — 그 플래그는 *다른 카드가 부여*할 때만 세팅되고 자기-정적 바인딩은 안 채움. 두 표현 미연결. (대조: DP·Security Attack 같은 연속 **수정자**는 `ContinuousDpGate`/`ContinuousModifierGate`로 레지스트리를 read-time 조회 → 정상. ST1_11 SA+는 키워드가 아니라 이 수정자 계열이라 정상 작동.)

**수정:** `ContinuousKeywordGate.HasKeyword`(레지스트리 키워드 바인딩을 read-time 조회 — 수정자 게이트와 동일 pull 패턴) 신설. 소비측이 메타 플래그 **OR** 게이트를 보도록:
- Blocker: `BlockTiming.TryCreateCandidate` — **라이브 검증**(감사: 0→62 블록 윈도우/20게임, 실제 28회 블록).
- Piercing: `BattleResolver`(piercing security check 트리거).
- Jamming: `SecurityResolver`(시큐리티 배틀 생존).

**검증:** `tests/GR-005.KeywordContinuity`(게이트가 3키워드를 레지스트리에서 도출 + 메타 플래그 없는 등록 `<Blocker>`가 라이브 블록 후보). 전체 231/231. **단 Piercing/Jamming은 ST1~3 스타터덱에 카드가 없어 self-play 실측은 못 함**(게이트 프리미티브 + Blocker 대칭 코드로 검증). 전용 카드 deck로 실측은 후속.

**분류 정리 (어떤 게 단절이고 어떤 게 아닌가):**
- 키워드 statics `Blocker/Jamming/Piercing` → 단절이었음, GR-005로 해소.
- `Reboot`(KeywordBaseBatch1Kind 4번째) → 소비측이 `hasReboot` 메타 플래그를 읽어(`HeadlessEarlyPhaseFlow`) **동일 단절 패턴**이나, **현재 자기-정적으로 포팅하는 factory/카드가 없어 잠재(latent)**. Reboot 자기-정적 카드 포팅 시 `ContinuousKeywordGate`에 Reboot 추가 + EarlyPhaseFlow가 게이트도 보도록 같은 수정 필요.
- `Recovery(<Recovery +N>)` → 키워드 아님. `RecoveryTriggerEffect`(트리거 효과)로 타이밍에 resolve돼 Recover mutation emit → 파이프라인 정상. **self-play 라이브 발동 확인**(ST3_09 `recover:OnEnterFieldAnyone`). 단절 아님.
- 연속 수정자 `DP / Security Attack(ST1_11)` → `ContinuousDpGate`/`ContinuousModifierGate`로 read-time 조회 → 정상(애초에 단절 아님).
