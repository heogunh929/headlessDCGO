# R2-C(비용 파이프라인 재하우징) 사전조사 (2026-07-16)

Base: b1-registry-integration. 조사 전용. 상세 근거=조사 에이전트 보고 원문(세션 기록) — 본 문서는 판정·계획 요지.

## 핵심 판정
**R2-C는 행동 차단이 아니라 구조 재하우징 골.** 비용 계산·지불·감소·CannotReduceCost·would-be-played(BeforePayCost) 창은 **이미 live로 동작** — EX8_074의 코스트 감소도 실동작(발명 substrate 경유: SuspendCostReductionEffect→registry 바인딩→ResolvePlayCost fold, G9-010/005/006 green). 카드 STOP 주석의 실체는 "AS-IS 버킷+ChangeCostClass 구조가 아닌 혼용(발명) 상태" 서술. 종점 결핍=최상위 오케스트레이터(`CardSource.PayingCost`/`GetPayingCostWithBaseCost`/`CostList`)가 미러 부재, Headless 4곳(ContinuousModifierGate.ResolvePlayCost 등)에 분산.

## AS-IS 지도 요지
PayingCost(CardSource.cs:635)→baseCost(진화=CostList.Min :617)→특수감소(DigiXros :670/Assembly :705, CanReduceCost 게이트 선행)→ChangeCostClass 2-그룹 fold(:775/:864 — 그룹별 NotIsUpDown→IsUpDown 순서)→Math.Max(0). CannotReduceCost veto=Player.cs:1330. BeforePayCost 컷인=CardController:604/:707(지불 전 발화→`UntilCalculateFixedCostEffect.Add`). 지불=:806-826(GetPayingCostWithBaseCost **2회 호출** verbatim)→AddMemory(-Cost) :970→**플레이당 버킷 소거 :961**+턴종료 리셋 TSM:3179.

## 미러 현황
- 1:1 완료: ChangeCostClass/CannotReduceCostClass kind-class·Player.CanReduceCost(:392)·버킷 정의+EffectList fold·`FoldPlayCost`(2-그룹 2-패스 1:1)·ChangePlayCostStaticEffect. BeforePayCost 창=이미 개방(PlayCardAction:107-149·DigivolveAction:196-227). 버킷 write-read 왕복=R5-A PlayForCost가 실증.
- 발명 substrate(R2-C 타깃): ContinuousModifierGate.ResolvePlayCost/ResolveDigivolutionCost(+registry NumericModifier fold union)·CostReductionImmune registry 3키(ReplacementHelpers:264-270 — live CanReduceCost와 평행 표현)·호출부 4곳(PlayCardAction:473/DigivolveAction/CardEffectCommons:1917/:3591)·DigiXros/Assembly 감소 분산·**플레이당 Player-버킷 소거 부재**(registry만 ExpireFixedCostCalc — 버킷형 1회성은 턴 내 누수 소지).

## 갭·의존
- EX8_074 혼용 청산 최소집합: ①#1을 버킷+ChangeCostClass AS-IS형 재작성(신 인프라 불요) ②플레이당 버킷 소거 추가(:961 미러) — #5/#6은 R5 스텁 의존(비용 무관 별건).
- **W3c 잔여와 강결합**: registry-key cost immunity 3키가 마지막 평행 표현 — R2-C에서 ResolvePlayCost 폐지+PayingCost 수렴 시 함께 flip(한 골 권장).
- R4 접점 최소(선행 불요). RD-R5-01 버스트 비용엔진=R2-C 후 자연 수용 지점(하류 약결합).

## 배치·리뷰 지점
배치: ①PayingCost/GetPayingCostWithBaseCost/CostList 정위치 신설(특수감소·FixedCost·evo-Min 흡수, FoldPlayCost 내부화)→②pay 경로 4곳 재배선+플레이당 버킷 clear→③registry-key immunity flip(union 소멸) — ①②③ 한 골(강결합), ④EX8_074 혼용 청산=후속. **행동 불변 제약 리팩터**(기존 비용 스위트 13종이 조밀 회귀 게이트).
적대리뷰 렌즈: 2회-호출 verbatim(순수함수 등가)·2-패스 순서·**FoldPlayCost의 Root.None 전달=잠재 fidelity 갭**(AS-IS는 실root — root-의존 비용효과 시 발산, 재하우징 시 스레딩 교정)·버킷/registry 소거 원자성·CannotReduceCost 5장 과소면역 회귀.
계기판: 발명 참조 6+ → 0, pay 진입=CardSource.PayingCost 단일.
witness: AS-IS 소비 규모=ChangeCostClass 230장·버킷 광의 191장·ChangeDigivolutionCost 49장·CannotReduceCost 5장(대부분 기포팅 — 재하우징=재포팅 아님).

## §F. R2-C 골 착지 + 적대리뷰 GO (2026-07-16)
커밋 2(09bb3387 ①②·c3cb0819 ③, c1-cost-integration). pay 진입=CardSource.GetPayingCostWithBaseCost 완전 단일(9 pay-site 재배선 — 조사 4곳+발견 5곳), Root 스레딩 갭 교정(실소비 확인), 버킷 원자 소거(:961 미러), registry-key 면역 3키 flip(PRIM-P0.CannotReduceCost red→green). union 유지 판정=legacy NumericModifier 생산자 잔존(정직). STOP 2=RD-R2C-ASSEMBLY(비발화 실증)·RD-P6C1-2(CostList 비-live). 적대리뷰 6렌즈 전부 확인됨(2회-호출 순수성·fold 순서·root 등가·소거 원자성·과소면역 무회귀[실카드 5장=양쪽 다 미포팅 스켈레톤]·DigiXros 게이트 선행). **P2 원장 5**: root-조건 효과 미-witness(현 카드셋 무해=future fidelity 개선으로 명시)·canReduceCost knob 폐기(죽은 파라미터)·ExpireFixedCostCalc payer=actor vs owner 엣지·죽은 상수/매핑 잔존(cleanup 후속)·GetCostItself 발산(후속 배선 자연 지점). 스위트 329/107/436(신규 FAIL 0·red 1 해소).
