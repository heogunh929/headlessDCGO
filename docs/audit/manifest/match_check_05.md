# AS-IS↔TO-BE 매칭 검증 — part 5/13 (both_part_05.txt)

대상 5장: `Script/ICardEffect.cs`, `Script/Effects.cs`, `Script/SelectDigiXrosClass.cs`,
`Script/SelectCardEffect.cs`, `Script/SelectPermanentEffect.cs`.
AS-IS = `DCGO/Assets/Scripts/<relpath>`, TO-BE = `src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`.
전 파일 전문 실독(양측) 완료.

---

## 1. Script/ICardEffect.cs — 정상 (경미한 발견 1건)

AS-IS 1291줄 / TO-BE 1435줄. `ICardEffect` 추상클래스 전 필드/프로퍼티/메서드(SetUpICardEffect,
EffectSourceCard/EffectSourcePermanent, MaxCountPerTurn, CanTrigger/CanActivate/CanUse,
IsOptional 계열 bool 9종, IsDisabled/IsOnPlay/IsWhenDigivolving/IsOnDeletion/IsOnAttack,
IsSameEffect, `ActivateICardEffect`/`ActivateICardEffectExtensionClass`), `CalculateOrder` enum,
`EffectTiming` enum(65개 값) 전건 대조 — 이름/순서/시그니처 1:1 보존. Unity 전용 부분
(Debug.Log, PlayLog, GManager UI 컴포넌트 호출)은 각 AS-IS 줄번호를 인용한 주석으로 스트립 근거
명시. `PermanentOfThisCard()`(AS-IS `Permanent`) → `ResolvePermanentOfThisCard`(PermanentView
브릿지) 치환도 AS-IS 각 호출부(:390,:408,:434-444,:810)에 대응 근거 제시. 검증한 범위에서 실질
로직 손실·왜곡 없음.

**발견 1 (경미)**: TO-BE `EffectTiming` enum에 `WhenDigivolving` 멤버가 존재하나(파일 첫 그룹,
None/OnEnterFieldAnyone/OnDetermineDoSecurityCheck/OnUseAttack 다음), AS-IS `EffectTiming` enum
(ICardEffect.cs:969-1032, 60개 값 전수 확인)에는 이 이름의 멤버가 없음. AS-IS에 존재하는
"When Digivolving" 관련 개념은 (a) `ICardEffect.IsWhenDigivolving` 프로퍼티
(`EffectDiscription.StartsWith("[When Digivolving]")` 체크, 이 파일 :772-794, TO-BE에도 정확히
1:1 이식됨)와 (b) `CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card)` 헬퍼(카드
효과부 조건 함수, 별도 파일)뿐 — 어느 쪽도 `EffectTiming` enum 멤버가 아님. 이후 추가된 값들
(batch 2/3a/3b/4, F1-M0-1, F1-DEAD)은 전부 AS-IS 원본 열거값에 대응하는 근거를 주석으로
제시하는데, `WhenDigivolving`만 그런 근거 없이 최초 블록에 섞여 있어 AS-IS에 대응 근거가 없는
발명 심볼로 판단. (동일 EffectTiming enum 소재 위치이므로 이 파일 소관.)

---

## 2. Script/Effects.cs — 정상 (스텁이 타당함; 헤더 문구만 오도적)

AS-IS 2306줄 전문 실독 완료 — `Effects : MonoBehaviour`. 전 리전(手札のカードを使用する,
場のポケモンのスキル発動エフェクト, トラッシュ/処理領域/手札カード効果発動エフェクト,
フィールドキャラカード生成/進化/バウンス/デッキバウンスエフェクト, カード公開1/2,
カードドロー/表示エフェクト, バフ/デバフ/デジクロス選択/Assembly/凍結/回復/セキュリティ
ブレイク/パーマネント破壊/セキュリティ登場・破棄/バトル/進化元離脱/プレイ失敗エフェクト)이
100% Unity 프레젠테이션(Instantiate, DOTween Sequence, `ContinuousController.instance.PlaySE`,
Transform 애니메이션, `WaitForSeconds`/`WaitUntil` 코루틴 타이밍)이며, 게임 상태를 실제로
변경하는 결정론적 규칙 로직은 전무(단, `permanent.ShowingPermanentCard.IsEffectPlaying` 같은
표시용 플래그만 다룸 — 이 플래그 자체가 Unity 표시 오브젝트 소속).

기존 마이그레이션 결정("Headless/=substrate만, 게임 로직은 미러 레이어") 및 실제 관찰(AS-IS
전문에 규칙 로직 0)에 근거해 TO-BE가 7줄 스켈레톤 스텁(`// TODO: Skeleton only. Port or
implement deterministic .NET logic later.`)으로 남아있는 것은 **결함이 아님** — 포팅할
결정론적 로직이 AS-IS에 애초에 없음.

**발견 2 (경미/문서 정합성)**: 스텁 헤더가 `Decision: PORT` / `Category: UnityMixedLogic` /
`TODO: ... port ... later`로 표기되어 있어 "포팅 미완료"처럼 보이나, 실측 결과 포팅할 대상이
없으므로 헤더 문구가 상태를 오도함. 기능적 결함은 아니고 헤더 정확도 문제.

---

## 3. Script/SelectDigiXrosClass.cs — 정상 (실질 동작 편차 1건 발견)

AS-IS 1048줄 / TO-BE 1070줄. `selectedDigicrossCards`/`addDigivolutionCardInfos`/
`excludedCards`/`playCard` 상태 표면, `maxTrashCount`/`maxTamerDigivolutionCardsCount`,
`isHandCard`/`isBattleAreaCard`/`isTrashCard`/`isSecurityCard`/`isTamerDigivolutionCard`,
`CanSelectDigiXros`, `Select`(영역별 선택 루프 전체, "count==1 skip/continue", "count==2
자동선택", 그 외 ModeChoice 패널), `SelectHandCard`/`SelectBattleAreaPermanent`/
`SelectTamerDigivolutionCard`/`SelectTrashCard`, `AddDigivolutiuonCards`/
`AddDigivolutiuonCardsByEffect`, `AddDigivolutionCardsInfo` 홀더까지 전건 대조 — AS-IS 줄번호
인용 주석과 함께 로직/순서/이름 1:1. `PermanentOfThisCard()` → `ResolvePermanentOfThisCard`
치환, Photon RPC → `ChoiceType.ModeChoice` 치환 등 substrate 치환 근거도 AS-IS 대응줄 인용.

**발견 3 (실질, 중간)**: AS-IS `EndSelectDigiXros()`는 `yield return null;`을 포함한
`IEnumerator` 코루틴 메서드(:874-878)로, C# 이터레이터 시맨틱상 `StartCoroutine`/명시적
`MoveNext` 없이 단순 호출(`EndSelectDigiXros();`)만 하면 본문이 전혀 실행되지 않음(상태
머신 객체만 생성되고 버려짐) — 즉 AS-IS의 4개 호출부(`SelectHandCard`/
`SelectBattleAreaPermanent`/`SelectTamerDigivolutionCard`/`SelectTrashCard` 내부
`AfterSelectCardCoroutine`/`AfterSelectPermanentCoroutine`, 각 :622,:680,:742,:797,:862)에서
호출되는 `EndSelectDigiXros();`는 실질적으로 no-op이며 `_endSelectDigiXros`는 이 경로로는
결코 true가 되지 않음(AS-IS의 실제 관찰된 동작).
TO-BE는 `EndSelectDigiXros()`를 `await` 없는 일반 동기 `Task` 반환 메서드로 이식했고
(`Task EndSelectDigiXros() { _endSelectDigiXros = true; return Task.CompletedTask; }`), 4개
호출부에서 `_ = EndSelectDigiXros();`로 호출 — 이 경우 메서드 본문이 즉시(호출 시점에)
동기 실행되어 `_endSelectDigiXros = true`가 실제로 세팅됨. 즉 IEnumerator→Task 치환이
AS-IS의 "호출은 되지만 지연 실행이라 사실상 미실행"이라는 실제 동작을 보존하지 못하고,
TO-BE에서는 매번 실제로 실행되는 동작으로 바뀌었음 — 4개 호출부 모두에서 AS-IS 대비 상태
변화가 추가된 실질적 동작 편차.

---

## 4. Script/SelectCardEffect.cs — 정상 (경미한 발견 1건)

AS-IS 1025줄 / TO-BE 936줄(신규 `BuildRequest`/`BuildMutations`/`Apply` 헬퍼 레이어 추가,
UI/Photon 스트립으로 순감소). `SetUp`(16-param, AS-IS 필드 리셋 목록 1:1), `Mode`/`Root` enum,
`RootCardList`, `CanSelectCard`(→`CanSelectCardAsIs`), `active()`, `Activate()` 전체(가드,
선택 트랜스포트 → `ChoiceProvider`/`RunAsIsSelectionAsync`, Mode별 라우팅 AddHand/Discard/
PlayForFree/PlayForCost/Custom, AddHandCards 배치, after-코루틴) 대조. AS-IS 특이 동작
(`Mode.Discard`의 "하나라도 손패 카드면 `_targetCards` 전체를 한 번에 discard, 이후
반복에서는 트래시 폴백 무시" 버그성 동작, `Mode.PlayForCost`가 `root: Root.Hand`로 고정
플레이하는 AS-IS 특이점 등)이 주석과 함께 의도적으로 보존됨을 확인.

**발견 4 (경미/문서 정합성)**: `SetReducedCostTuple`/`SetFixedCostTuple`의 XML 주석이
"A NON-null tuple reaching Mode.PlayForCost STOPs (design item RD-W4-1 — ... has no mirror
surface yet)"라고 명시하나, 실제 `Activate()`의 `Mode.PlayForCost` 분기(:646-729)는
`ChangeCostClass`를 생성해 `selectPlayer.UntilCalculateFixedCostEffect`에 등록/해제하는
로직을 온전히 구현하고 있어 STOP/throw가 전혀 없음 — 주석이 실제 구현 상태와 불일치(과거
STOP 시점의 낡은 주석으로 추정). 기능 결함 아님, 문서 정확도 문제.

---

## 5. Script/SelectPermanentEffect.cs — 정상

AS-IS 1055줄 / TO-BE 821줄(UI/Photon 스트립 + `BuildRequest`/`BuildMutations` 신규 레이어로
순감소). `Mode` enum, `SetUp`(11-param, AS-IS 필드 리셋 목록 1:1 — `_canAttackPlayer`/
`_defenderCondition`/`_isFaceUp`가 AS-IS처럼 리셋되지 않는 특이 동작까지 보존),
`CanTarget`(→`CanTargetAsIs`, `CanSelectBySkill` 게이트 소유자 조건까지 1:1), `active()`
(forced-selection 카운트 조건, `ParameterComparer.Enumerate` → 로컬 조합 열거 치환),
`CanEndSelect`, `Activate()` 전체(강제선택 단축 경로, 선택 트랜스포트, Mode별 배치 — Tap/
UnTap/Destroy/Bounce/PutLibrary*/PutSecurity*/Degenerate/Attack, after-코루틴) 대조. AS-IS의
`Mode.PutSecurity*`가 `Owner.CanAddSecurity(_cardEffect)`로만 게이팅되는 조건, `Mode.Attack`이
순차 처리되는 특이 동작 모두 주석에 AS-IS 줄번호 인용과 함께 보존 근거 제시. 실질 문제 미발견.

---

## 요약

| 파일 | 판정 | 문제 건수 |
|---|---|---|
| ICardEffect.cs | 정상 | 1 (경미 — 발명 enum 멤버 `WhenDigivolving`) |
| Effects.cs | 정상(스텁 타당) | 1 (경미 — 헤더 문구 오도) |
| SelectDigiXrosClass.cs | 정상 | 1 (중간 — `EndSelectDigiXros` 지연실행→즉시실행 동작 편차, 4개 호출부) |
| SelectCardEffect.cs | 정상 | 1 (경미 — 낡은 STOP 주석) |
| SelectPermanentEffect.cs | 정상 | 0 |

전 5장 모두 대분류 판정은 "정상"(치명적 매칭 실패·소실 없음). 발견 4건 중 3건은 경미(문서/
enum 위생), 1건(SelectDigiXrosClass.cs의 `EndSelectDigiXros` 지연실행 편차)은 4개 호출부에
걸친 실질 동작 변화로 중간 등급 — AS-IS의 원래(버그성) no-op이 TO-BE에서 실제 상태변경으로
바뀌는 지점이라 재현 시나리오에 따라 관찰 가능한 차이를 낼 수 있음.
