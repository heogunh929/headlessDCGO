# 범위 밖 파일 대장 — 로드맵 1.5 산출물

2026-07-29. **목적**: 로비·덱에디터·타이틀 계열은 지금은 복사되어 껍데기 심으로 컴파일만 통과한다.
추후 완성도 작업에서 들어낼 때 변환 대장 diff가 삭제 행으로 커지는데, 그 삭제가 "선언된 범위 밖
제거"로 분류되려면(표류 오인 방지, R8) 이 대장이 그 선언이다.

판정 기준: **배틀(대전) 한 판을 돌리는 데 필요한가.** 필요 없으면 범위 밖.

## A. 들어냄 후보 — 메뉴·로비·덱에디터 씬 전용 (배틀측 참조 0 또는 형제 참조뿐 [실측])

Opening 씬/메뉴: `Title` `HomeMode` `BattleMode` `DeckMode` `SelectBattleMode` `PatchNotes`
`LanguagePanel` `OptionPanel` `VolumePanel` `CheckUpdate` `GSSReader` `TestWWW` `GifImage`
로비/방: `RoomManager` `CreateRoom` `EnterRoom` `CRoomElement` `LobbyManager_FriendMatch`
`LobbyManager_RandomMatch` `FirstPlayerIndexIdToggle`
덱 편집: `EditDeck` `DetailCard_DeckEditor` `DeckListPanel` `SelectDeck` `SelectBattleDeck`
`CreateNewDeckButton` `CardPrefab_CreateDeck` `DeckInfoPanel` `DeckInfoPrefab` `TrialDraw`
`CardImagePanel`

참조 그물 [실측]: 위 파일들의 참조자는 거의 전부 같은 목록 안의 형제다(`DeckMode`·`BattleMode`가
허브). 들어낼 때는 **폐포 단위**로 함께 나가야 컴파일이 깨지지 않는다.

## B. 배틀측 실결합 — 들어내기 전 분리 선행 필요 3건 [실측]

| 파일 | 결합 | 조치 |
|---|---|---|
| **`Opening`** | 배틀측 **19파일**이 `Opening.instance` 참조 — 주로 `PlayDecisionSE`/`PlayCancelSE` 등 **사운드 서비스 홀더** 역할(CommandButton.OnPointerClick 등) | 그냥 들어내면 배틀이 깨짐. 사운드 서비스 분리(또는 심 유지) 선행 |
| `SideBar` | `GManager`가 참조(selectCommandPanel 흐름의 SetUpSideBar/OffSideBar) | 배틀 표현층으로 재분류하거나 no-op 심 유지 |
| `Title` | `Effects`가 참조 | 참조 지점 확인 후 결정 |

## C. 배틀 표현층 — 범위 밖 아님 (혼동 주의)

`NextPhaseButton` `CheckCardPanel` `SideBar`(위 B)는 배틀 씬 UI다. 사람 입력 전용이라 headless에서
안 눌릴 뿐, 배틀 코드가 참조하므로 **잔류 대상**이다(no-op으로 살아 있음).

## 들어낼 때의 절차 (선언)

1. A목록 폐포를 한 커밋으로 제거, 커밋 메시지에 본 대장 인용
2. 변환 대장(diff -r) 검사 규칙에 "본 대장 등재 파일의 삭제 행 = 선언된 제거"를 추가
3. B의 3건은 개별 분리 커밋 선행
