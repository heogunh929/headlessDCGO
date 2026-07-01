# PRIM-W5 — 커버리지 래퍼 웨이브 (트리거/인터랙션 액션)

> **목표:** 로컬모델 config-only 커버리지를 **~7% → 60%+**(스트레치 76%)로. 방법 = 원본 카드가 실제로 부르는 **상위 빈도 액션 토큰**을 카드-facing 팩토리/래퍼로 노출(원본 심볼명 미러). 프리미티브 선행개발을 **트리거·인터랙션 계층으로 확장.**
>
> **근거(전수 census):** 3918 카드를 vanilla(6.7%)/triggered(46.7%)/interactive(40.9%)/special(5.7%)로 분류 → 액션 토큰 클러스터링. triggered↔interactive가 토큰을 공유해 한 래퍼가 양쪽 커버. **통합 누적 커버리지: 상위 52토큰→60%, 80→71%, 100→76%.**
>
> **종료 기준(각 항목):** `bash scripts/run-tests.sh` green + 격리/픽스처 테스트 + RuleAudit 0. AS-IS 미러. 기존 메커니즘 재사용(대부분 "엔진 있음/카드-facing 이름 없음"). 없는 메커니즘만 신설.

## 커버리지 곡선 (전체 3918 대비)
| 상위 K 토큰 | 커버 | 신규 래퍼(누적) |
|---|---|---|
| 40 | 53.8% | ~24 |
| **52** | **60.0%** | **~32** |
| 80 | 71.3% | ~50 |
| 100 | 76.5% | ~65 |

## 백로그 (카드수 내림차순 · 상태) — 신규만
> HAVE(카탈로그 있음)/PLUMB(UI·컨텍스트, 대개 no-op 확인)는 제외. 아래가 실제 작업.

| # | 카드 | 토큰(원본 심볼) | 헤드리스 대응 / 방법 |
|---|---|---|---|
| 1 | **1106** | `AddSelfDigivolutionRequirementStaticEffect` | W1 `AddDigivolutionRequirement`의 self 변형(진화 도착지 요건) |
| 2 | 723 | `PlayPermanentCards` (+SELECT) | select-and-play (존→플레이). ActivatedPlayFromUnder/PlayCardEffect 조합 |
| 3 | 451 | `ChangeDigimonDP` | 타겟 DP 수정자(활성/지속) |
| 4 | 444 | `DrawClass` | `DrawEffect` 노출 |
| 5 | 350 | `SimplifiedSelectCardConditionClass` | W2 `SelectCardConditionClass` 별칭 |
| 6 | 343 | `SimplifiedRevealDeckTopCardsAndSelect` | `SimplifiedRevealAndSelectEffect` 래퍼 |
| 7 | 300 | `DigivolveIntoHandOrTrashCard` | 진화원→손/트래시(ReturnDigivolutionCards 존재) |
| 8 | 282 | `SuspendPermanentsClass` | 타겟 suspend |
| 9 | 269 | `DeletePeremanentAndProcessAccordingToResult` | `DestroyPermanentsEffect` 래퍼 |
| 10 | 216 | `ChangeCostClass` | `ChangePlayCost` 노출 |
| 11 | 142 | `IgnoreColorConditionClass` | W3 `UseRequirements` 별칭 |
| 12 | 122 | `ChangeDigimonSAttack` | 타겟 SA 수정자 |
| 13 | 121 | `AddThisCardToHand` | self ReturnToHand |
| 14 | 94 | `TrashDigivolutionCardsFromTopOrBottom` | 진화원 트래시(존재) |
| 15 | 89 | `DestroyPermanentsClass` | W2 `DestroyPermanentsEffect` 별칭 |
| 16 | 71 | `BlastDigivolveEffect` | 특수진화(FreeDigivolve) 노출 |
| 17 | 70 | `AddSelfLinkConditionStaticEffect` | 링크 요건 |
| 18 | 64/39/39/35 | `GainBlocker/GainPierce/GainCanNotAttack/GainRush` | 기존 키워드/제약 별칭 |
| 19 | 49 | `ChangeCardNamesClass` | 카드명 부여(메타) |
| 20 | 47 | `SelectTrashDigivolutionCards` | W2 `ActivatedSelectTrashDigivolutionEffect` 별칭 |
| 21 | 41 | `AddSkillClass` | 스킬/효과 부여 |
| 22 | 36 | `CanNotAffectedClass` | 효과 면역(replacement) |
| 23 | 34 | `PlayOptionCards` | 옵션 플레이 |
| 24 | 31 | `CanNotSuspendClass` | W4 `CantSuspendStaticEffect` 별칭 |
| 25 | 29/29/27 | `AddEffectToPlayer`/`RevealDeckTopCardsAndProcessForAll`/`PlayCardClass` | 플레이어 효과/공개/플레이 |

## PLUMB 확인 대상 (래퍼 불요 가능성 높음)
`CardEffectHashtable`(386)·`AddActivateMainOptionSecurityEffect`(185)·`PlaceDelayOptionCards`(146)·`ShowReducedCost`(121)·`ActivateClassesForSharedEffects`(78) — UI/컨텍스트/공유효과 플러밍. 각 probe해서 no-op/기존처리면 제외, 아니면 최소 배선.

## 진행
- [x] **W5-0 질의 뷰 계층 (근본 enabler)** — 드라이런에서 발견: 진짜 병목은 액션 래퍼가 아니라 카드 술어가 읽는 `permanent.*`/`TopCard.*`/`cardSource.*` 멤버 부재. 헤드리스 `Permanent`={Instance,Owner}뿐이라 술어-보유 카드는 컴파일조차 불가. → `CardSource`에 뷰 멤버 14종(IsDigimon/IsTamer/IsToken/Level/HasLevel/IsLevel/CardColors/HasCardColor/CardNames/EqualsCardName/ContainsCardName/CardTraits/EqualsTraits/ContainsTraits) + `Permanent`에 DP(연속수정자 접힘)/TopCard(→CardSource 재사용)/Level/IsDigimon/IsTamer/IsToken/IsSuspended/DigivolutionCards/HasNoDigivolutionCards. 엔진 상태 기반 **평가 가능**. **G9-043**(술어 `p.DP==0 && p.TopCard.ContainsCardName(...)` 실평가 포함). 275 green.
  - 잔여 롱테일 멤버(GetCostItself·CanAttack·CanNotBeAffected·HasCSTraits·HasText·PermanentFrame 등)는 실제 카드 포팅 시 수요 맞춰 확장.
- [ ] W5-a 얇은 별칭(HAVE 재사용: IgnoreColor→UseRequirements, GainX→키워드, CanNotSuspend, SelectTrashDigivolution, DestroyPermanents, SimplifiedSelectCardCondition) — 저비용 일괄
- [ ] W5-b 노출 래퍼(Draw/ChangeCost/DestroyPermanents/RevealAndSelect + AddSelfDigivolutionRequirement)
- [ ] W5-c 타겟 수정자(ChangeDigimonDP/SAttack, Suspend/Delete 타겟)
- [ ] W5-d 인터랙션 코어(PlayPermanentCards + SELECT) — 진짜 신규
- [ ] W5-e 나머지(Link조건/Skill/CardName/CanNotAffected/PlayOption 등)
- 완료 → 카탈로그 재생성(원본 심볼 키 + honored/ignored 주석 + STOP-목록) + 스킬 범위 재조정.
