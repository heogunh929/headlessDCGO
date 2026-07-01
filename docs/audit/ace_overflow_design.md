# AceOverflow 헤드리스 설계안 (PRIM-W4 잔여)

## AS-IS 정독 결과 (추측 없음)

`AceOverflowClass.Overflow()` (CardController.cs:5836):
```
필터: IsACE && !IsFlipped && (배틀에어리어 존재 || 브리딩-디지몬 존재)
정렬: 턴 플레이어 소유 먼저
각 카드: owner.AddMemory(-OverflowMemory)  + 로그
```

**트리거 표면(전체 호출부):**
| 위치 | 메서드 | 이동 |
|---|---|---|
| CardObjectController:528 | `RemoveField(permanent, ignoreOverflow=false)` | 필드→제거(삭제/트래시) |
| CardObjectController:598 | `AddHandCards` | 필드→손 (바운스) |
| CardObjectController:787 | `AddLibraryTopCards` | 필드→덱 top |
| CardObjectController:869 | `AddLibraryBottomCards` | 필드→덱 bottom |
| CardController Degeneration ×2, BT18_042/BT17_098/BT24_093 | 카드별 | 진화원 이탈 시 |

**결론: 중앙 규칙** — ACE 디지몬(뒤집히지 않음)이 **필드(배틀/브리딩)를 떠날 때** 소유자가 그 카드의 `OverflowMemory`만큼 메모리를 잃는다. 카드 데이터(`IsACE`, `OverflowMemory`)는 `OfficialCardListUtility`가 정의별로 세팅(예: 4/4/3).

## 헤드리스 대응

**중앙 이동 경로 = `MatchStateMutationSink`.** 카드가 필드를 떠나는 mutation에서 hook.

### 1. 카드 데이터 키
- CardRecord(정의) 메타: `isAce` (bool), `overflowMemory` (int) — 카드-데이터 로더가 정의별 세팅(포팅 시 config).
- CardInstance 메타: `isFlipped` (bool, 기본 false).

### 2. `AceOverflowGate` (신규, Headless/Runtime)
```csharp
// 필드를 떠나는 ACE 카드의 메모리 페널티(없으면 null)
static int? MemoryPenaltyOnLeave(EngineContext ctx, HeadlessEntityId cardId, ChoiceZone fromZone)
  // fromZone ∈ {BattleArea, BreedingArea} && isAce && !isFlipped ? overflowMemory : null
```

### 3. sink hook (필드-이탈 mutation)
- `ApplyDelete` (→trash, = RemoveField)
- `ReturnToHandKind` (→hand)
- `ReturnToDeckTopKind` / `ReturnToDeckBottomKind` (→deck)

각 지점에서 **이동 직전** 카드의 현재 zone을 읽어 `MemoryPenaltyOnLeave`가 양수면 소유자에게 `AddMemory(-overflowMemory)` emit. (from-field 조건이 "배틀/브리딩에 있었음"을 보장 → 덱에서 손으로 뽑는 ACE는 트리거 안 함.)

- `ignoreOverflow` 대응: 일부 이동(예: 효과가 명시적으로 오버플로우 무시)을 위해 mutation value `ignoreOverflow` 플래그 지원.

### 4. 카드-facing
**팩토리 불요** — 데이터 기반 자동 규칙. 포팅된 ACE 카드는 `isAce=true` + `overflowMemory=N`만 데이터에 두면 엔진이 처리(AS-IS와 동일: OfficialCardListUtility가 데이터 세팅, 규칙은 엔진 전역).

## 범위/충실도 노트
- **per-card 처리**: 호출부 대부분 단일 카드. 턴-플레이어 정렬은 다중 동시에만 의미 → 단일 처리로 충분(문서화).
- **스택 다중 ACE**(RemoveField가 stack 전체) — 드묾. 1차는 top 카드 기준, 필요 시 확장.
- **격리 테스트**: 필드→trash/hand/deck 각각 ACE 이탈 시 메모리 -N 검증 + 비-ACE/뒤집힘/덱-출발은 no penalty(control).

## 규모
게이트 1개(~15줄) + sink hook 3~4곳(각 2~3줄) + 메타 키 2개. **바운드됨, 서브시스템 아님.**
