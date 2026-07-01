# PRIM-W3 — 중빈도 프리미티브 (선행개발 웨이브 3, 사용 6–19)

> **위치:** `primitive_backlog.md`의 W3. 중빈도 키워드·제약·바운스·메모리·시큐리티. W2 완료 후.
> **공통 종료 기준·규율:** W1/W2와 동일(green + 격리 테스트 + RuleAudit 0, AS-IS 미러, probe-first, 중앙 게이트 재구현 금지, 범위 밖 NotSupported). 각 항목 독립 green 게이트.
> **재사용 힌트:** 키워드는 기존 팩토리 관용. `*StaticEffect`(비-self) 변형은 대응 self 버전에 대상 스코프만 확장. 제약류(CanNot*)는 해당 게이트(공격/차단/파괴/서스펜드)에 정적 바인딩.

## 서브goal (사용빈도순)
| 프리미티브 (사용) | 원본 위치 | 계열 |
|---|---|---|
| TrainingEffect (19) | `KeyWordEffects/Training.cs` | 효과 |
| PartitionSelfEffect (18) | `KeyWordEffects/Partition.cs` | 키워드 |
| ScapegoatSelfEffect (17) | `KeyWordEffects/Scapegoat.cs` | 키워드(would-be-deleted) |
| Gain1MemoryTamerOpponentDigimonEffect (17) | `CardEffectFactory` (probe) | 메모리 |
| BlitzSelfEffect (17) | `KeyWordEffects/Blitz.cs` | 키워드 |
| UseRequirements (16) | `CardEffectFactory` (probe) | 사용 요건 |
| ReturnToLibraryBottomDigivolutionCardsClass (15) | `Script/CardController.cs` | 진화원 덱바닥 |
| MaterialSaveEffect (13) | `KeyWordEffects/MaterialSave.cs` | 효과 |
| Gain2MemoryOptionDelayEffect (13) | `CardEffectFactory` (probe) | 메모리(딜레이) |
| IcecladSelfStaticEffect (12) | `KeyWordEffects/Iceclad.cs` | 키워드 |
| DecoySelfEffect (12) | `KeyWordEffects/Decoy.cs` | 키워드(would-be-deleted) |
| MindLinkClass (11) | `CardEffectCommons/KeyWordEffects/MindLink.cs` | 마인드링크 |
| RushStaticEffect (11) | `KeyWordEffects/Rush.cs` | 키워드(비-self) |
| ChangeSAttackStaticEffect (10) | `CardEffectFactory/ChangeSAttack.cs` | 시큐리티어택 ±(대상) |
| GrantedReduceLinkCostClass (9) | `KeyWordEffects/Link.cs` | 링크 코스트 감소 |
| CanNotBeBlockedStaticSelfEffect (9) | `CardEffectFactory/CanNotBeBlocked.cs` | 제약(차단불가) |
| RebootStaticEffect (8) | `KeyWordEffects/Reboot.cs` | 키워드(비-self) |
| ChangeSelfLinkMaxStaticEffect (8) | `CardEffectFactory/ChangeLinkMax.cs` | 링크 최대 |
| ReplaceBottomSecurityWithFaceUpOption(Main)Effect (7+7) | `CardEffectFactory` (probe) | 시큐리티 교체 |
| FragmentSelfEffect (7) | `KeyWordEffects/Fragment.cs` | 키워드(would-be-deleted) |
| ExecuteSelfEffect (7) | `KeyWordEffects/Execute.cs` | 키워드 |
| CantUnsuspendStaticEffect (7) | `CardEffectFactory/CanNotUnsuspend.cs` | 제약 |
| BlastDNADigivolveEffect (7) | `KeyWordEffects/BlastDNADigivolution.cs` | 특수진화 |
| ProgressSelfStaticEffect (6) | `KeyWordEffects/Progress.cs` | 키워드(면역 부여) |
| CanNotBeDestroyedBySkillStaticEffect (6) | `CardEffectFactory/CanNotBeDeletedByEffect.cs` | 제약(스킬파괴불가) |
| CanNotAttackStaticEffect (6) | `CardEffectFactory/CanNotAttack.cs` | 제약(대상) |
| ArtsDigivolveEffect (6) | `KeyWordEffects/ArtsDigivolve.cs` | 특수진화 |

## 진행 (27/27 ✅ 완료, 269 green, RuleAudit 0)
- [x] **키워드 grant 9종**: Blitz·Decode·Progress·Partition(Batch2) + Iceclad·Decoy·Fragment·Execute·Scapegoat(SelfKeywordByNameEffect + 상수). **G9-032**.
- [x] **정적 non-self 3종**: RushStatic·RebootStatic(player-scope 키워드) + CanNotAttackStatic(player-scope 제약). **G9-033**.
- [x] **메모리 2종**: Gain1MemoryTamerOpponentDigimon·Gain2MemoryOptionDelay(신규 TriggeredGainMemoryEffect). **G9-034**.
- [x] **게이트-추가 제약 3종**: CantUnsuspend(언서스펜드 스텝)·CanNotBeBlocked(블록 후보 열거)·CanNotBeDestroyedBySkill(효과-삭제 경로). RestrictionHelpers 키·kind·helper + 각 게이트 consult 추가. **G9-035**(삭제는 end-to-end).
- [x] **재사용/서브시스템-래퍼 6종**: ChangeSAttackStatic(player-scope SA 수정자)·ReturnToLibraryBottom(ReturnDigivolutionCardsKind)·ReplaceBottomSecurity x2(AddToSecurity+ReturnToHand)·Training(신규 TrainKind→TrainAsync)·MaterialSave(신규 MaterialSaveKind→MoveSourcesBottom). **G9-036** end-to-end.
- [x] **subsume 2종**: Arts(FreeDigivolveHelpers Blast/Arts)·BlastDNA(SpecialPlayKind.DnaDigivolve free-recipe). 신규 코드 불요.
- [x] **최종 4종**: MindLink(키워드 grant)·ChangeSelfLinkMax(LinkedMaxDelta 수정자)·GrantedReduceLinkCost(LinkCostDelta 수정자)·UseRequirements(ignore-color 플래그, DigivolveAction consult 배선). **G9-037**.

> **preemptive-seal(grant live, behavior-consumer latent)**: MindLink(tamer-as-Digimon 소비자)·ChangeSelfLinkMax/GrantedReduceLinkCost(링크 서브시스템 소비자 EnforceLinkedMaxAsync/LinkSelfEffect가 registry 미보유 → 별도 마이그레이션). fidelity_debt.md 참조. 나머지는 behavior-live.

## 이전 진행 요약(원안)
- [ ] 키워드류(Partition·Blitz·Iceclad·Decoy·Fragment·Execute·Progress·Rush/Reboot-static)
- [ ] would-be-deleted 대체군(Scapegoat·Decoy·Fragment) — 공통 창(DeletionReplacement) 재사용 검토
- [ ] 메모리(Gain1/Gain2)·시큐리티 교체(ReplaceBottomSecurity)·특수진화(BlastDNA·Arts)
- [ ] 제약(CanNotBeBlocked·CanNotAttack·CantUnsuspend·CanNotBeDestroyedBySkill)·링크(GrantedReduceLinkCost·ChangeSelfLinkMax)·기타(Training·MaterialSave·UseRequirements·ReturnToLibraryBottom·MindLink·ChangeSAttack)
- 완료 → PRIM-W4.

---

## 실행 대화문 (복붙용)
```
PRIM-W3 진행. docs/audit/primitive_w3_goals.md 스펙대로 사용빈도순 순차 실행(would-be-deleted 대체군 Scapegoat/Decoy/Fragment는 기존 DeletionReplacement 창 재사용 여부 먼저 probe).
각 항목: 구현 전 원본(표 위치)에서 1:1 확인(추측 금지) → probe(키워드=기존 팩토리 관용, static 비-self=self 버전에 대상 스코프 확장, 제약=해당 게이트 정적 바인딩) → 미러 → bash scripts/run-tests.sh green + 격리/픽스처 테스트 + tools/RuleAudit 0. 이전 항목 green 후 다음. 중앙 게이트 재구현 금지, 범위 밖 NotSupported. AS-IS 불명확하면 중단·확인. 커밋은 내가 지시할 때.
```
