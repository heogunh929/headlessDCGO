# 렌즈-없는 BOTH 재감사 취합 — 2026-07-24 (HEAD e5ea69d7)

344쌍 전건, Sonnet 13기가 **판정 카테고리 없이 자체 판단**으로 검증. 코디네이터(나)가 재분류하지 않고 각 기체 판단을 그대로 취합.
파트별 상세=`match_check_00.md`~`match_check_12.md`.

## 배경
직전 categorized 감사(verdicts_script_both_ALL.csv)는 코디네이터가 판정 카테고리(MATCH/DEVIATION/BLOCKED)와
무죄 출구("substrate라 정당"·"방언"·"AI전용"·"부분포팅 확인")를 프롬프트에 심어 발사했음. 사용자 지적:
"네가 임의로 렌즈를 씌워 결함을 못 보게 만든다." 렌즈 제거 후 재감사 결과, **categorized 감사가 무결/MATCH로 통과시킨 파일들에서
행동 결함·로직 불일치가 다수 발견됨** — 사용자 우려 실증.

---

## A. 행동 결함·로직 불일치 (categorized 감사가 놓친 것 다수)

| # | 파일 | 발견(기체 판단) | categorized 감사의 처리 |
|---|---|---|---|
| A1 | `CardSource.cs` | **`HasCardColor` 듀얼컬러 union 드롭 (LIVE·최고심각)**: AS-IS 무플래그는 `CardColors∪DualCardColors`, TO-BE는 `CardColors`만. ~590 호출부 다수 영향. 듀얼컬러 카드가 색판정 탈락 | DEVIATION이나 "~90 게터"만 지적, 이 union 회귀 **누락** |
| A2 | `PermanentEffectFactory.cs` | **CollisionEffect 면역게이트 오삭제**: `!TopCard.CanNotBeAffected` 제거. 주석 "전 self-grant" 전제가 거짓(EX8_070·BT21_077·EX11_063·EX10_032·EX10_008은 타 퍼머넌트 부여) | 파트9 **DEVIATION 0(무결)** 통과 |
| A3 | `KeyWordEffects/Decode.cs` | **CanEnterField 게이트 누락**: 저수준 `PlayPermanentCards(sourceCard:)` 호출로 ICanNotPutFieldEffect 스캔 미검사. Partition/Blast는 올바른 오버로드 | 파트9 DEVIATION 0 통과 |
| A4 | `SelectDigiXrosClass.cs` | **no-op→실행 행동변경**: AS-IS `EndSelectDigiXros()` IEnumerator가 StartCoroutine 없이 불려 미실행(버그), TO-BE 동기 Task가 실제 실행. 4 호출부 | MATCH-TRANSLATED 통과 |
| A5 | `KeyWordEffects/MindLink.cs` | **`!IsFlipped` 협착 소실**: `Count(IsTamer&&!IsFlipped)`→`Count(IsTamer)`. IsFlipped는 타처 live 플래그(:1180) | MATCH-TRANSLATED, 주석 "flip 미모델링" 그대로 무죄 |
| A6 | `TrashDigivolutionCards.cs` | **선택집합 협착**: 소스없는 host 배제·host pick 후 0-trash 금지(vs AS-IS canNoSelect). "verbatim" 주석과 모순 | MATCH-TRANSLATED(재하우징 확인) 통과 |
| A7 | `CardController.cs` IUnsuspendPermanents | **언탭 도메인 확대(P1)**: 컷인 후 재필터를 1차생존자 대신 프리필터 전체(`_permanents`)로 | 재하우징 무결 판정 |
| A8 | `CardController.cs` IRecovery | **배선 결함(P5)**: `SecurityRuleGateSeam.CanAddSecurity`(=>true 스텁) 호출로 "시큐리티 추가불가" 제약 무시. 충실 미러 `Player.CanAddSecurity`가 한 호출 거리 | 재하우징 무결 판정 |
| A9 | `CardController.cs` 바운스3종 | **pre-move 취소창 누락(P2)**: would-return/would-remove-field 컷인 창 부재(Destroy엔 있음). BT5_086 prevent-removal 무력화 | "sink 라우트로 폴드, 확인" 통과 |
| A10 | `CardController.cs` IPutSecurityPermanent | **면역·DigiEgg분기·토큰게이트 누락(P3)**: happy-path만 재구현 | 재하우징 무결 판정 |
| A11 | `CardController.cs` ISecurityCheck | **해결순서 스왑·다중[Security] select루프 미재현(P4)** | SecurityResolver 재하우징 무결 판정 |
| A12 | `GiveEffectToPlayer/ChangePlayCost.cs` | **면역검사 대상 오류(#18)**: `CanNotBeAffected(changeCostClass)`(신규) vs AS-IS `(activateClass)`(원 효과). SkillCondition이 타입 검사 | MATCH-1:1 통과 |
| A13 | `Permanent.cs` | **`Level` 오버라이드 드롭**: AS-IS `if(!TopCard.HasLevel)Level=1145140` 강제폐기를 미이식, folded 값 유지 가능 | "AS-IS 분기 dead라 非결함" 통과 |
| A14 | `Permanent.cs` | **`cardSources` 순서 불일치**: 링크카드 항상 맨뒤, 주석("AddLinkCard appends") 사실무근 — AS-IS는 index1 interleaved | 미지적 |
| A15 | `GameRandom.cs` | **결정론 계약 파손**: AS-IS `NextUInt32`(상위32) vs TO-BE `NextUInt64`(전체64), Range 폭·Probability 소실·seed long→int | "raw-draw 폭 substrate 허용" 무죄 |

## B. 미포팅 로직 갭 (AS-IS 실 로직 존재·TO-BE 스텁/부재)

- **덱 코덱 계열(CoreRule/HIGH)**: `DeckData.cs`(base-256 코덱)·`DeckBuildingRule.cs`(합법성·밴리스트)·`DeckCodeUtility.cs`·`CreateNewDeckButton.cs`·`ShuffleDeckCode.cs`
- **결정론 로직**: `Combinations.cs`(색/이름 조합, 소비 10+)·`ConvertBinaryNumber.cs`(진법)·`SpellRestoration.cs`·`JsonSerializedClass.cs`(스키마 재발명)·`AutomaticOrder/StartTurnTamerMemory.cs`
- **CardObjectController**: `AddLibraryTopCards`/`AddLibraryBottomCards`(호출 50)·`MovePermanent`(10) 정본구현 부재
- **ContinuousController**: `RandomUtility.IsSucceedProbability` 미러 부재
- **Permanent 필드**: `HandBounceEffect`·`LibraryBounceEffect`·`DPWhenSuspended`(write-only)·`DigivolutionOrLinkCards`
- **CardSource 술어**: `HasInheritedEffect`·`HasUseCost`·~50 trait 술어·~10 keyword flag
- **효과 계통**: ChangeSAttack `Invert*` 3종·`OwnerHas1OrLessTamers`·`OptionSecurityEffect`·`Progress.GainProgress`

## C. 발명 (AS-IS 무대응)

- `ICardEffect.EffectTiming.WhenDigivolving`(발명 심볼 — categorized는 "확립된 방언"으로 무죄)
- `CardController.PlayPermanentClass` WhenDigivolving 2번째 창(DISPATCH-REMAP BRIDGE)
- `SpecialPlayRecipeRegistry`(BurstDigivolve 등 등록 — 레지스트리 청산 규약 대조 요망)
- `SelectCardEffect`/`SelectPermanentEffect` 사문 레거시 API 블록(id-flip 잔존)

## D. 결정적 관찰

**categorized 감사 vs 렌즈-없는 감사가 서로 다른 결함을 잡음:**
- 렌즈-없는 감사가 A1~A15의 행동/로직 결함 다수 신규 발견(특히 파트0 CardController·파트9는 categorized가 "무결/DEVIATION 0"이라 했으나 각 6·2건 발견)
- 반대로 categorized 감사만 잡은 것도 있음(CardEffectCommons `DigivolveIntoHandOrTrashCard` 코스트필터 — 렌즈-없는 파트3는 심볼존재만 확인, 이 로직 미검)
- **결론: 어느 단일 감사도 완전하지 않음. 두 결과 합집합이 현재 결함 하한선.**
