# 로드맵 — AS-IS 직접 빌드 전환

2026-07-29 수립. 이전의 "손포팅 후 감사·수리" 노선을 폐기하고 재작성.

## 목표

DCGO를 **RL 학습환경**으로 만든다. 헤드리스화는 수단이다.

**방법**: AS-IS 소스를 **그대로 쓴다.** 유니티 종속부는 substrate(rlEngine)가 대응 심볼을 제공하고,
원본은 기계적으로 변환한다. new-TO-BE는 **손으로 새로 쓰는 트리가 아니라 AS-IS의 복사본에서 출발**한다.

---

## 명칭 (고정)

| 명칭 | 경로 | 무엇인가 | 커밋 |
|---|---|---|---|
| **AS-IS 소스** | `DCGO/Assets/Scripts/` | 원본 유니티 게임 소스. **유일한 진실.** 수정 금지 | ✗ (6.6GB 프로젝트) |
| **old-TO-BE 소스** | `~/git/headlessDCGO.handport-backup/Assets/` | 손포팅 트리. **실패 확정** | ✗ (저장소 밖) |
| **new-TO-BE 소스** | `src/HeadlessDCGO.Engine/Assets/Scripts/` | AS-IS 복사본에서 출발해 수정해 갈 소스 | **✓** |
| substrate | `src/HeadlessDCGO.Engine/Headless/` | 유니티·Photon 대응 심볼 | ✓ |
| 파이프라인 | `tools/AsIsSync/` | AS-IS → new-TO-BE 복사·변환·재동기화 | ✓ |

substrate는 소스 3종과 층이 다르다 — AS-IS를 **대체하는 게 아니라 떠받친다.**

### old-TO-BE 취급 (사용자 확정)

- 저장소 **바깥**에 백업 — `~/git/headlessDCGO.handport-backup/` (4,361파일 / 65M)
- **참조 대상 아님.** 판정 근거로 쓰지 않는다 — 자기인용 루프가 된다
- **커밋 대상 아님**
- new-TO-BE가 **그 경로를 물려받는다**

### 경로를 물려받는 이유

경로 구조가 AS-IS와 **완전히 정렬**되어 `diff -r`가 그대로 성립한다.
새 경로를 파면 과도기 비용만 생기고, old-TO-BE는 어차피 참조 대상이 아니라 나란히 둘 이유가 없다.

---

## 이 전환의 근거 [실측]

AS-IS 4,354파일 + substrate 133파일만으로 빌드(old-TO-BE **0파일** 투입):

| | |
|---|---:|
| 오류 없이 그대로 컴파일 | **4,198파일 (96.4%)** |
| 오류 있는 파일 | 156 (엔진층 142 · 카드층 14) |
| 고유 누락 심볼 | **102** (거의 전부 표현층) |
| **카드**: 원본 그대로 통과 | **3,904 / 3,918 (99.6%)** |

핵심 게임로직 `CardController.cs`(5,988줄)·`Permanent.cs`(4,187줄)·`CardEffectCommons.cs`·
`CardEffectFactory.cs`가 **원본 그대로 오류 0**이다.

즉 손포팅은 필요 없었다. old-TO-BE가 AS-IS와 벌린 차이 114,460행은 수리할 목록이 아니라
**하지 않아도 될 일을 하다 표류한 거리**다.

프로브: `probe.csproj`에 `DCGO/Assets/Scripts/**` + `Headless/**`만 포함, `net8.0`/`ImplicitUsings=enable`/`Nullable=enable`.

---

## 유일한 계기판 — 변환 대장

```
diff -r DCGO/Assets/Scripts  src/HeadlessDCGO.Engine/Assets/Scripts
```

**이 diff가 "우리가 원본에 손댄 것 전량"이다.**

- 작으면 성공. old-TO-BE처럼 114,460행으로 자라면 **같은 실패의 재발**
- 수정이 스크립트에서 왔든 손에서 왔든 상관없다 — **보이고, 작고, 사유가 있으면** 된다
- `tools/AsIsSync`가 선언한 변환 규칙으로 이 diff가 **전부 설명되는가**를 게이트로 검사한다.
  설명되지 않는 행 = 누군가 손으로 고쳤다는 뜻이고, 그 자체가 경보다
- `DCGO/`가 로컬 전용이므로 이 게이트는 로컬에서만 돈다

### 왜 복사하는가 (AS-IS 제자리 빌드를 안 하는 이유)

빌드만 목적이면 복사는 불필요하다 — 프로브가 `DCGO/`를 제자리에서 컴파일해 증명했다.
복사는 다음 둘을 사기 위한 거래다:

1. **저장소 자족성** — 지금은 저장소만으로 빌드가 안 된다(6.6GB 로컬 폴더 필수).
   워크트리마다 `DCGO` 심링크를 걸어야 했고, 잊어서 테스트 78개가 가짜로 실패한 적이 있다
2. **upstream 델타 가시화** — AS-IS를 직접 빌드하면 원본이 갱신됐을 때 **아무 diff 없이 빌드 내용이 바뀐다.**
   new-TO-BE가 커밋돼 있으면 sync 후 `git diff`가 **곧 upstream 변경 목록**이다.
   원본 갱신을 기계적으로 따라가는 것이 이 프로젝트의 존재 이유이므로 이건 부가 기능이 아니다

---

## 원칙

1. **변환 최소.** 변환은 substrate가 도저히 흡수할 수 없을 때만 쓰는 탈출구다.
   기본값은 순수 복사. 변환을 추가하기 전에 "substrate로 못 하나"를 먼저 묻는다.
2. **컴파일러가 1차 오라클.** 두 번째 오라클을 만들지 않는다.
3. **충실도 질문의 표면이 줄었다.** 소스가 AS-IS이므로 "포트가 원본과 같은가"는 더 이상 질문이 아니다.
   남는 질문은 **"심이 유니티처럼 행동하는가"** 하나이고, 표면은 102개 심볼이다.
4. **심 헌장**: 심 파일 헤더에 **무엇이 실제로 돌고 무엇이 선언뿐인지** 명시한다.
   검증하지 않은 것은 쓰지 않는다.
5. **파이프라인은 재실행 가능·멱등이어야 한다.**

---

## 단계 0 · 정리

**목적** old-TO-BE 잔재를 제거해 AS-IS와 타입이 충돌하지 않게 한다.

| # | 작업 | 상태 |
|---|---|---|
| 0.1 | old-TO-BE 백업 (저장소 밖) | **완료** — 4,361파일 / 65M |
| 0.2 | `src/HeadlessDCGO.Engine/Assets/` 비우기 | 대기 |
| 0.3 | **substrate 133파일 삼중 분류** | 대기 |
| 0.4 | 폐기 산출물 정리 | 대기 |

### 0.3 — substrate 삼중 분류 (이 단계의 실제 작업)

`Headless/`는 old-TO-BE를 받치려고 만들어졌고, 그 과정에서 **게임 룰을 흡수**했다
(`AttackPipeline`·`SecurityResolver`·`DeletionReplacement*`·`DigivolutionStackHelpers` 등).
AS-IS가 그 로직을 직접 제공하므로 **중복분은 삭제 대상**이다.

판정 기준: *유니티/Photon/헤드리스 고유 결정의 대체물인가* → **유지**. *게임 규칙인가* → **삭제**.

**게이트** `Headless/` 잔여가 전부 substrate로 분류됨. 게임 룰 0.

**위험(R6)** 삭제한 게임로직을 AS-IS가 실제로 대체하는지 확인 없이 지우면 컴파일은 되나 동작이 빈다.
→ 삭제 전 각 파일의 AS-IS 대응 지점을 **실소스로 지목**할 것.

### 0.4 — 폐기 산출물

`docs/audit/diffs/`·`rawdiff/`·`audit_prompt.md`·`residual_ranking.tsv`·`audit_queue.tsv`,
결함 대장 O-195~O-200. 전부 old-TO-BE를 잰 것이다.
**`fulltree_diff_census.md`는 전환 근거로 남긴다.**

---

## 단계 1 · 컴파일 게이트

**목적** new-TO-BE가 substrate만으로 오류 0.

| # | 작업 |
|---|---|
| 1.1 | `tools/AsIsSync` — 복사 파이프라인. 인코딩 정규화(BOM · **ISO-8859 3파일** → UTF-8), 줄끝, 멱등성, 재동기화 |
| 1.2 | AS-IS → `Assets/Scripts/` 초기 복사 + 커밋 |
| 1.3 | **심볼 102개 삼중 분류** ← 핵심 |
| 1.4 | 심 작성 |
| 1.5 | 실패 156파일 통과 — **범위 밖 파일은 껍데기 심으로 컴파일만 통과시키고 대장에 등재** |

### 1.5 — 범위 밖 파일 (사용자 확정 07-29)

로비·덱에디터 계열(`EditDeck`·`RoomManager`·`DetailCard_DeckEditor`·`Opening`·`SelectBattleDeck` 등)은
**지금은 복사하고 껍데기 심으로 컴파일만 통과**시킨다. 복사 단계에서 거르지 않는다 —
거르면 변환 대장 diff가 지저분해지고, 이를 참조하는 AS-IS 파일이 깨진다.

**단 추후 완성도 작업에서 들어낼 소스로 판단한다.** 그러려면 지금 식별해 둬야 한다:

- 산출물 `docs/out_of_scope.md` — 범위 밖 파일 대장. 파일별로 **왜 범위 밖인지**와
  **게임 룰이 이 파일을 참조하는가**를 기록
- 들어내는 시점에 변환 대장 diff가 **삭제 행**으로 커진다.
  그 삭제는 "선언된 범위 밖 제거"로 대장에 분류돼야 하며, 표류로 오인되면 안 된다

### 1.3 — 심볼 삼중 분류

| 부류 | 뜻 | 대략 |
|---|---|---|
| **A. 빈 껍데기로 충분** | 선언만 있으면 컴파일되고, 룰이 값을 읽지 않음 | 대다수 |
| **B. 실동 필요** | 게임 룰이 값을 읽어 판단함 | 소수 — **여기가 전부** |
| **C. 범위 밖** | 로비·덱에디터 전용 | ? |

관측된 102개의 성격:

- **어트리뷰트** `SerializeField` `Header` `HideInInspector` `CreateAssetMenu` `RequireComponent`
  `ExecuteInEditMode` `RuntimeInitializeOnLoadMethod` `TextArea` `DefaultExecutionOrder` `PunRPC`
- **UI 위젯** `Button` `Canvas` `CanvasGroup` `Image` `Text` `Toggle` `Slider` `Dropdown` `InputField`
  `ScrollRect` `RectTransform` `EventTrigger` `UIBehaviour` `ILayoutGroup` `IPointer*Handler`
  `BaseEventData` `PointerEventData` `TMP_*` `TextMeshPro(UGUI)` `TMPro` `Font`
- **렌더·씬** `GameObject` `Transform` `Camera` `Material` `MeshRenderer` `MeshCollider`
  `SpriteRenderer` `LineRenderer` `ParticleSystem` `Animator` `RuntimeAnimatorController`
  `Rigidbody` `Texture2D` `Sprite` `Quaternion` `Vector3` `Color` `Rect` `ScriptableObject` `TextAsset`
- **오디오** `AudioClip` `AudioSource`
- **이벤트** `UnityEvent` `UnityAction`(3종)
- **서드파티** `DG`(DOTween) `Cinemachine` `Coffee` `Shapes2D` `UIShiny` `WebP` `WebSocketSharp`
  `AutoLayout3D` `ExitGames` `RoomInfo` `JetBrains` `UnityEditor` `Unity`
- **누락 네임스페이스 16** `UnityEngine.{UI,Events,EventSystems,SceneManagement,XR,Networking,TextCore,UIElements,Rendering,Pool}` `Photon.Realtime` 등

**판정 방법**: 심볼별로 AS-IS에서 사용처를 `grep -a`로 전수하고,
그 사용처가 **룰 파일인지 표현 파일인지**, 값을 **쓰기만 하는지 읽어서 분기하는지**로 가른다.
"읽어서 분기한다"가 하나라도 있으면 B다.

**게이트** `dotnet build` 오류 0.

### R1 선행 실측 결과 (2026-07-29) — B 분류가 좁혀졌다

**컴파일 통과는 동작 보증이 아니다.** 심이 no-op인데 룰이 값을 읽으면 조용히 틀린다.
착수 전에 두 가지를 실측했다.

#### R3 — 화면 좌표·씬 계층이 룰을 움직이는가 → **대체로 아니다. 단 예외 1**

AS-IS 전체 사용량: `.parent` 233 · `GetChild` 153 · `childCount` 105 · `transform` 599.
룰 파일에서는 적다 — `AutoProcessing`·`AttackProcess`·`CardEffectCommons` **0**,
`CardController` 7 · `Permanent` 6 · `CardSource` 12 · `Player` 25 · `TurnStateMachine` 45.

용례를 전부 읽은 결과:
- `Permanent.cs` 6건 — 전부 `SetActive` **표시 전용**
- `TurnStateMachine.cs` 45건 — `SetActive`·타겟 화살표·핸드 위치·브레인스톰 연출. **전부 표시**
- `CardSource.cs:2306-2352` `PreferredFrame()` — 빈 배틀에어리어 슬롯 중 **어디에 놓을지**를
  화면 좌표로 정렬해 고른다. **배치 결정이지 룰이 아니다**(E-01 영역)
- `CardController.cs:1588-1612` — 좌표를 ±5로 비교해 `move` 플래그를 세우지만,
  그 플래그는 `MovePermanent(...)`(재배치 **연출**)만 가른다. 상태 변경 없음
- `CardController.cs:660` — `GetChild(0).activeSelf`로 `noHandCard`를 정하고,
  그것은 카드 이동 **연출**만 가른다

> **예외 — `Player.cs:23-44` [B 확정]**
> `BattleAreaFrameParent.childCount` / `GetChild(i)` 로 **`fieldCardFrames` 슬롯 배열을 구성**한다.
> `BreedingAreaFrameParent`도 같다. 즉 **필드 슬롯 개수가 유니티 씬 계층에서 나온다.**
> substrate가 이 계층을 **합성**해 주지 않으면 `fieldCardFrames`가 비고,
> 이를 쓰는 `PreferredFrame()`·`CanMove`·플레이 경로가 전부 무너진다.
> → `Transform`은 **`childCount`/`GetChild`가 실제로 동작해야 하는 B 부류**이고,
> 프레임 트리를 만들어 주는 초기화가 필요하다.

#### R2 — `Effects` 대기가 룰 상태나 순서를 만드는가 → **아니다**

룰 코드가 `Effects`를 `yield`로 기다리는 곳 **516군데**. 그러나 `Effects.cs` 본문은 순수 연출이다.

| 룰 상태 변경 흔적 | 건 | | 연출 API | 건 |
|---|---:|---|---|---:|
| `HandCards.Add/Remove` | **1 — 주석 처리됨** | | `DOTween` 계열 | 65 |
| `Trash/Library/Security/Permanents` | **0** | | `SetActive` | 58 |
| `Memory =` | **0** | | `Instantiate`/`Destroy` | 77 |
| `Suspend`/`Unsuspend` | **0** | | `WaitForSeconds` | 33 |
| `EffectList`/`UntilOwnerTurnEndEffects` | **0** | | 오디오 | 33 |

**룰 상태 변경이 0이다.** 유일한 `HandCards.Remove`(:156)는 주석 처리돼 있다.

`WaitWhile(() => !end)` 의 `end`는 DOTween `AppendCallback`이 세운다 —
**애니메이션 완료 대기**일 뿐이므로 no-op 즉시 반환이 상태상 안전하다.

> **단서 2가지**
> - `:932 WaitWhile(() => ShowCardParent.childCount >= 1)` · `:947 WaitUntil(() => handCard != null)` —
>   **사용자가 카드 표시 패널을 닫기를 기다린다.** 상호작용 지점이므로 `IChoiceProvider`(또는 자동 해제) 소관
> - 순서 위험은 남지만 **R4로 환원된다** — 단일 스레드 드라이버에는 병렬 코루틴이 없으므로,
>   대기 시간이 사라져 생기는 인터리빙 변화는 fire-and-forget `StartCoroutine` 191곳에서만 발생한다

#### 남은 B 후보

- **`Transform`** — `childCount`/`GetChild` 실동 + 프레임 트리 합성 (`Player.cs:23-44`)
- **`GameObject`** — 위 계층의 노드로서 실체 필요. `SetActive`/`activeSelf`는 표시로 확인됨
- **`GManager`** (오류 342) — 신 싱글턴이자 모든 접근 경로. 실동 필수
- 나머지 UI·렌더·오디오·어트리뷰트·서드파티는 **A(빈 껍데기)** 로 보인다

`Color`는 안전하다 — AS-IS 자체 `CardColor` enum(`CEntity_Base.cs:381`).

---

## 단계 2 · 기동

**목적** 한 판이 처음부터 끝까지 돈다.

| # | 작업 |
|---|---|
| 2.1 | **수명주기** — `GManager.instance`·`ContinuousController.instance`를 유니티 없이 인스턴스화. `Awake`/`Start` 순서 재현 |
| 2.2 | `MonoBehaviour` 심 — AS-IS가 실제로 쓰는 멤버 전수 확인 |
| 2.3 | **코루틴 드라이버 접속** — 이미 실증 완료. AS-IS가 원본 `IEnumerator`라 **변환 작업 자체가 없다** |
| 2.4 | `WaitUntil`/`WaitWhile` 게이트 파킹 (드라이버에 구현됨) |
| 2.5 | **선택 지점 → `IChoiceProvider`** — AS-IS의 UI 대기 지점을 가로챔 |
| 2.6 | **Photon RPC → 로컬 디스패치** — 전부 `RpcTarget.All`이라 단일 프로세스에선 로컬 1회가 등가 |
| 2.7 | fire-and-forget `StartCoroutine` 191곳 — **설계 필요** |

**게이트** 무작위 대전 N판 완주, 미처리 예외 0.

---

## 단계 3 · 결정성

**목적** 같은 시드 → 같은 트래젝토리.

| # | 작업 |
|---|---|
| 3.1 | `UnityEngine.Random` → 시드 RNG. 전수 치환 |
| 3.2 | 시간·프레임 의존 제거 (`Time.*`, `WaitForSeconds` 무시) |
| 3.3 | 컬렉션 순회 순서 안정화 |

**게이트** 트래젝토리 다이제스트 A/B 무불일치. **이미 검증된 수단**(`isAI` 작업 123판 0불일치).

---

## 단계 4 · RL 인터페이스

**목적** 학습 루프 실동.

| # | 작업 |
|---|---|
| 4.1 | 관측 인코딩 — 공개 상태 + 자기 비공개 상태 |
| 4.2 | 행동 공간 — 합법수 마스킹 |
| 4.3 | `step` / `reset` |
| 4.4 | 벡터화 — **워커 6** (물리 코어 6) |
| 4.5 | 기존 RL 자산(`RlVectorHost` 등) 접속 |

**게이트** 학습 루프가 돌고 eval이 나온다.

---

## 이 전환으로 소멸하는 트랙

| 트랙 | 규모 | 사유 |
|---|---|---|
| 카드 포팅 | 3,574장 | 원본 3,904/3,918이 그대로 컴파일 |
| 코루틴 복귀 | 2,783곳 + 손 86 | AS-IS가 원본 코루틴 |
| 전수 대조 감사 | 306파일 / 8천만 토큰 | 감사 대상이 삭제됨 |
| 결함 대장 수리 발주 | OPEN 200 | old-TO-BE의 결함 |
| 실체 모델 7단계 | 1,021줄 설계 | AS-IS `Player`/`Permanent` 그대로 씀 |
| namespace 정리 | 520파일 | AS-IS 네임스페이스 그대로 |
| substrate 15뿌리 | 282행 | 뿌리 자체가 old-TO-BE의 우회였음 |

---

## 위험 등록부

| # | 위험 | 완화 |
|---|---|---|
| R1 | **no-op 심이 조용히 틀림** | 단계 1.3의 B 분류. 심 헤더에 실동/선언뿐 명시. **선행 실측으로 B 후보가 `Transform`·`GameObject`·`GManager`로 좁혀짐** |
| R2 | ~~`Effects` 대기 타이밍 소실~~ | **해소** — `Effects.cs` 룰 상태 변경 0, 순수 연출. 순서 위험은 R4로 환원 |
| R3 | ~~트랜스폼 계층이 존 소속을 표현~~ | **부분 해소** — 좌표는 룰이 아님. 단 `Player.cs:23-44`가 씬 계층에서 슬롯 배열을 만든다 → **프레임 트리 합성 필요** |
| R4 | fire-and-forget 병렬성 | 단계 2.7 설계 |
| R5 | ISO-8859 3파일 문자열 깨짐 | 복사 단계 인코딩 정규화 + 육안 확인 |
| R6 | substrate 게임로직 삭제 시 공백 | 삭제 전 AS-IS 대응 지점 실소스 지목 |
| R7 | AS-IS 자체 버그 | **재현 대상이지 수정 대상이 아니다** |
| R8 | new-TO-BE가 다시 표류 | 변환 대장 게이트. diff가 커지면 경보 |

---

## 확정된 결정

> **열린 결정 없음. 로드맵 확정(2026-07-29).**

| 일자 | 항목 | 결정 |
|---|---|---|
| 07-29 | old-TO-BE 취급 | 저장소 밖 백업. **참조 대상 아님 · 커밋 대상 아님** |
| 07-29 | new-TO-BE 경로 | old-TO-BE 경로를 **물려받음** (`src/HeadlessDCGO.Engine/Assets/Scripts/`) |
| 07-29 | new-TO-BE 커밋 | **커밋한다** (`DCGO/`만 제외) |
| 07-29 | `.meta` 4,777개 | **복사 제외가 기본.** 추후 필요하면 재결정 |
| 07-29 | 범위 밖 파일 | **껍데기 심으로 컴파일만 통과 + 대장 등재.** 추후 완성도 작업에서 들어냄 |
| 07-29 | R1 선행 실측 | 실시함. R2 해소 · R3 부분 해소 · **B 후보 3개로 좁혀짐** |
| 07-29 | **E-01 필드 슬롯** | substrate가 합성할 프레임 트리를 **충분히 크게** 만든다. 용량 제한이 사실상 걸리지 않게 한다 |
