# P8 컷오버 — 구모델 카드 재포팅 레시피 (2026-07-14)

목적: 남은 **구모델 `ActivatedEffect` 카드**를 신모델 `ActivateClass`로 재포팅 → 레지스트리 삭제(스캔전용=AS-IS 1:1)를 위한 선행. union은 과도기 비계였고 중단됨(memory registry-deletion-endpoint).

## 대상 식별
`grep -rlE 'new ActivatedEffect\(' src/.../CardEffect --include=*.cs` 중 `new ActivateClass(`/`SetUpActivateClass` 없는 **순수 구모델**. 실카드는 AS-IS 1:1, Tfx 픽스처는 별도.

## Gold source
각 카드의 **AS-IS 원본** `DCGO/Assets/Scripts/CardEffect/<set>/<color>/<CARD>.cs`. 그 파일의 `ActivateClass` 구조를 **그대로 미러**(추측 금지). grep 시 `--binary-files=text`(비-UTF8 스킵 방지).

## Transform (구 → 신)
구모델:
```csharp
cardEffects.Add(new ActivatedEffect(
    card: card, timing: T,
    canUse: ctx => CardEffectCommons.CanTriggerX(ctx, card),   // 트리거 게이트
    canActivate: CanActivate,                                  // Func<bool> 전제
    body: new DrawBody(1),                                     // IEffectBody 프리미티브
    maxCountPerTurn: null, isOptional: false, description: "..."));
```
신모델(AS-IS ActivateClass, 참조 ST1_08·BT1_017·BT1_104):
```csharp
ActivateClass activateClass = new ActivateClass();
activateClass.SetUpICardEffect(shortDesc, CanUseCondition, card);
activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, isOptional, EffectDiscription());
cardEffects.Add(activateClass);
// 로컬 함수:
string EffectDiscription() => "...";                          // 전체 효과문
bool CanUseCondition(Hashtable h) => CardEffectCommons.CanTriggerX(h, card);
bool CanActivateCondition(Hashtable h) { /* AS-IS 전제 1:1 */ }
async Task ActivateCoroutine(Hashtable _h) { /* AS-IS 본체 코루틴 1:1 — body 프리미티브를 AS-IS 코루틴으로 */ }
```
매핑:
- `canUse` → `CanUseCondition(Hashtable)`
- `canActivate`(Func<bool>) → `CanActivateCondition(Hashtable)`
- `maxCountPerTurn: null` → `-1`; `isOptional` → 그대로
- **`body: new XBody(...)` → AS-IS 원본의 `ActivateCoroutine` 본체를 1:1 미러** (이게 핵심 재도출)

## Substrate 번역만 허용
`IEnumerator`→`async Task`; `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`; `card.PermanentOfThisCard()`(Permanent 취급)→`ICardEffect.ResolvePermanentOfThisCard(card)`; select flow=`GManager.instance.GetComponent<SelectPermanentEffect>()`(bridge W4); UnityEngine/Photon/Debug.Log/PlaySE/WaitForSeconds 제거; `Mathf`→`System.Math`.

## STOP 규칙
프리미티브/인프라 갭(신모델에 없는 body·팩토리·select 컴포넌트) 만나면 **STOP + design item**(literal TODO 금지, "design item RD-P8-NN"), 발명 금지. Opus가 프리미티브 개발 판정.

## 검증
카드별 컴파일(엔진 error CS 0) + 그 카드를 read하는 테스트 통과(예: BT1_046→G-계열, ST/witness→해당 witness). **suite 전체 실행 금지, 개별 테스트만.** 커밋은 코디네이터.

## dispatch remap 주의
[When Digivolving] 등은 AS-IS가 `OnEnterFieldAnyone`에 등록하고 hashtable 게이트로 구분하지만, 미러 엔진은 전용 키(`WhenDigivolving`)로 dispatch → 등록 timing은 미러 키 사용(ST1_08 주석 참조). 게이트 자체는 AS-IS verbatim.

## design items (P8 STOP 카드)
- **RD-P8-01** (BT1_039, BT1/Blue): AS-IS ActivateCoroutine이 `GManager.instance.GetComponent<SelectHandEffect>()`(hand select+discard, Mode.Discard) 사용 — 미러 `Script/SelectHandEffect.cs`는 0-type 스켈레톤(타입 선언조차 없음, 7줄 TODO 헤더뿐). 기존 corpus의 동일 갭 선례=BT9_109 `[When Attacking]`분기(design item RD-P6C3-D2, 동일 근본원인). `SelectCardEffect(Root.Hand, Mode.Discard)`로 치환하면 기능적으로 등가일 수 있으나, AS-IS가 명시적으로 별개 클래스(`SelectHandEffect`)를 구성하므로 이 대체는 발명(선례 BT9_109가 이미 이 대체를 거부하고 STOP으로 남김). BT1_039는 구모델 유지(`SelectTrashHandThenSelfMutationBody`, 기존 상태 변경 없음).
- **RD-P8-02** (BT1_109, BT1/Green): AS-IS ActivateCoroutine이 배경-프로세스 정리 패턴(ChangeCostClass 등록 + `SetIsBackgroundProcess(true)`인 두 번째 ActivateClass를 `CanTriggerWhenPermanentWouldDigivolve` 게이트로 첫 매칭 digivolve 시 `UntilEachTurnEndEffects.Remove`)을 사용. 미러 `CardEffectCommons.AddEffectToPlayer`는 `(effectDuration, card, cardEffect, timing)` 4-param 오버로드만 존재(`getCardEffect: Func<EffectTiming,ICardEffect>` 지연-생성 오버로드 없음 — AS-IS가 실제로 호출하는 5-param 오버로드가 미포팅) — 컴파일조차 안 됨. 설사 4-param으로 우회해도 `AddEffectToPlayer`는 `LegacyBindingBridge.TryToBinding`로 구모델 `ToBinding(string)` 메서드를 리플렉션 조회하는데, 신모델 `ActivateClass`/`ChangeCostClass`는 `ToBinding`을 구현하지 않아 `NotSupportedException`(RD-P6C3-C1, 기존 문서화된 갭)로 즉시 실패. 즉 이 카드의 AS-IS "one-shot cleanup on first matching digivolve" 구조는 신모델에 필요한 두 인프라(getCardEffect 지연-오버로드 + 신모델 player-scope 바인딩 저장소)가 모두 부재. BT1_109는 구모델 유지(`RegisterDigivolutionCostDeltaForPlayer`, 기존 FIDELITY DEBT 주석 그대로 — scope는 정확하나 one-shot 아닌 until-turn-end 지속이라는 기존 공지 편차 유지).
