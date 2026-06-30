# Card Porting Standard (구조 동일 원칙)

> 목표: 로컬 LLM이 카드를 대량 포팅할 수 있도록 **재현 가능한 작업 기준**을 세운다.
> 핵심 원칙(사용자 지시): **"원본구조랑 같게하는게 중요하다."** — 행동(behavior)만이 아니라
> 원본 DCGO `Script/`의 **구조**(파일 위치 · 팩토리/메서드 이름 · 논리 분해)까지 1:1로 미러한다.

## 0. 두 레이어 모델

| 레이어 | 원본 (DCGO/) | 헤드리스 | 미러 수준 |
|---|---|---|---|
| **카드** | `Assets/Scripts/CardEffect/<Set>/<Color>/<Id>.cs` (얇은 팩토리-호출 셸) | 동일 경로 | **구조+행동 1:1** |
| **엔진/키워드** | `Assets/Scripts/Script/...` (`CardEffectFactory`, `KeyWordEffects/<Keyword>.cs` 등) | 동일 파일 레이아웃 + 헤드리스 디스패치 스캐폴딩(`KeywordBaseBatch1/2.cs`) | **파일·이름 1:1 / 런타임은 번역** |

- 원본은 Unity MonoBehaviour/coroutine/GManager 싱글톤. 헤드리스는 async/Task·ChoiceController·EffectRegistry·게이트/파이프라인으로 **번역**한다.
- 런타임 메커니즘은 같게 만들 수 없지만 **파일 위치·팩토리 이름·논리 분해는 같게 만든다.** 이게 "구조 동일"의 실천적 정의.

## 1. 표준 원칙 (PASS 기준)

1. **파일 위치 동일**: 원본 `KeyWordEffects/Vortex.cs` → 헤드리스 `KeyWordEffects/Vortex.cs`.
2. **팩토리/메서드 이름·시그니처 동일**: 원본 `CardEffectFactory.VortexSelfEffect(bool, CardSource, Func<bool>, ICardEffect=null)`이
   있으면 헤드리스에도 **같은 이름·인자 순서**로 존재해야 한다. (원본에만 있고 헤드리스에 없으면 = 빠진 프리미티브 = 포팅 실패의 원인)
3. **행동 동일(AS-IS 대조 필수)**: 카드/키워드가 *무엇을* 하는지 원본(`DataBase.cs` 룰텍스트 + `<Keyword>Process`)과 대조해 같게.
   - 추측 금지. 빈도로 합리화 금지(드물어도 누락은 FAIL). 부차 게이팅 완화도 실패.
4. **라이브 검증 필수**: 단위 단언 + 실제 흐름(게이트/소비자/EndOfTurn 윈도우 등)에서 효과가 **inert가 아님**을 측정.

## 2. 작업 워크플로우 (실증됨 — G9-002)

원본에 있는데 헤드리스에 없는 프리미티브를 채우는 표준 절차. 예: self-static 키워드 팩토리.

1. **원본 시그니처 확인**
   `grep -rn -A18 "public static .* VortexSelfEffect(" DCGO/.../CardEffectFactory/`
   → `VortexSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition, ICardEffect rootCardEffect=null)`,
   가드 `IsExistOnBattleAreaDigimon(card) && condition()`, 본체 `VortexEffect`에 위임.
2. **헤드리스 격차 확인**
   헤드리스 `CardEffectFactory`에 같은 이름이 있나? 없으면 빠진 프리미티브.
   (이번 케이스: Blocker/Jamming/Pierce SelfStatic만 있고 Reboot/Alliance/Overclock/Vortex SelfEffect 없었음.)
3. **이름·시그니처 그대로 미러**
   - Batch1 키워드(Blocker/Jamming/Pierce/Reboot) → 기존 `SelfKeywordEffect`.
   - Batch2 키워드(Alliance/Vortex/Overclock/…) → `SelfKeywordBatch2Effect` (이번에 추가, `SelfKeywordEffect`의 구조적 쌍둥이).
   - 원본의 `IsExistOnBattleAreaDigimon` 가드는 헤드리스에선 **바인딩 생명주기**(enter-play 등록 / leave 해제) + 읽기시점
     `ContinuousKeywordGate` 질의로 표현된다(주석에 매핑 명시).
4. **라이브 검증** — 실 카드 경로로:
   `CardEffectFactory.X(card,...).ToBinding()` → `EffectRegistry.Register` → 소비자(게이트/EndOfTurnEffectAttack)가 인식.
   - 등록 전 게이트가 false(거짓양성 없음), 등록 후 true, **자기 카드에만** 적용(bystander 미적용).
5. **전체 회귀** `bash scripts/run-tests.sh` → `FAIL=0`.

## 3. 핵심 함정 (이전 세션에서 실제로 밟음)

- **자기-정적 키워드 단절**: self-static은 EffectRegistry **바인딩**으로 등록되지만, 소비자는 per-instance
  **메타데이터 플래그**(`hasBlocker` 등)를 읽음 — 이 플래그는 *다른 카드가 grant*할 때만 세팅됨 → self-static은 inert.
  해결: 소비자가 `ContinuousKeywordGate.HasKeyword`(레지스트리 풀 질의)로도 확인. (GR-005/006)
- **카드 .cs grep으로 키워드 카드 탐색 금지**: 별칭 디스패치(effectClass) 때문에 누락됨 → `cards.json`(effect text + effectClass)로 탐색.
- **Vortex 대상**: 상대 **디지몬**(unsuspended 포함), **플레이어 아님**. 플레이어 공격은 별도 효과. (헤드리스 주석이 틀렸던 케이스)
- **타이밍 allow-list 광범위 확장 금지**: "모든 Anyone 타이밍" 확장은 BlockerSuspend 회귀를 냄 → 명시적 allow-list 유지.

## 4. 현재 상태 / 다음 프리미티브

- ✅ self-static 키워드 팩토리 4종(Reboot/Alliance/Overclock/Vortex) 추가 + 라이브 검증(G9-002).
- ✅ play-cost 팩토리 2종(`ChangePlayCostStaticEffect` / `MandatorySelfPlayCostReduction`, int+Func<int>) 추가 +
  라이브 검증(G9-003, 235/235 green). **핵심 교훈**: 코스트 엔진(`PlayCostHelpers` + `ContinuousModifierGate.ResolvePlayCost`)은
  이미 완성·배선돼 있었고, 빠진 건 **카드-facing 등록 팩토리뿐**이었다 — 키워드 단절과 같은 "엔진 있음 / 등록 경로 없음" 패턴.
  → 새 프리미티브 작업 전 **반드시 엔진에 이미 메커니즘이 있는지 먼저 확인**(probe), 없을 때만 엔진 작업.
## 5. EX8_074 단계 로드맵 (hard card 포팅 forcing function)

원본 EX8_074는 **6개 효과**: ①[BeforePayCost] 디지몬 2체 서스펜드→코스트-4(인터랙티브) ②[None] 같은 -4
가용성-체크용 패시브 ③[OnAllyAttack] Alliance(✅) ④[OnEndTurn] Vortex(✅) ⑤[OnEnterField/When Digivolving]
1체 서스펜드→상대 ≤8000 삭제(+서스펜드당 3000) ⑥[OnEnterFieldAnyone/All Turns,OPT] 자기 [When Digivolving] 재발동.

핵심 장애물: 헤드리스 PlayCard는 **코스트 선계산 원자 액션**(PlayCardAction에서 action-generation 시 cost lock-in),
원본은 **지불 전 인터랙티브 윈도우**(BeforePayCost). "asis처럼" 미러하려면 윈도우를 신설해야 함.

- ✅ **Stage 1 (기반 미러, G9-004)**: `EffectTiming.BeforePayCost` enum + CardEffectCommons 읽기 술어
  (`IsExistOnHand` / `IsSuspended` / `MatchConditionPermanentCount` / `HasMatchConditionPermanent`,
  헤드리스 엔티티-id 관용). 236/236 green. 여러 카드 재사용 가능.
- ⏭ **Stage 2 (Permanent enrich 결정)**: 원본 카드 본체가 `Func<Permanent,bool>`(`p.IsSuspended`/`CanSuspend`/
  `TopCard`)를 쓰므로 본체 1:1을 원하면 `Permanent` shim을 Context-백킹으로 살찌워야 함(모든 생성처 파급).
  대안: 헤드리스 엔티티-id 술어 관용 유지(Stage 1 방식) — 본체가 살짝 다르나 기존 관용과 일관. **결정 필요.**
- ⏭ **Stage 3 (BeforePayCost 윈도우)**: PlayCardAction에 지불 전 인터랙티브 코스트 감소 윈도우 통합
  (기존 `OptionalPromptQueue` + `SelectPermanentEffect.Custom` + suspend mutation 재사용; 코스트 lock-in을
  윈도우 이후로 이동). 가장 큰 아키텍처 변경.
- ⏭ **Stage 4**: ⑤ 동적 임계 select-and-delete(서스펜드 수에 따라 8000+3000n). ⑥ [All Turns] once-per-turn
  자기 [When Digivolving] 재발동. + `OnEndTurn` 타이밍 enum/등록 경로(④ Vortex가 카드로 등록되려면).
  각기 §2 워크플로우(원본 이름 확인 → 엔진 격차 probe → 미러 → 라이브 검증)로.
