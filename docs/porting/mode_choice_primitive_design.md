# Mode-Choice Activated Effect — 설계 (PRIM-P0-flow Build Order 2)

- 작성일: 2026-07-05. 근거: AS-IS 모드 메뉴 + 헤드리스 활성효과/choice 인프라 전수 조사.
- 대상: "다음 중 하나 선택" 모드 메뉴 카드. AS-IS `SetBoolSelection` 281장 + `SetIntSelection` 79장 = ~360장
  (ALL_CARD_PRIMITIVE_BACKLOG P0 Build Order 2, Mode choice / multi-mode option flow 366장).
- 성격: 흐름 프리미티브 — 모드 메뉴 제시 → 선택된 분기 실행을 하나의 활성 효과로.

## 0. 결론

**라벨-옵션 choice idiom을 재사용.** 신규 choice 인프라는 `ChoiceType.ModeChoice` enum 한 개뿐. 나머지는
전부 기존 재사용. 각 모드 분기는 이미 존재하는 `IActivatedCardEffect`(draw/delete/suspend/bounce/play…)이고,
선택 분기는 기존 `ActivatedEffectResolver.ResolveListAsync`로 재귀 디스패치(`ReuseMainOptionEffect`와 동일 패턴).

## 1. AS-IS 메커니즘 (충실도 앵커)

`UserSelectionManager.SetBoolSelection`(2모드)/`SetIntSelection`(3+모드) + `SelectionElement<T>(message, value,
spriteIndex)`. 카드는 element 리스트를 만들고(조건 불충족 모드는 리스트에서 **생략** — `AD1_007.cs:110`),
메뉴를 열고, `WaitForEndSelect` 후 `SelectedBool/IntValue`로 `if/switch` 분기. 각 분기는 비-모드 카드와 동일한
`SelectPermanentEffect`/`SelectCardEffect`/커먼즈 재사용. 전체가 하나의 `ActivateClass`(단일 활성효과).
메뉴는 mandatory(제시된 모드 중 하나 필수 선택), 분기 내부 optionality는 분기 자체가 처리.
- 캐노니컬: `BT19_090.cs:69`(bool 2모드), `AD1_007.cs:110`(조건부 int 모드).

## 2. 헤드리스 인프라 (재사용)

- **활성효과 모델**: `IActivatedCardEffect`(마커, `CardPortingFramework.cs:659`), 각 구현이 `ResolveAsync`에서
  `ChoiceProvider.ChooseAsync` await 후 행동. 디스패치는 `ActivatedEffectResolver.ResolveListAsync`의
  `switch(cardEffect)`(케이스별 활성효과 타입). 활성 진입: `OptionActivateAction`(OptionSkill)·
  `SecurityResolver`(SecuritySkill). choice pause 재진입은 `DeferredChoicePendingException` 재생 사이클.
- **라벨-옵션 메뉴 idiom(재사용 핵심)**: `DeletionReplacementTiming.OpenKeywordChoice`가 문자열 옵션 리스트를
  합성 id `"{inst}#{option}"` + Label로 ChoiceCandidate화, `Segment(id, idx)`로 파싱 후 `switch` 디스패치.
  AS-IS `SetIntSelection` + `switch(SelectedIntValue)`의 1:1 구조 대응.
- **choice 모델**: `ChoiceRequest`(type·player·message·min/max·canSkip·zone·candidates), `ChoiceCandidate`
  (id·label·zone·selectable), `ChoiceResult`(SelectedIds/IsSkipped). 후보는 entity-id 키 — 합성 id 관용구 지원.
  **모드/메뉴 전용 ChoiceType 없음**(신규 1개 필요).

## 3. 설계

### 신규 프리미티브: `ModeChoiceEffect : IActivatedCardEffect`
```
CardEffectFactory.SelectModeEffect(card, description, params Mode[] modes)
  Mode(string Label, Func<bool>? IsAvailable, ICardEffect Branch)
```
`ModeChoiceEffect` 동작(리졸버 case에서):
1. `AvailableModes()` = `IsAvailable() != false`인 모드만(생략 = AS-IS 조건부 element).
2. 0개면 no-op. 1개 이상이면 `BuildRequest`로 모드당 후보(합성 id `"{inst}#mode#{i}"` + Label,
   `ChoiceType.ModeChoice`, min/max 1, canSkip false = mandatory) → `ChooseAsync`.
3. 선택 id에서 index 파싱 → `available[i].Branch` 획득.
4. 분기를 `ResolveListAsync(context, effectClass, card, players, sink, new[]{ branch }, ct)`로 재귀 디스패치
   — 기존 switch가 분기 타입(Draw/Destroy/Select/Play…)을 해소. 같은 sink·choice 사이클 공유.

### 플러그인 지점
- `Headless/Choices/ChoiceType.cs`에 `ModeChoice` enum 값 1개 추가.
- `ActivatedEffectResolver.ResolveListAsync` switch에 `case ModeChoiceEffect` 1개 추가(재귀 호출은 기존
  `ReuseMainOptionEffect` 패턴 재사용 — 추출 리팩터 불필요).
- `OptionActivateAction`/`SecurityResolver`는 무변경(리졸버 통해 자동 연결). 메뉴 pause·분기 sub-choice pause
  모두 기존 `DeferredChoicePendingException` 재생이 커버.

### 경계
- 개별 카드의 모드 구성(어떤 라벨·어떤 분기)은 per-card 포팅 몫(데이터). 이 설계는 "메뉴 제시 + 분기 디스패치"
  인프라만.
- 분기는 기존 `IActivatedCardEffect` 프리미티브 재사용. 없는 분기 동작은 별도 프리미티브 갭(이 트랙 밖).

## 4. 구현 순서 & 검증
1. `ChoiceType.ModeChoice` → 빌드.
2. `ModeChoiceEffect` 클래스 + `CardEffectFactory.SelectModeEffect` 팩토리.
3. `ActivatedEffectResolver` case.
4. 테스트: 2모드 효과를 활성화해 (a) 메뉴가 열리고, (b) 모드 A 선택 시 A 분기만 실행, (c) 모드 B 선택 시 B만,
   (d) 조건 불충족 모드는 메뉴에서 생략. 전체 스위트 무회귀.

## 5. 관련
- [ALL_CARD_PRIMITIVE_BACKLOG.md](ALL_CARD_PRIMITIVE_BACKLOG.md) P0 Build Order 2.
- 재사용: `DeletionReplacementTiming.OpenKeywordChoice`(라벨 메뉴), `ActivatedEffectResolver`(디스패치),
  `OptionActivateAction`/`SecurityResolver`(활성 진입).
