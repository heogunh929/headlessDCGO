# Headless 포팅 함수 매핑 정의서

## 목적

이 문서는 다른 작업자나 모델이 원본 `DCGO/Assets/...` 의존성 함수와 현재 `HeadlessDCGO.Engine` 포팅 API를 혼동하지 않도록 만든 기준 문서다.

중요한 전제:

- 현재 Headless 구현은 원본 Unity 함수명을 1:1로 복제한 것이 아니다.
- Phase 1의 목적은 Unity, Photon, UI, Coroutine, GameObject 의존성을 제거하고 Headless 계약/API를 고정하는 것이다.
- 따라서 원본 함수명과 현재 함수명이 다르면 누락이 아니라 `역할 기반 재설계`일 수 있다.
- `CONTRACT`, `BRIDGE`, `EXCLUDED` 상태를 `미완성`으로 단순 판단하면 안 된다.

## Canonical Files

모델은 아래 파일을 우선 확인한다.

| 용도 | 파일 |
|---|---|
| 모델용 정규화 CSV | `docs/headless_porting_function_mapping_for_models_ko.csv` |
| 사람용 요약 CSV | `docs/headless_dependency_function_port_mapping_ko.csv` |
| 사람용 상세 Markdown | `docs/headless_dependency_function_port_mapping.md` |
| 원본 의존성 대체 정책 | `docs/headless_complete_dependency_replacement.csv` |
| Headless 파일별 원본 출처 | `docs/headless_source_origin_mapping.csv` |

## CSV 컬럼 정의

`docs/headless_porting_function_mapping_for_models_ko.csv`는 아래 컬럼을 사용한다.

| 컬럼 | 의미 |
|---|---|
| `매핑ID` | 안정적인 행 식별자. 다른 문서에서 이 값을 참조한다. |
| `단계` | 현재 매핑이 속한 포팅 단계. 현재는 대부분 `Phase 1`이다. |
| `분류` | 런타임, 코루틴, Choice, 효과 등 기능 영역. |
| `원본_함수명` | 원본 Unity/DCGO 쪽 함수, 호출, 개념 이름. |
| `원본_위치` | 원본 함수나 의존성이 있던 위치. |
| `원본_의존성_성격` | 원본 코드가 어떤 Unity/클라이언트 의존성을 가진 것인지. |
| `포팅_방식` | 동일명 포팅인지, 역할 기반 재설계인지, 브리지인지, 제외인지. |
| `현재_대표_API` | 현재 Headless에서 확인해야 하는 대표 함수/API. |
| `현재_위치` | 현재 대표 API가 있는 파일/라인. `N/A`면 구현 대상이 아니다. |
| `상태` | `PORTED`, `CONTRACT`, `BRIDGE`, `EXCLUDED`, `FUTURE`. |
| `상태_의미` | 상태의 판정 의미. |
| `모델_확인_규칙` | 다른 모델이 이 행을 검증할 때 따라야 하는 규칙. |
| `후속_작업` | 이후 Phase에서 해야 할 일. |
| `주의사항` | 잘못된 판단을 피하기 위한 경고. |
| `근거` | 이 매핑을 만든 근거 문서나 원문 메모. |

## 상태 정의

| 상태 | 모델이 해야 할 판단 |
|---|---|
| `PORTED` | 현재 Headless 함수/API가 실제로 존재한다. 파일/라인을 열어 확인한다. |
| `CONTRACT` | API 계약은 존재하지만 실제 게임 룰/카드 효과의 완전 구현은 후속 Phase에서 확장한다. 완료 누락으로 단정하지 않는다. |
| `BRIDGE` | 기존 구조를 옮기기 위한 임시 연결부다. 새 구현은 가능하면 직접 Headless 서비스로 붙인다. |
| `EXCLUDED` | 시각/UI/오디오/카메라/클라이언트 전용 의존성이다. Headless 런타임에 구현하면 안 된다. |
| `FUTURE` | 방향만 정해졌고 후속 Goal에서 구현한다. |

## 포팅 방식 정의

| 포팅 방식 | 의미 |
|---|---|
| `역할기반_재설계` | 원본 함수명은 유지하지 않고 같은 게임 의미를 Headless 계약/API로 재구성했다. |
| `계약고정_후속구현` | 호출 표면은 정했지만 세부 룰은 후속 Phase에서 구현한다. |
| `임시브리지` | 원본 접근 방식을 잠시 받아주되 장기적으로 직접 서비스 접근으로 제거한다. |
| `제외` | Headless 런타임에는 필요 없는 클라이언트 전용 의존성이다. |
| `직접구현` | 원본 의미와 거의 같은 동작을 현재 함수가 직접 수행한다. |

## 다른 모델의 확인 절차

1. 원본 함수명 또는 의존성 호출명을 `원본_함수명`에서 먼저 찾는다.
2. 정확히 없으면 부분 문자열로 찾고, `분류`와 `원본_위치`를 함께 본다.
3. `상태`가 `PORTED`이면 `현재_대표_API`와 `현재_위치`를 열어 실제 함수가 있는지 확인한다.
4. `상태`가 `CONTRACT`이면 API는 완료된 계약이고, 세부 룰/카드 효과 구현은 후속 Phase 범위인지 확인한다.
5. `상태`가 `BRIDGE`이면 새 코드에서 그대로 확장하지 말고 가능하면 `EngineContext`, `IZoneMover`, `IChoiceProvider`, `EffectScheduler` 같은 직접 서비스로 연결한다.
6. `상태`가 `EXCLUDED`이면 Headless core에 구현하지 않는다.
7. `현재_위치`가 `N/A`이면 구현 누락이 아니라 제외/외부 어댑터 대상인지 먼저 확인한다.
8. 원본 `DCGO/Assets/...` 파일은 읽기 전용 기준이다. 이 문서 업데이트 때문에 수정하지 않는다.

## 대표 판단 예시

### GManager.Init

- 원본: `GManager.Init`
- 현재 대표 API: `DcgoMatch.InitializeAsync(MatchConfig, CancellationToken)`
- 판단: 원본 이름을 유지하지 않은 것이 정상이다. Headless match lifecycle API로 역할이 이동했다.

### StartCoroutine

- 원본: `StartCoroutine(IEnumerator)`
- 현재 대표 API: `EngineTaskRunner.Enqueue`, `StepAsync`, `RunUntilIdleAsync`
- 판단: Unity coroutine을 복제하지 않고 deterministic task runner로 바꾼다.

### SelectCardEffect

- 원본: `SelectCardEffect.SetUp`, `WaitForEndSelect`, `OnClick`
- 현재 대표 API: `ChoiceRequest`, `ChoiceResult`, `IChoiceProvider.ChooseAsync`
- 판단: UI 창과 클릭 처리는 제거하고 선택 의미만 serializable choice 계약으로 보존한다.

### Photon RPC

- 원본: `PhotonNetwork`, `PhotonView`, `[PunRPC]`
- 현재 대표 API: `SessionContext`, `HeadlessActionQueue`, `GameEvent`
- 판단: 네트워크 transport는 Headless core에 포팅하지 않는다. deterministic local session/action/event로 의미를 옮긴다.

### TextMeshPro / DOTween / Audio

- 원본: UI/애니메이션/오디오 호출
- 현재 대표 API: 없음 또는 `UnityNullObjectPolicy`
- 판단: 게임 상태에 영향이 없으면 `EXCLUDED`다. 구현하지 않는 것이 맞다.

## 금지 사항

- 원본 함수명과 현재 함수명이 다르다는 이유만으로 미구현으로 판정하지 않는다.
- `CONTRACT` 상태를 실제 카드 효과 포팅 완료로 해석하지 않는다.
- `EXCLUDED` 상태의 UI/시각/오디오 기능을 Headless runtime에 새로 넣지 않는다.
- 다음 Phase Goal이 명시되지 않은 상태에서 실제 룰/카드 효과 포팅을 시작하지 않는다.

## 유지보수 규칙

- 새 포팅 API가 생기면 `docs/headless_porting_function_mapping_for_models_ko.csv`에 새 행을 추가한다.
- 기존 행의 의미가 바뀌면 `상태`, `포팅_방식`, `모델_확인_규칙`, `후속_작업`을 함께 갱신한다.
- 사람용 문서와 CSV가 다르면 모델용 정규화 CSV를 우선한다.
