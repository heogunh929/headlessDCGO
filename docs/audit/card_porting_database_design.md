# 카드 포팅 데이터베이스 — 설계안 (draft v0.1)

- 작성일: 2026-07-04. 상태: **설계 단계**(구현 전). 이 문서는 "무엇을 만들 것인가"만 정한다.
- 목적: 로컬 모델의 대량 카드 포팅을 **고효율·확정적**으로 만들기 위해, 원본 전체를 미리 구조화한
  **포팅 IR 데이터베이스**를 세운다. 포팅 착수 전에 "무엇을·어떤 순서로·어떻게 묶어" 포팅할지를
  데이터로 확정한다.
- 관련: [card_porting_standard.md](card_porting_standard.md)(구조 동일 원칙), [primitive_backlog.md](primitive_backlog.md)
  (프리미티브 선행개발), [bt1_porting_goals.md](bt1_porting_goals.md), RL L4 로그([rl_l4_match_log_design.md](rl_l4_match_log_design.md)).
- 메모리 정렬: [[primitive-predevelopment-role]](프리미티브 선행) · [[check-asis-before-implementing]](AS-IS 미러) ·
  [[fidelity-over-coverage]](충실도 우선) · [[porting-pipeline-design]](로컬 배치 포팅).

## 0. 조사 결론 — 이 숫자가 전략을 재정의한다 (전수 스캔, 2026-07-04)

`DCGO/Assets/Scripts/CardEffect/**` 3,918 카드 전수 IR 추출(`tools/CardIrExtractor`, 파싱오류 0) 결과.

**핵심 정정(IR 추출로 확인)**: 카드는 **두 포팅 형태**로 나뉜다 — 앞선 "팩토리 프리미티브만" 스캔은 절반을
놓쳤다.

| 포팅 형태 | 카드 수 | 성격 | 경량 모델 난이도 |
|---|---|---|---|
| **factory** (one-liner) | **263** | `CardEffectFactory.X(args)` 한 줄 | 최하 — 인자 채우기 |
| **inline** (코루틴) | **1,545** | `new *Class()` + `ActivateCoroutine` 본문 | 높음 — 의미 번역 |
| **mixed** (둘 다) | **2,110** | 팩토리 + 인라인 | 높음 |
| vanilla | 0 | (효과 없는 카드는 별도, 이 스캔 밖) | — |

- **inline/mixed는 구조 미러가 아니라 의미 번역이다.** 예: `ST1_08`의 AS-IS 인라인 `ActivateClass`+코루틴
  +`SelectPermanentEffect` 전체가 헤드리스에선 `CardEffectFactory.SelectAndBuffDpEffect(...)` **한 줄로 응축**
  (타이밍도 `OnEnterFieldAnyone`→`WhenDigivolving`으로 번역). 코루틴 의도를 읽고 대응 헤드리스 헬퍼를
  찾는 판단이 필요 = 경량 모델 단독으로는 위험.
- 그래서 **DB의 진짜 레버 = 레퍼런스 페어링**: "이 카드를 **같은 시그니처의 이미 포팅된 카드**에 유추하라".
  경량 모델의 작업이 자유 생성 → 유추 채우기로 내려간다(§0.2 북극성).

**준비도(포팅 안 된 카드 기준, IR DB 산출):**

| 준비도 | 카드 수 | 의미 |
|---|---|---|
| **ready** | **680** | factory형 + (inline/mixed 중 동일 시그니처 레퍼런스 보유) → 경량 모델 즉시 가능 |
| **review** | **3,116** | 레퍼런스 없는 inline/mixed → **강모델/사람이 클러스터당 레퍼런스 1장 선행 필요** |
| **blocked** | **86** | 누락 프리미티브(거의 전부 `ActivateClassesForSharedEffects` 1종) |

- 프리미티브 갭 = **6종, 실질 1종**([primitive_backlog.md]의 2026-07-01 소진 선언을 재확인 — 프리미티브
  선행개발은 사실상 끝). 즉 병목은 프리미티브가 아니라 **review 티어의 레퍼런스 시딩**이다.
- **레퍼런스 시딩 작업량 = review 3,116장이 span하는 고유 시그니처 1,783종.** 강모델이 이 1,783장을 심으면
  경량 모델이 나머지 1,333장을 유추 확산(현 엄격 시그니처 기준 평균 1.7×). 이 시딩 수가 **가려져 있던 실제
  작업 규모** — DB가 이걸 가시화한 것이 1차 성과.

**결론(수정됨)**: 포팅은 프리미티브 문제가 아니라 **① 두 형태 분리 ② 레퍼런스 페어링으로 경량 모델 작업의
직관화 ③ review 티어 레퍼런스 시딩(강모델) ④ 검증·추적** 문제다. 첫 초안의 "96% 기계적"은 factory형만 본
과대평가였고, IR 추출이 이를 정정했다(= 착수 전 DB화의 가치).

### 0.2 북극성 — "가장 가벼운 모델이 확정적으로 수행"
이 DB의 존재 이유는 **작업 단위를 최대한 직관적으로 쪼개, 아주 가벼운 로컬 모델도 결정적으로 수행**하게
만드는 것이다. 설계의 모든 선택은 이 기준으로 판단한다:
- **모델이 할 일을 최소화**: 이상적 작업 단위 = "AS-IS 원본 + **구조가 같은 이미 포팅된 레퍼런스** + 무엇이
  다른지(인자 슬롯)". 모델은 **유추 채우기**만 한다(자유 생성 아님). 판단·탐색·설계를 요구하지 않는다.
- **가능하면 모델을 아예 건너뜀**: 리터럴-인자만 다른 클러스터는 IR→코드 **결정적 생성**으로 초안(§4-3).
  모델은 조건람다 등 잔여 비정형만 처리 → 경량 모델의 실패 표면을 최소화.
- **직관성 = 시그니처 입도의 함수**: 클러스터가 조밀할수록(같은 시그니처 카드↑) 레퍼런스와의 차이가
  작아져 작업이 쉬워진다. 그래서 배치 압축(§3-2)은 효율만이 아니라 **난이도 하향** 장치다.
- **세팅과 직교**(§7): "가벼운 모델을 어떻게 구동하느냐"(세팅 트랙)와 "작업을 어떻게 직관화하느냐"(이
  DB)는 별개 층. DB는 어떤 경량 모델·구동 방식에도 같은 직관적 작업 단위를 공급한다.

### 0.1 AS-IS 카드의 실제 형태 (왜 DB화가 가능한가)
```csharp
public class ST1_03 : CEntity_Effect {              // 얇은 셸
  public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card) {
    var effects = new List<ICardEffect>();
    if (timing == EffectTiming.None) {              // ← 타이밍
      bool Condition() => CardEffectCommons.IsOwnerTurn(card);   // ← 커먼즈 술어
      effects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(    // ← 프리미티브
        changeValue: 1000, isInheritedEffect: true, card: card, condition: Condition));  // ← 인자
    }
    return effects;
  }
}
```
카드는 (타이밍 × 프리미티브 호출 × 인자 × 커먼즈 술어)의 선언적 나열이다 — **구조가 곧 데이터**.
포팅된 `.NET` 버전은 이 구조를 그대로 미러(namespace/using·`sealed`·`IReadOnlyList` 반환형만 조정). 즉
**IR로 추출 → 결정적 코드생성**이 원리적으로 성립한다.

## 1. 데이터 모델 — 카드 포팅 IR (per-card 1레코드)

저장: `docs/porting/card_ir.sqlite`(단일 파일, 조회/집계/조인) + 파생 뷰는 쿼리로. JSONL 익스포트 병행
(디프·리뷰 친화). 스키마(레코드 1건 = 카드 1장):

```
card_id          TEXT  PK      -- "ST1_03" (canonical, cards.json vocab과 동일 규칙)
set_code         TEXT          -- "ST1"
color            TEXT          -- 디렉토리에서 (Red/Blue/…)
card_type        TEXT          -- cards.json 조인 (Digimon/Tamer/Option/DigiEgg)
has_effect       INTEGER       -- 0 = 바닐라(포팅 불요, 데이터만)
timings          JSON          -- ["None","OnEnterFieldAnyone",…] EffectTiming 사용 집합
primitives       JSON          -- [{"name":"ChangeSelfDPStaticEffect","count":1,"args":{…}}]
commons          JSON          -- ["IsOwnerTurn",…] CardEffectCommons 술어
keywords         JSON          -- ["Blocker",…] 참조 키워드
signature_hash   TEXT          -- H(timings + 정렬된 프리미티브 멀티셋) = 배치 클러스터 키
arg_profile      JSON          -- 인자의 '種'(리터럴int / 조건람다 / 키워드enum / 존…) — 템플릿 변수 슬롯
readiness        TEXT          -- ready | blocked
blocking_prims   JSON          -- readiness=blocked면 누락 프리미티브
port_status      TEXT          -- pending | in_progress | ported | verified | debt
verify_signature JSON          -- 기대 L4 이벤트 패턴(§5) — 예: {"type":"StateChanged","cause":"DPChange"}
source_path      TEXT          -- DCGO/…/ST1_03.cs
target_path      TEXT          -- src/…/ST1/Red/ST1_03.cs
notes            TEXT
```

- `signature_hash`가 **배치의 원자**다: 같은 해시 = 템플릿 1개로 포팅되는 동치류.
- `arg_profile`은 "무엇이 카드마다 다른가"(=템플릿 변수)를 명시 — 로컬 모델은 템플릿에 인자만 채운다.
- `readiness`/`blocking_prims`가 **게이트**: blocked 카드는 프리미티브 착지 전까지 포팅 큐에서 격리.

## 2. 추출기 (AS-IS → IR)

- 위치(제안): `tools/CardIrExtractor/`(C# 콘솔, 엔진 솔루션 밖 도구). 입력 `DCGO/…/CardEffect/**`,
  출력 `docs/porting/card_ir.sqlite` + `card_ir.jsonl`.
- **파싱 전략 — Roslyn 우선**: AS-IS가 컴파일 가능한 C#이므로 Roslyn 구문트리로 팩토리 호출·인자·타이밍
  분기를 **정확히** 뽑는다(정규식은 스캔용 근사였음 — DB는 정확해야 배치가 안전). 실패/비정형 카드는
  `notes`에 원인 기록하고 수동 검토 큐로.
- **커버리지 자기검증**: 추출한 프리미티브 집합이 §0 스캔의 95종과 일치하는지 대조(누락 파싱 = 즉시 실패).
- **결정론**: 같은 AS-IS = 같은 IR(정렬·canonical). 재실행이 디프를 안 만든다.
- cards.json 조인으로 `card_type`/색 보강(존재하지 않는 카드 = 명시 실패 — 충실도 원칙).

## 3. 파생 뷰 (DB에서 쿼리로 산출 — 저장 안 함)

1. **프리미티브 언락 랭킹**: `primitive → 그것이 (공)여는 카드 수`. 갭 프리미티브 개발 우선순위
   (지금은 `ActivateClassesForSharedEffects` 1종이 84카드 = 유일 우선). 신세트 편입 때 재계산.
2. **배치 클러스터**: `signature_hash → 카드목록`. 크기순 정렬 = 템플릿 1개의 ROI 순서. (상위 10종 392장.)
3. **준비도 코호트**: `set_code × readiness` — 발매순 착수의 "이번 세트 몇 % 즉시 가능"(§0: 초기 세트 100%).
4. **검증 대시보드**: `port_status=ported`인데 L4 로그에서 `verify_signature` 미발화 = **의심 카드**
   (테스트 green인데 실전 inert인 부류 — card_porting_standard §3 함정을 자동 탐지).
5. **부채 원장**: `port_status=debt` + 사유 — [fidelity_debt.md]와 연동.

## 4. 포팅 워크플로우 (DB가 로컬 모델을 구동하는 방식)

DB-우선이 바꾸는 것: 로컬 모델이 **카드를 한 장씩 눈먼 채로** 보는 게 아니라, **배치 클러스터 + 템플릿 +
채울 인자 슬롯**을 받는다.

1. **세트 선택**(발매순, BT1부터) → 준비도 코호트에서 `ready` 카드만 큐잉, `blocked`는 격리.
2. **클러스터 단위 배급**: 같은 `signature_hash` 묶음을 한 작업으로. 강모델(또는 사람)이 **클러스터당
   레퍼런스 포트 1장**을 확정(구조 동일 검수) → 로컬 모델은 나머지에 `arg_profile` 슬롯만 채운다.
   (예: `ChangeSelfDPStaticEffect` 클러스터 65장 = 레퍼런스 1 + 인자만 다른 64.)
3. **결정적 코드생성 초안**: 단순 클러스터(리터럴 인자만)는 IR→코드 **자동 생성**으로 초안 후 검수 —
   로컬 모델은 조건람다/비정형만 처리(고빈도 tail 압축).
4. **검증 게이트**: 카드별 단언 테스트(기존) + **L4 로그 발화 확인**(§5). 둘 다 통과해야 `verified`.
5. **진척 반영**: `port_status` 갱신 → 준비도 코호트·검증 대시보드 실시간 갱신.

## 5. 검증 연계 (RL 인프라 재사용 — NFR-5 "포팅 검증 도구도 됨")

- 각 IR 레코드의 `verify_signature` = "이 카드가 실전에서 fire하면 L4 로그에 나와야 하는 이벤트 패턴".
- 대량 셀프플레이(리그/랜덤) + **L4 ANALYSIS 로그** 집계로, 포팅했다고 표시된 카드가 실제 매치에서
  효과를 발화하는지 측정. 발화 0 = card_porting_standard §3의 "self-static 단절"류 **자동 후보 탐지**.
- 카드 단언 테스트가 못 잡는 "green인데 inert"를 DB가 잡는다 = 포팅 품질의 2차 방어선.

## 6. 대시보드 통합 (기존 GUI 확장)

`rl/dashboard/`에 **포팅 탭** 추가(읽기 전용): 세트별 준비도 막대, 배치 클러스터 트리맵(크기=카드수),
프리미티브 언락 랭킹, 검증 대시보드(ported vs fired). 서버는 `card_ir.sqlite`를 읽기만 — 조작면 없음.

## 7. 층위 분리 — 이 DB는 "로컬 모델 세팅"과 직교한다 (중요)

직전 v2→v3 파이프라인의 리버트는 **로컬 모델 세팅(인프라/오케스트레이션) 층위**의 이슈였다(콘텐츠 설계가
아니라 모델을 어떻게 세팅·구동하느냐의 문제). 이 포팅 DB는 **콘텐츠 층위**(무엇을·어떤 순서로·어떻게 묶어
포팅할지)이므로 그 실패와 **겹치지 않는다**. 두 층을 명시적으로 분리한다:

| 층 | 관심사 | v2→v3에서 문제된 곳 | 이 DB |
|---|---|---|---|
| **세팅/오케스트레이션** | 로컬 모델 런타임·배치 분배·재시도·결과 수집·검증 루프 배선 | ← 여기 | 무관 |
| **콘텐츠(포팅 IR)** | 준비도·배치 클러스터·템플릿·인자 슬롯·검증 시그니처 | — | ← 여기 |

- DB는 **세팅이 무엇이 되든 그 위에 얹히는 입력**이다: 어떤 로컬 모델·어떤 오케스트레이터를 쓰든, "이
  클러스터를 이 레퍼런스로 이 슬롯만 채워라"는 **콘텐츠 지시**는 동일하게 재사용된다. 따라서 DB는 세팅
  방향 확정과 **병행/선행**해 만들어 둘 가치가 있다(자산이 세팅에 종속되지 않음).
- 세팅 재설계는 **별도 트랙**(이 문서 범위 밖). P-DB4(파일럿)에서 둘이 만나며, 그때 세팅 트랙이 제공하는
  구동 방식에 DB 출력(배치 매니페스트)을 물린다. 인터페이스만 합의하면 두 트랙은 독립 진행 가능.

## 8. 단계

- **P-DB1** ✅ (2026-07-04): 추출기(`tools/CardIrExtractor`, Roslyn·JSONL) + DB 빌더
  (`tools/porting/build_card_db.py`, stdlib SQLite) + 태스크 카드 생성기(`tools/porting/porting_task.py`).
  3,918 카드 전량 IR(파싱오류 0), 프리미티브 커버리지 자기검증(95종 일치), 두 형태·준비도·레퍼런스 페어링
  산출. 태스크 카드 = 대상 AS-IS + 동일-시그니처 레퍼런스(AS-IS+포팅본) + 유추 지시.
- **P-DB2** ✅ (2026-07-04): 시그니처 입도 튜닝 실측(`tools/porting/tune_signature.py`) + 추출기 강화
  (코루틴 액션: `new *Effect`·`GetComponent<*Effect>`·효과 설명문). **핵심 발견 = 확산/순도/시딩수의
  3-way 프론티어**(§9-A) — 시그니처 재조합만으로 확산율을 크게 못 올린다. 프로덕션 시그니처를 **S5(키워드+
  프리미티브+설명문 액션태그, 순도 89%)**로 채택: strict(순도 36%, 커버 506)보다 커버는 낮지만(218) **틀린
  레퍼런스가 경량 모델을 오도하는 것보다 신뢰도 우선**. 대시보드 포팅 탭은 후속.
- **P-DB3** review 티어 **레퍼런스 시딩** 파이프라인: ROI 상위 클러스터부터 강모델이 레퍼런스 1장 확정 →
  경량 모델 확산. 병행: `ActivateClassesForSharedEffects`(유일 실질 갭, 84카드) 강모델 선행.
- **P-DB4** BT1 발매순 파일럿 — [card_porting_pilot_design.md](card_porting_pilot_design.md) 설계 완료
  (Sonnet 4.6로 티어별 유추 성공률 실측 → §9-A 시딩 수 확정). 하네스 `tools/porting/pilot/port_with_sonnet.py`
  (티어 선정·태스크 카드·Sonnet 호출·G1 컴파일). 실행은 API 키·승인 후. 경량 모델 파일럿은 세팅 트랙 확정 후 별도.

## 9. 미해결 / 확인 필요

### 9-A. 확산/순도/시딩수 3-way 프론티어 (P-DB2 실측 결론)
시그니처 6종을 포팅된 36장(정답셋)으로 측정한 결과, **하나의 전역 시그니처로 확산율과 순도를 동시에 못
올린다** — 순도를 높이려면 클러스터를 더 쪼개야 하고(시딩수↑·확산율↓), 확산율을 높이면 서로 다른 포트
타깃이 섞인다(순도↓).

| 시그니처 | pending 클러스터 | 확산율 | 순도(포팅셋) | 신뢰 레퍼런스 커버 |
|---|---|---|---|---|
| strict(타이밍+프리미티브멀티셋) | 1,979 | 2.0× | 36% | 506 |
| shape-only(프리미티브+키워드) | 1,044 | **3.7×** | 33% | 950 |
| **S5(키워드+프리미티브+설명액션)** | 2,769 | 1.4× | **89%** | 218 |

**채택 = S5**(순도 우선). 근거: 경량 모델에게 **틀린 레퍼런스는 무(無)레퍼런스보다 나쁘다**(오도) →
신뢰도가 커버리지보다 중요. 확산율 1.4×는 낮지만, 218장은 "이대로 유추하면 대체로 맞는" 신뢰 원본이다.
- **함의(중요)**: 포팅 작업량은 **효과 다양성 자체가 지배**한다. review 3,405장 → 고유 시그니처 **2,568종**
  (시딩 1장당 확산 1.3장). 즉 강모델이 심어야 할 레퍼런스가 **~2,568장**으로 irreducibly 크다. 클러스터링
  개선이 아니라 **시딩 자체를 효율화**(P-DB3)하는 것이 다음 레버 — 다행히 시드도 몇 개 확정되면 같은 패밀리
  내에선 서로 유추 대상이 되므로, 강모델의 시딩도 점증적으로 쉬워진다.

### 9-B. 기타
- **로컬 모델 세팅 트랙**(별도, §7): 세팅 이슈 성격에 따라 P-DB4 파일럿 구동 인터페이스 결정. 독립 진행.
- **2단 레퍼런스(후속 아이디어)**: 순도-우선 S5로 신뢰 레퍼런스가 없을 때, shape-only 코스 스캐폴드를
  fallback로 제시(경량 모델이 설명문 읽고 액션 조정 — review 플래그). 프론티어를 계층으로 우회.
- IR 코드생성 자동화 범위: 리터럴-인자 클러스터는 자동 초안 가능해 보이나, 조건람다 자동 미러 한계선은
  파일럿 실측.
- `signature_hash` 입도: 엄격(타이밍+프리미티브 멀티셋, 2.0×) vs 느슨(프리미티브 패밀리, 압축↑ 정확도↓) —
  배치 ROI와 안전의 균형은 P-DB4 파일럿 수치로.
- 바닐라 카드(~1,545) 처리: 데이터(cards.json)만으로 완결인지, 최소 스텁이 필요한지 확인.
