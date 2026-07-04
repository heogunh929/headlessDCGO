# 표현 번역표 (condition/술어 안의 원본 표현 → 헤드리스 표현)

> **용도**: 원본 카드의 `condition` 람다 / 술어 안에 나오는 **멤버 접근·표현**을 헤드리스로 옮길 때의 결정론적 치환표. (팩토리 호출은 `PRIMITIVE-CATALOG.md`, 코루틴 의도는 `PORTING-RECIPE.md`의 의도→팩토리 표를 본다.)
>
> **규칙**: condition 안의 모든 멤버 접근은 이 표에서 조회해 치환한다. **표에 없고 그대로 컴파일된다는 확신도 없으면 그 분기는 STOP.** 표현을 발명하지 말 것.
>
> 배경: 원본은 `Permanent`/`Player` 같은 풍부한 객체를 직접 항해하지만, 헤드리스는 그 핸들을 얇은 ID/뷰로 미러하고 상태는 게이트·서비스로 우회한다. 아래 왼쪽 표현 다수는 **이름 그대로 존재**하고, 일부만 형태가 다르다.

## 1. 그대로 쓰는 것 (동일 이름 — 치환 불필요)

| 원본 표현 | 헤드리스 | 비고 |
|---|---|---|
| `permanent.TopCard` | 동일 (`Permanent.TopCard` → `CardSource`) | 최다 빈도(7,916회) |
| `permanent.DigivolutionCards` / `.Count` | 동일 | `IReadOnlyList<CardSource>` |
| `.Level` / `card.Level` | 동일 | 연속효과 폴딩 포함 |
| `permanent.DP` / `.BaseDP` | 동일 | 수정치 폴딩 포함 |
| `permanent.IsSuspended` / `.IsDigimon` / `.IsTamer` / `.HasNoDigivolutionCards` | 동일 | |
| `CardEffectCommons.Is*/CanTrigger*/HasMatchCondition*(...)` | 동일 | 카탈로그 "Commons 헬퍼 마스터"에서 시그니처 확인 |
| `card.PermanentOfThisCard()` | 동일 (`PermanentView` 반환) | 단 키워드 조회는 §2 |

## 2. 형태가 다른 것 (치환 필요)

| 원본 표현 | 헤드리스 표현 | 비고 |
|---|---|---|
| `card.PermanentOfThisCard().HasPierce` | `CardEffectCommons.HasPierce(card)` | 키워드는 레지스트리 게이트 경유 |
| `...().HasBlocker` / `...().HasJamming` | `CardEffectCommons.HasBlocker(card)` / `HasJamming(card)` | 〃 |
| `permanent.HasPierce/HasBlocker/HasJamming/HasRush/HasReboot` (술어 인자 `Permanent`) | 동일 이름 프로퍼티 **존재** — 그대로 사용 | `Permanent.HasKeyword(string)`도 가능 |
| 기타 키워드 보유 확인 | `CardEffectCommons.HasKeyword(card, ContinuousKeywordGate.<키워드>)` | 키워드 상수는 `ContinuousKeywordGate` |
| `card.Owner.MemoryForPlayer` | `CardEffectCommons.MemoryForPlayer(card)` | 소유자 관점 게이지 값 |
| `CardColors.Contains(CardColor.X)` | `HasCardColor("X")` (CardSource 메서드) | 색은 **string** — `CardColor` enum 없음 |
| `CardColor.X` (단독) | `"X"` 문자열 | 예: `CardColor.Red` → `"Red"` |
| `카드명/특징 비교` | `EqualsCardName/ContainsCardName/EqualsTraits/ContainsTraits("...")` | CardSource 메서드 |
| `card.Owner.Enemy` | 직접 대응물 없음 — 상대 조회는 `CardEffectCommons.IsOpponent*` 계열 헬퍼 사용 | 없으면 STOP |
| `card.Owner.HandCards/TrashCards/SecurityCards/LibraryCards` (개수·존재 확인) | `CardEffectCommons`의 존-조회 헬퍼 (카탈로그 확인) | 직접 리스트 항해 금지, 없으면 STOP |

## 3. 코루틴/명령형 (여기 아님 — 레시피 의도→팩토리 표로)

`new DrawClass(...).Draw()`, `owner.AddMemory(±N)`, `CardEffectCommons.ChangeDigimonDP(...)`(코루틴 안 즉발형) 등 **행동**은 표현 치환이 아니라 **의도→팩토리 매핑** 대상이다. `PORTING-RECIPE.md`의 표를 먼저 보고, 대응 팩토리가 카탈로그에 있으면 사용, 없으면 STOP.

## 4. 유지보수

- 새 갭 발견 시: 강모델이 헬퍼를 신설(§`CardPortingFramework.cs`의 EXPR-MAP 구획)하고 이 표에 행을 추가한다. 로컬 모델은 표를 **소비만** 한다.
- 근거 실측(2026-07-03, DCGO 전수): `.TopCard` 7,916 · `.DigivolutionCards` 1,837 · `.Level` 1,266 · `.CardColors` 777 · `.DP` 511 · `.MemoryForPlayer` 58 · `.HasBlocker` 27 · `.HasJamming` 4 · `.HasPierce` 1.
