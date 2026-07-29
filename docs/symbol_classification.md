# 심볼 분류 — 단계 1.3

2026-07-29. 대상 = AS-IS를 substrate에 얹어 빌드했을 때 누락된 **형식 86 + 네임스페이스 16**.

## 분류 기준

| 부류 | 뜻 |
|---|---|
| **A** | **빈 껍데기로 충분.** 선언만 있으면 컴파일되고, 룰이 값을 읽어 분기하지 않는다 |
| **B** | **실동 필요.** 룰 또는 행동 공간이 이 값을 읽어 판단한다 |
| **C** | **범위 밖.** 로비·덱에디터 전용. 껍데기로 통과시키고 추후 들어낸다 |

판정은 두 단계로 했다.
1. 기계 1차 — 심볼별로 AS-IS 전체 사용량 / 룰층 사용량 / 조건절 사용량 집계
2. 손 확인 — **1차는 타입 이름만 세므로 멤버 접근을 놓친다**(`BattleAreaFrameParent.childCount`에는
   "Transform"이라는 글자가 없다). 룰층의 유니티 멤버 접근을 따로 전수해 조건절 사용을 전부 읽었다

---

## 결론 — B는 2개뿐이다

| 심볼 | 왜 B인가 | 필요한 실동 |
|---|---|---|
| **`GameObject`** | `activeSelf`가 **행동 공간을 가른다** | 실제 활성 플래그 + 정체성 + `.transform` + `.GetComponent<T>()` |
| **`Transform`** | 씬 계층이 **필드 슬롯 배열을 공급한다** | `childCount` · `GetChild(int)` · `.parent` · `.gameObject` |

나머지 84개 형식과 16개 네임스페이스는 **A 또는 C**다.

### B-1 · `GameObject.activeSelf`

```
TurnStateMachine.cs:1429
  if (fieldPermanentCard.gameObject.activeSelf && ThisPermanent.CanDeclareSkill() || ...)
      fieldPermanentCard.OnSelectEffect(1.1f);
      fieldPermanentCard.AddClickTarget(...);
```
**선택 가능한 퍼머넌트를 등록하는 루프**다. 헤드리스에서 이 경로가 곧 행동 공간이 된다.
`activeSelf`가 항상 false면 **아무것도 선택할 수 없다.**

```
commandText.gameObject.activeSelf        24곳 (카드층 6 포함)
  yield return new WaitWhile(() => GManager.instance.commandText.gameObject.activeSelf);
```
**생존성 문제**다. 항상 true면 영원히 멈추고, 항상 false면 즉시 통과한다.
의미는 "플레이어가 안내를 확인할 때까지 대기"이므로 **`IChoiceProvider`/펌프 소관**(단계 2.5).

기타 조건절: `ShowUseHandCard.activeSelf`(CardController·AutoProcessing·EX5_053),
`ShowingHandCard.activeSelf`(연출 게이트), `TrashHandCard.activeSelf`(BT6_078).

### B-2 · `Transform.childCount` / `GetChild`

```
Player.cs:23-44
  for (int i = 0; i < BattleAreaFrameParent.childCount; i++)
      if (BattleAreaFrameParent.GetChild(i).childCount >= 2)
          fieldCardFrame.Frame        = BattleAreaFrameParent.GetChild(i).GetChild(0).gameObject;
          fieldCardFrame.Frame_Select = BattleAreaFrameParent.GetChild(i).GetChild(1).GetComponent<Image>();
```
**필드 슬롯 개수가 유니티 씬 계층에서 나온다.** 합성하지 않으면 `fieldCardFrames`가 비고
`PreferredFrame()`·`CanMove`·플레이 경로가 전부 무너진다. `BreedingAreaFrameParent`도 같다.

**E-01 확정에 따라 슬롯을 충분히 크게 합성한다** — AS-IS 용량 판정 코드는 그대로 실행되지만
빈 슬롯이 늘 남아 사실상 걸리지 않는다. 자세한 근거는 `docs/audit/sanctioned_exceptions.md`.

### 딸린 요구 — `GetComponent<T>`

B는 아니지만 **B의 실현에 필수**다. 누락 심볼 목록에는 없지만(`MonoBehaviour` 심에 딸린 멤버),
사용량이 압도적이다.

| 대상 | 호출 |
|---|---:|
| `GetComponent<SelectPermanentEffect>` | 3,423 |
| `GetComponent<SelectCardEffect>` | 1,242 |
| `GetComponent<SelectHandEffect>` | 1,181 |
| `GetComponent<Effects>` | 757 |
| `GetComponent<SelectAttackEffect>` | 142 |

**카드층에서만 6,492회.** 카드가 선택 흐름에 도달하는 유일한 경로이므로 실제 컴포넌트 레지스트리가 필요하다.
이 대상들(`Effects`·`Select*Effect`)은 전부 **AS-IS 클래스**이므로 구현은 AS-IS가 제공한다 —
substrate는 **찾아주기만** 하면 된다.

> **substrate가 만들 것은 결국 하나다: 최소 객체·컴포넌트 모델.**
> `GameObject`(활성 플래그 + 정체성) + `Transform`(계층) + 컴포넌트 레지스트리.
> 나머지 84개는 이 위에 얹히는 빈 타입이다.

---

## A · 빈 껍데기로 충분

### 확인된 근거

- **`Color`** — 룰층 105건은 전부 AS-IS 자체 `CardEffectCommons.IgnoreRequirement.Color` enum과
  주석·로그 문자열이다. `UnityEngine.Color`는 정적 접근 32 + `new Color` 76으로 표시용뿐이고,
  AS-IS는 자체 `CardColor`를 **1,478회** 쓴다(`CEntity_Base.cs:381`)
- **`Vector3`** — `PreferredFrame()`이 `localPosition`으로 슬롯을 정렬하지만, 이는 **배치 결정**이다.
  좌표가 전부 0이면 정렬이 리스트 순서로 퇴화하나 **결정적**이고, E-01로 슬롯이 넉넉하므로 무해하다
- **`Transform` 좌표계** — `CardController.cs:1588-1612`가 좌표를 ±5로 비교하지만
  그 결과는 `MovePermanent(...)` **연출**만 가른다. 상태 변경 없음
- **어트리뷰트류** — `SerializeField` `Header` `HideInInspector` `CreateAssetMenu` `RequireComponent`
  `ExecuteInEditMode` `RuntimeInitializeOnLoadMethod` `TextArea` `DefaultExecutionOrder` `PunRPC`.
  선언만 있으면 된다

### 룰층 사용 0 — 표현층 전용 54개

```
Animator  AutoLayout3D  BaseEventData  Button  Canvas  CanvasGroup  CinemachineImpulseSource
Coffee  CreateAssetMenu(Attribute)  DefaultExecutionOrder(Attribute)  Dropdown
ExecuteInEditMode(Attribute)  Font  HeaderAttribute  HideInInspectorAttribute  ILayoutGroup
IPointerClickHandler  IPointerEnterHandler  IPointerExitHandler  InputField  JetBrains
LineRenderer  MeshCollider  MeshRenderer  PointerEventData  PunRPCAttribute  RectTransform
RequireComponent(Attribute)  Rigidbody  RuntimeAnimatorController
RuntimeInitializeOnLoadMethod(Attribute)  ScriptableObject  ScrollRect  SerializeFieldAttribute
Slider  TMP_FontAsset  TMP_InputField  TMP_Text  TextArea(Attribute)  TextAsset  TextMeshPro
Texture2D  Toggle  UIBehaviour  UIShiny  UnityEvent  WebP  WebSocketSharp
```

### 룰층 사용 있으나 조건 0 — A

`Header` 26 · `HideInInspector` 15 · `Text` 14 · `Image` 14 · `Vector3` 12 · `UnityAction`(3종) 9 ·
`Sprite` 9 · `PunRPC` 7 · `ExitGames` 7 · `Material` 5 · `SerializeField` 4 · `Rect` 3 ·
`TextMeshProUGUI` 2 · `TMPro` 2 · `Unity` 3 · `UnityEditor` 1 · `Camera` · `AudioClip` · `AudioSource` ·
`Quaternion` · `ParticleSystem` · `SpriteRenderer` · `DG` · `Cinemachine` · `Shapes2D` · `RoomInfo`

> **watch — `Image`**: `Player.cs:29`가 `Frame_Select = ...GetChild(1).GetComponent<Image>()`로 받는다.
> 현재 조건절 사용은 확인되지 않았으나, `GetComponent<Image>()`가 null을 반환할 때
> 이후 역참조가 있는지 단계 1.4에서 확인할 것.

### 네임스페이스 16 — 전부 A (빈 네임스페이스 + 필요한 타입만)

```
UnityEngine.UI  UnityEngine.Events  UnityEngine.EventSystems  UnityEngine.SceneManagement
UnityEngine.Networking  UnityEngine.TextCore  UnityEngine.UIElements  UnityEngine.Rendering
UnityEngine.Pool  UnityEngine.XR  UnityEngine.Analytics  UnityEngine.ParticleSystem
Photon.Realtime  Photon.Pun.Demo  System.Runtime.InteropServices.WindowsRuntime
HeadlessDCGO.Engine.Assets
```

> `HeadlessDCGO.Engine.Assets` 는 **old-TO-BE 잔재**다. substrate 파일 일부가 아직
> 손포팅 네임스페이스를 참조하고 있다 — 단계 0.3(substrate 삼중 분류)에서 정리된다.

---

## C · 범위 밖

`.cs` 파일 단위 판정이며 심볼 단위가 아니다. 오류가 난 156파일 중
로비·덱에디터 계열(`EditDeck` `RoomManager` `DetailCard_DeckEditor` `Opening` `SelectBattleDeck`
`DeckInfoPanel` `FilterCardList` `CardPrefab_CreateDeck` `GameplayOption` 등)이 후보다.

**껍데기 심으로 컴파일만 통과시키고 `docs/out_of_scope.md`에 등재**한다(07-29 확정).
파일별로 왜 범위 밖인지와 게임 룰이 참조하는지를 함께 기록한다.

---

## 단계 1.4 작업 순서

1. **최소 객체·컴포넌트 모델** — `GameObject` + `Transform` + 컴포넌트 레지스트리 (B 2개 해소)
2. **씬 계층 합성** — `BattleAreaFrameParent`/`BreedingAreaFrameParent` 프레임 트리,
   슬롯 충분히 크게 (E-01)
3. **A 84개 빈 껍데기** — 기계적. 심 헤더에 *"선언뿐, 동작 없음"* 명시
4. **네임스페이스 16개**
5. `commandText.activeSelf` 대기 24곳 → 단계 2.5 `IChoiceProvider`로 이관 (여기서는 통과만)
