# 결함 관리 대장 (defect register)

기준: **AS-IS(DCGO/Assets/Scripts/) 동일로직 동일경로**. 발견 출처 = 2026-07-28 적대리뷰 7구역(리뷰어는 미러 주석 미참조) + 재이관 과정 발견 + 수리 과정 발견.

## 표기
- **상태**: `FIXED`(수리·검증 완료) / `OPEN`(미수리) / `WATCH`(구조적 부채)
- **도달성**: `LIVE`(현재 경로로 발현 확인) / `경로없음(확인범위: …)`(현재 도달 경로 못 찾음 — *잠복이 아니라 "아직 못 찾음"으로 읽을 것*) / `미확인`
- **검증**: `직접`(코디네이터가 AS-IS 원문 대조) / `리뷰어`(서브에이전트 보고, 미재확인)

---

## A. 수리 완료 (FIXED)

| ID | 영역 | 결함 | AS-IS 근거 | 실제 영향(확인) | 검증 |
|---|---|---|---|---|---|
| F-01 | AttackProcess | 공격/블록 서스펜드가 `SuspendPermanentsClass` 우회(원시 메타 기록) | AttackProcess.cs:160-166, :557 | OnTappedAnyone 창 미발화(ST4_14·EX11_074·BT23_081), cannot-suspend·CanNotBeAffected 필터·DPWhenSuspended 누락 | 직접 |
| F-02 | AttackProcess | `SwitchDefender`가 cause effect 폐기(`"CardEffect", null` 하드코딩) | AttackProcess.cs:536-540 | 리다이렉트 4종(AD1_012·BT15_078·BT25_039·Raid)에서 "상대 효과 영향 없음" 판정 불가 | 직접 |
| F-03 | Security | `SecurityRuleGateSeam.CanAddSecurity/CanReduceSecurity`가 `=> true` 스텁 | Player.cs:1471, :1523 | 시큐리티-열람 창 중 배치가 차단되지 않음 | 직접 |
| F-04 | Permanent | `DigivolutionCards` 순서 역전(아래→위) | Permanent.cs:888 | BT25_096 오선택(가장 깊은 뒷면 대신 맨 위), candidate pool 32곳 역순, DiscardEvoRoots 트래시 순서 반전 | 직접 |
| F-05 | CardObjectController | `cardSource.Init()` 6곳 누락(주석은 "no-op"이라 허위 표기) | CardObjectController.cs:550/627/732/835/929/969 | 덱·트래시·핸드 복귀 후 "턴 1회" 사용횟수 미초기화 | 직접 |
| F-06 | Player | 미러 `Player`에 값-동등성 없음 → `Player==Player` 항상 false | AS-IS Player는 좌석 싱글턴(참조동일=좌석동일) | **BT7_087 트리거 영구 불발**, **EX11_004 게이트 영구 false**, 마리건 라벨 오표기 | 직접 |
| F-07 | TurnStateMachine | 랜덤 선공이 같은 시드에서 AS-IS와 반대 좌석 | :255 draw + :312 루프-최상단 SwitchTurnPlayer | 시드-패리티 붕괴(룰 위반은 아님) | 직접 |

### 재이관 과정에서 함께 복구된 것
| ID | 내용 |
|---|---|
| F-08 | 죽은 트리거 4종 복구(OnCounter 2-pass·OnDigivolutionCardReturnToDeckBottom·WhenDigivolving×2) → AS-IS `StackSkillInfos` |
| F-09 | `OnAddDigivolutionCards` 창 복구(BT22_044·EX6_001이 등록했으나 발화 0이던 상태) |
| F-10 | `WhenLinked` 창을 AS-IS `StackSkillInfos` 형태로 복구 |
| F-11 | 초기 핸드 딜 순서 역전(pre/post-switch 좌표) 수정 |
| F-12 | RD-EOT-SELFDELETE 해결(reader 없는 마커 릴레이 → AS-IS `PermanentEffects` 본체) |
| F-13 | R2-P2-4 해결(DP0 스윕 → AS-IS `DigimonLackDPProcess`) |
| F-14 | RL 관측 DP가 printed DP를 보고하던 것 → AS-IS 실효 `Permanent.DP` |
| F-15 | `ContinuousRestrictionGate` 은퇴로 디지볼브 카드 미전달 결함 해소(→ `CardSource.CanNotEvolve`) |

---

## B. 미수리 (OPEN)

### B-1. 수리 과정에서 새로 드러남
| ID | 영역 | 결함 | 도달성 | 검증 |
|---|---|---|---|---|
| O-01 | CardSource | **미러 `Init()`이 AS-IS와 다름** — AS-IS 3문장(`InitUseCountThisTurn`/`SetFace`/`SetChangedLocationTime`) 중 2개 없고, AS-IS에 없는 effect-list 재료화 패스가 있음. F-05 복원으로 그 패스가 6곳(퇴장 카드 포함)에서 실행됨 | LIVE(이번 수리로 유발) | 직접 |
| O-02 | CardObjectController | `AddTrashCards(List<CardSource>)` 미포팅 (AS-IS :739-777, 7번째 `Init()` 자리) | 미확인 | 리뷰어 |
| O-03 | Security | `IsSecurityLooking` 리더 2곳 미배선(`RemoveFromAllArea`, 소스추가 face-stamp 게이트) | 미확인 | 리뷰어 |
| O-04 | 카드 | BT14_033·BT14_093·ST10_06·BT16_024가 `IsSecurityLooking`을 설정하지 않음(포팅됨) | LIVE | 리뷰어 |
| O-05 | CardController | `SuspendPermanentsClass`의 `CanNotBeAffected` 게이트 술어가 AS-IS(`CardEffect != null`)와 다름(`EffectSourceCard.InstanceId` 비어있지 않음) | 경로없음(확인범위: 현 2 호출부) | 리뷰어 |
| O-06 | CardSource | `SetChangedLocationTime` 캐리어 부재(MIG3-LOCATIONTIME) | 미확인 | 리뷰어 |

### B-2. 적대리뷰 — 뮤테이션/시큐리티
| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-07 | 시큐리티 face 판정이 다른 키(`IsFlipped`→`"isFlipped"` vs `SecurityFaceState`→`"securityFaceUp"`) → `isFaceDown` 항상 false | 경로없음(확인범위: 소비 카드 EX10_012/020/035/057 전부 스텁) — **카드 포팅 시 즉시 발현** | 직접 |
| O-08 | `IPutSecurityPermanent` 강등 시점: AS-IS는 첫 `OnAddSecurity` 창을 맨 위 상태로 열고 이후 강등, 미러는 먼저 바닥 삽입 후 창 | 미확인 | 리뷰어 |
| O-09 | DigiEgg를 덱 "위"로 되돌릴 때 digitama 덱 바닥에 삽입(IZoneMover에 digitama-top API 부재) | 미확인 | 리뷰어 |
| O-10 | `AceOverflowClass`: AS-IS는 비-ACE 육성 디지몬에도 `AddMemory(0)` 호출, 미러는 `overflow>0`일 때만 | 미확인 | 리뷰어 |
| O-11 | `AddSecurityFromLibrary`가 배치 이동 후 일괄 처리(AS-IS는 카드별 인터리브) → `IAddSecurity` 창이 후속 카드 포함 스택 관측 | 미확인 | 리뷰어 |

### B-3. 적대리뷰 — Permanent / 소스·링크
| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-12 | `AddLinkCard` 오버플로우 트림을 await하지 않음 → `WhenLinked`가 초과 링크·과다 DP 관측 | 미확인 | 리뷰어 |
| O-13 | App-Fusion re-root(top swap) 시 `linkedCardIds`/`linkedDp`/`dpBoosts` 미이관 → 링크 소실·DP 손실 | 미확인 | 리뷰어 |
| O-14 | `isFromSameDigimon` 항상 false(detach 후 계산, `sourceIds`만 조회) | 미확인 | 리뷰어 |
| O-15 | `isFromDigimon` 판정 축소(배틀에어리어 top만 인정) | 미확인 | 리뷰어 |
| O-16 | 다중 zone 소스 추가 시 `OnAddDigivolutionCards` 창 2회(AS-IS 1회) | 미확인 | 리뷰어 |
| O-17 | 소스 추가 zone 조회에 host 소유자 사용 → 상대 카드가 양쪽 zone 이중 계상 | 미확인 | 리뷰어 |
| O-18 | 다중 zone Top 배치 시 인터리브 순서 소실 | 미확인 | 리뷰어 |
| O-19 | `AddDigivolutionCardsBottom`의 두 분기(다른 퍼머넌트 top / 자기 top) 부재 → `NotSupportedException` | 미확인 | 리뷰어 |
| O-20 | Top 경로의 조건부 face-up(`!IsFlipped \|\| IsBeingRevealed \|\| IsSecurityLooking`) 누락 | 미확인 | 리뷰어 |
| O-21 | 소스/링크 부착 프리미티브가 AS-IS에 없는 `InitUseCountThisTurn`을 수행 | 미확인 | 리뷰어 |
| O-22 | `RemoveCardSource`가 top/link 카드를 제거 못 함(`sourceIds`만 조작) | 미확인 | 리뷰어 |
| O-23 | `cardSources` 내 링크 카드 위치가 끝(AS-IS는 index 1, top 바로 아래) | 미확인 | 리뷰어 |
| O-24 | `LinkedDP` 0-클램프 및 리스트 비면 키 삭제(AS-IS는 음수/잔존 허용) | 미확인 | 리뷰어 |
| O-25 | `PermanentView.DigivolutionCards`는 여전히 아래→위(F-04 미적용) | 경로없음(확인범위: 현 소비자 `.Count`/`.Any`만) | 리뷰어 |

### B-4. 적대리뷰 — 공격/턴 흐름
| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-26 | `endGame`이 매 접근마다 새로 생성되는 `TurnStateMachine` 인스턴스에 기록 → 시큐리티 소진 킬 후 MainPhase 미탈출 가능 | 미확인 | 리뷰어 |
| O-27 | `EndAttack()`이 "즉시 종료 시퀀스 실행"에서 "플래그만 설정"으로 변경 → `OnEndAttack` 창 시점·횟수 변화 | 경로없음(확인범위: 16개 AS-IS 호출 카드가 미러에서 미참조) | 리뷰어 |
| O-28 | 공격 재진입 가드 무력화(`DeclareAttack`이 먼저 상태를 써서 조건이 항상 false) → 중첩 선언이 진행 중 공격을 교체 | 미확인 | 리뷰어 |
| O-29 | `AttackCount++`가 자격 검사 밖으로 이동 | 경로없음(확인범위: ST5_04 조건 미포팅) | 리뷰어 |
| O-30 | 게임 종료 경로가 End 스테이지·Cleanup을 계속 진행(AS-IS는 즉시 중단) | 미확인 | 리뷰어 |
| O-31 | 카운터 창 페이로드가 선언-시점 스냅샷 → 카운터-시점 라이브 뷰 | 미확인 | 리뷰어 |
| O-32 | `Cleanup()`이 attacker/defender 식별자까지 소거(AS-IS는 다음 선언까지 보존) | 미확인 | 리뷰어 |
| O-33 | `ActiveAttack()`이 더 이른 시점부터 true → `[On Attack]` 창 중 Collision 분기 활성 | 미확인 | 리뷰어 |
| O-34 | 생존 판정이 `TopCard == null` → 배틀에어리어 소속으로 대체(컨트롤러 변경 시 분기) | 미확인 | 리뷰어 |
| O-35 | `IBattle`이 `Permanent.battle` 역참조 미설정 → ST2_01 경로 소스 없음 | 미확인 | 리뷰어 |
| O-36 | `enteredThisTurn`이 소스로 묻힌 카드에 잔존(만료 스윕이 필드 존만 순회) | 미확인 | 리뷰어 |
| O-37 | `CanAttackTargetDigimon` 4항의 프레임 검사가 무조건 실행(AS-IS는 프레임 null이면 건너뜀) | 미확인 | 리뷰어 |

### B-5. 적대리뷰 — 스킬창/선택
| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-38 | 창 중첩 상한 부재(AS-IS는 컴포넌트 풀 20/20/8 고갈 시 스킬 폐기) | 미확인 | 리뷰어 |
| O-39 | `autoEffectOrder`/`AutomaticOrder` 자동 정렬 분기 부재 | 미확인 | 리뷰어 |
| O-40 | `MatchConditionPermanentCount`는 `_card.Context`, 술어는 `AmbientMatchContext` 사용 → 병렬 워커에서 다른 보드 스캔 위험 | 미확인 | 리뷰어 |
| O-41 | `SelectJogressEffect.SelectWheterToJogress` 미러에서 호출자 0(AS-IS는 UI 드롭 경로) | 미확인 | 리뷰어 |

### B-6. 2차 적대리뷰(Sonnet) — AutoProcessing
**공통 패턴: 미러가 AS-IS에 없는 가드를 추가해 룰 집행을 억제** (일부는 삭제된 substrate 발명물에서 베껴옴)

| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-42 | `IsDigimonLackDP`에 `!IsDpDefined → return false` 가드 추가(AS-IS는 `DP==0 && IsDigimon && CanBeDestroyed()` 뿐). 미러 주석이 `GameFlowProcessor.HasLethalDp`(삭제된 발명물) 모방이라 자인 | LIVE(DP 0 디지몬 생존) | 직접 |
| O-43 | `IsDigimonLackLinkCondition`/`DigimonLackLinkConditionProcess`에 `LinkConditionOf() is not null` 가드 추가 — **`LinkConditionOf`는 AS-IS 참조 0**(존재하지 않음) | LIVE(링크 트림 불발) | 직접 |
| O-44 | `IsNotDigimonInBreeding`에 `!HasDefinition → return false` 가드 추가 | 미확인 | 리뷰어 |
| O-45 | `IsNotHavingDP`에 `IsDigimon && !IsDigiEgg && !IsDpDefined → return false` 가드 추가 | 미확인 | 리뷰어 |
| O-46 | `RuleProcess`: 게임종료 시 AS-IS는 `IsRuleProcessing`이 true로 고착(이후 룰처리 영구 차단), 미러는 해제 | 미확인 | 리뷰어 |
| O-47 | `DigimonLackLinkMaxCountProcess`: AS-IS는 초과 퍼머넌트 전부를 한 호출에서 처리, 미러는 첫 선택-대기에서 반환 → 나머지는 RuleProcess 재진입 후 처리(중간에 다른 룰 단계 재실행) | 미확인 | 리뷰어 |
| O-48 | `RuleProcess`에 AS-IS에 없는 "무진전 시 break" 루프 탈출 추가 | 미확인 | 리뷰어 |
| O-49 | `EndGameProcess`에 AS-IS에 없는 `IsTerminal()` 선-차단 추가 | 미확인 | 리뷰어 |
| O-50 | `EndTurnProcess`: AS-IS `SetMainPhase()` 재호출 분기가 미러에선 빈 블록 | 미확인 | 리뷰어 |
| O-51 | `AutoProcessCheck`의 `IsSelecting` true/false 브래킷 부재 | 미확인 | 리뷰어 |
| O-52 | `EndTurnProcess` 메모리: AS-IS는 `PlayerID==0 ? Memory=3 : Memory=-3`(좌석-절대), 미러는 무조건 `Set(-3)` — 미러는 "게이지가 턴플레이어-상대좌표라 축약"이라 주장 | **판정보류(검증필요)** | 리뷰어 |

### B-7. 2차 적대리뷰(Sonnet) — CardSource
| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-53 | **`EqualsCardName`/`ContainsCardName`/`EqualsTraits`/`ContainsTraits`가 공백-제거 매칭 누락** — AS-IS는 5비교(`Replace(" ","")` 양방향 포함), 미러는 대소문자무시 완전일치 1개. 포팅 카드에서 **공백 포함 인자 호출 57건** | **LIVE(광범위)** | 직접 |
| O-54 | `GetCostItself`가 `IChangeCostEffect` fold 없이 printed cost 반환(AS-IS는 fold) | 미확인 | 리뷰어 |
| O-55 | `HasDP` 로직 상이 — AS-IS `IsDigimon \|\| DP>0 \|\| BaseDP>0`, 미러는 `"dp"` 메타키 존재 여부만 | 미확인 | 리뷰어 |
| O-56 | `IsLevel(n)`이 fold된 `Level` 비교(AS-IS `IsLevel2~6`은 printed Level 비교) | 미확인 | 리뷰어 |
| O-57 | `CanPlayFromHandDuringMainPhase`가 프레임 존재/여유 검사 누락(AS-IS `CanPutFieldThisPermanentCard`의 프레임 순회) | 미확인 | 리뷰어 |
| O-58 | `HasCardColor`가 `DualCardColors` 미조회(AS-IS는 `AllCardColors`) | 미확인 | 리뷰어 |
| O-59 | `HasSaveText`가 `HasText("<Save>")` 대신 메타플래그+스캔 | 미확인 | 리뷰어 |
| O-60 | `CanNotTrashFromDigivolutionCards`에 AS-IS에 없는 `TrashProtectedKey` 메타 분기 추가 | 미확인 | 리뷰어 |
| O-61 | `EvoCosts`가 문자열 파싱 기반 + AS-IS에 없는 색-와일드카드/`TokenMatch` 신설 | 미확인 | 리뷰어 |
| O-62 | `SetFace`/`SetReverse`/`IsFaceUp`/`IsFaceDown`/`ChangedLocationTime`/`SetChangedLocationTime` 전부 부재(`IsFlipped`는 read-only, writer 없음) | 미확인 | 리뷰어 |
| O-63 | 특성 카테고리 속성 ~57종 부재(`HasBeastTraits`·`HasRoyalKnightTraits` 등) + `HasGreymonName` 등 이름 헬퍼 부재 | 미확인 | 리뷰어 |
| O-64 | `CanLink`의 `PayCost`가 필수→기본값 false로 변경 | 미확인 | 리뷰어 |
| O-65 | `LinkDP`·`IsDualCard`·`IsACE`·`OverflowMemory`·`HasUseCost`·`HasInheritedEffect` 등 AS-IS 멤버 부재 | 미확인 | 리뷰어 |

### B-8. 2차 적대리뷰(Sonnet) — 뮤테이션 프리미티브
| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-66 | **`DrawClass`가 `AddHandCards`를 우회**(ZoneMover.DrawAsync 직접) → 드로우가 `OnAddHand` 창을 열지 않음. 부수로 토큰/DigiEgg 라우팅·ACE overflow·per-card `Init()`도 미실행 | **LIVE**(모든 드로우) | 직접 |
| O-67 | **`IDegeneration`이 플레이어의 해제-횟수 선택을 생략**(AS-IS `SelectCountEffect` 0~max) — 생성자 지정값 강제. 주석은 "LOUD STUB"이나 실제 무음. **원문 좌표 확정**: AS-IS `CardController.cs:4813-4835`(ruling==null이면 `SelectPlayer=EffectSourceCard.Owner`·`MaxCount=Min(DigivolutionCards.Count, _degenerationCount)`·`CanNoSelect:false`로 2차 `SelectCountEffect` 발화 후 `:4837` 트래시 루프) ↔ 미러 `CardController.cs:912-921` `_ = maxCount;`. `BT2_066`은 AS-IS에서 **2회 질문**(카드 자체 0~2 + IDegeneration 내부 1..min), 미러는 1회 | **LIVE**(ruling 미지정 카드 전부) | 직접 |
| O-68 | `IAddSecurityFromLibrary`가 `AddSecurityCard` 우회 → 멤버십 가드·DigiEgg 라우팅·토큰 제외 미수행 | 미확인 | 리뷰어 |
| O-69 | `IDestroySecurity`가 `AddTrashCard` 우회 → `IsExistOnTrash` 재검사·`Init()` 미실행 | 미확인 | 리뷰어 |
| O-70 | `ITrashDigivolutionCards`: ACE overflow 과금 범위가 cut-in 이후 생존분으로 축소(AS-IS는 cut-in 이전 목록 기준) | 미확인 | 리뷰어 |
| O-71 | `ITrashDigivolutionCards`: `willBeRemoveSources` 마커 해제 시점이 물리 제거 이전으로 이동 + `TrashSpecificSourcesAsync`가 AS-IS에 없는 2차 보호 재검사 수행 | 미확인 | 리뷰어 |
| O-72 | `IDegeneration`: `SetChangedLocationTime()` 부재(MIG3-LOCATIONTIME, O-06과 동일 뿌리) | 미확인 | 리뷰어 |

### B-9. 2차 적대리뷰(Sonnet) — Select*Effect 3종
| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-73 | **`SelectHandEffect`의 이산 선택 제약 미표현** — AS-IS는 `canNoSelect=true, canEndNotMax=false`일 때 제출 가능 매수가 **0 또는 maxCount 뿐**(중간값 도달 불가). 미러 ChoiceRequest는 `[0..maxCount]` 연속 범위로 선언 → 에이전트가 AS-IS에 없는 부분 선택 제출 가능 | 미확인 | 리뷰어 |
| O-74 | **`_noSelect` 판정 혼동** — AS-IS는 `CardIDList == null`(명시적 "선택 안 함" 버튼)만 noSelect. 미러는 **빈 선택도 noSelect** 처리 → "0장으로 End Selection" 시 mode 처리 블록 전체를 건너뜀(PlayForCost의 ChangeCostClass 등록/해제 누락) | 미확인 | 리뷰어 |
| O-75 | 3파일 전부 `IsSelecting` save/restore 누락(AS-IS는 Activate 전체를 감쌈) — O-51과 동일 뿌리 | 미확인 | 리뷰어 |
| O-76 | `BuildRequest`의 minCount 공식이 같은 파일의 `CanEndSelectAsIs`(AS-IS 미러)와 **자기모순** — `canEndNotMax=true, canNoSelect=false`에서 전자는 최소 1, 후자는 0 허용. 현재 호출자 0(잠재 함정) | 경로없음(확인범위: src 전체 grep 호출자 0) | 리뷰어 |
| O-77 | `BuildRequest`의 `IsUntargetableBySkill` 필터가 무조건 적용(AS-IS 미러 `CanTargetAsIs`는 소유자 조건부) → 후보 풀 불일치 | 경로없음(호출자 0) | 리뷰어 |
| O-78 | `maxCount==0` 처리 불일치: SelectCard/SelectHand는 조기 반환(ChooseAsync 미호출), SelectPermanent는 `[0,0]` 요청 발행 | 미확인 | 리뷰어 |
| O-79 | `SelectCardEffect`의 `_isDeckBottom`/`_isDeckTop` 필드가 **읽히지 않음**(AS-IS는 auto-order `AutoSelect()` 분기 트리거) | 미확인 | 리뷰어 |
| O-80 | `Mode.Tap`이 `SuspendPermanentsClass`에 hashtable 대신 `_cardEffect`+`isBlock:false` 전달(AS-IS Tap/UnTap 비대칭 미보존) | 미확인 | 리뷰어 |
| O-81 | `PutSecurityTop/Bottom`이 AS-IS의 전용 `CardEffectCommons.CardEffectHashtable(_cardEffect)` 대신 공용 로컬 hashtable 재사용(AS-IS는 이 두 모드만 별도 헬퍼 사용) | 미확인 | 리뷰어 |
| O-82 | `Mode.AddHand`의 소스 분리가 `Effects.RemoveDigivolveRootEffect` → `Permanent.RemoveCardSource`로 대체(등가 미확인) | 미확인 | 리뷰어 |

### B-10. 3차 적대리뷰(렌즈 제거) — GameContext / Player
**주의: 이 구역에서 미러 주석의 허위 주장 3건 확인** — 주석은 증거로 쓸 수 없음이 재차 실증됨.

| ID | 결함 | 도달성 | 검증 |
|---|---|---|---|
| O-83 | `Player.CanMove`가 빈 프레임 조건 누락 — AS-IS `&& fieldCardFrames.Count(IsEmptyFrame)>=1`. 배틀에어리어 만석에도 육성→배틀 이동 허용 | **LIVE** | 직접 |
| O-84 | `GameContext.PermanentsForTurnPlayer`가 육성 에어리어 제외 — AS-IS `GetFieldPermanents()`(배틀+육성) vs 미러 `GetBattleAreaPermanents()`. **미러 주석이 "AS-IS는 GetBattleAreaPermanents"라 허위 기재** | **LIVE** | 직접 |
| O-85 | O-67(IDegeneration 선택 생략)의 스텁 사유 *"no SelectCountEffect mirror"*가 **거짓** — 미러 `SelectCountEffect.cs`(237줄, ChooseAsync 포함) 실재. 기술 제약이 아니라 배선 누락 | **LIVE** | 직접 |
| O-86 | `FieldCardFrame.IsBattleAreaFrame()`이 점유 필요로 변경 — AS-IS는 프레임 인덱스만으로 분류(빈 프레임도 배틀로 분류). 빈 프레임이 미러에선 육성으로 오분류 | 미확인 | 리뷰어 |
| O-87 | 메모리 부호 모델 상이 — AS-IS는 **좌석 고정**(`PlayerID==0`이면 항상 부호반전), 미러는 **현재 턴플레이어 여부**로 반전. 턴마다 같은 좌석의 부호가 뒤집힘. O-52의 뿌리 | 미확인(턴경계 재-부호 보정 여부 미검증) | 리뷰어 |
| O-88 | `Player.IsLose` getter 부재(`SetLose`만 포팅) | 미확인 | 리뷰어 |
| O-89 | `GameContext`: `You`/`Opponent`·`FirstPlayer`·`SwitchTurnPlayer()`·`PlayerFromID()`·`DoSwitchTurnPlayer`·`Memory` setter·`ActiveCardList` setter 전부 부재 | 미확인 | 리뷰어 |
| O-90 | `Player`: `ExpectedMemory(int)`·`TurnCount`·`DigivolveCount_ThisTurn`·`TurnStartTime`·`KeyCard`·`LostCards`·선택큐 4종 부재 | 미확인 | 리뷰어 |
| O-91 | zone 리스트가 read-only 계산 프로퍼티 — `player.HandCards.Add(x)`가 무효(AS-IS는 직접 가변 필드) | 미확인 | 리뷰어 |

### B-11. 전수 1:1 검수 (파일당 에이전트 1개)
| ID | 파일 | 결함 | 도달성 | 검증 |
|---|---|---|---|---|
| O-92 | CanUseEffects/OnDeletion.cs | `IsByEffect`가 AS-IS에 없는 `ReadCauseMarker(byEffectCause)` 폴백 보유 — 그 경로는 `cardEffectCondition`(예: Decoy의 "상대 효과로" 필터)을 **평가하지 않음** | 경로없음(확인범위: 마커 세터 `OnDeletionHashtable(...)` 오버로드 호출자 0) | 리뷰어 |
| O-93 | CardSource.PermanentOfThisCard | 배틀에어리어만 스캔(AS-IS `GetFieldPermanents()`=배틀+육성) — O-57/O-84와 동일 뿌리 | 미확인 | 리뷰어 |
| O-94 | ICardEffect.cs | **`CanActivate`의 계승/링크 효과 게이트가 육성에어리어 카드에서 통째 스킵** — `PermanentOfThisCard()`가 null 반환 → TopCard 동일성·IsFlipped·IsDigimon·LinkedCards 검사 전부 미실행, "트리거 시점과 같은 퍼머넌트" 재검사도 스킵 | **LIVE** | 리뷰어 |
| O-95 | ICardEffect.cs | `IsOnDeletion`이 실제 스택 대신 **합성 단일카드 `Permanent`** 생성(육성에어리어 카드) → `[On Deletion]` 조건이 소스·링크 구성을 다르게 봄 | **LIVE** | 리뷰어 |
| O-96 | ICardEffect.cs | `EffectTiming` enum에 AS-IS에 없는 `WhenDigivolving` 멤버 추가 + **선언 순서 전면 재배치**(ordinal 상이) — int 캐스팅/직렬화 시 문제 | 미확인 | 리뷰어 |
| O-97 | ICardEffect.cs | `EffectSourceCard` 게터의 폴백 분기가 미러에선 도달 불가(`Permanent.TopCard`가 null을 반환할 수 없음) | 경로없음(확인범위: SetEffectSourcePermanent 전 호출부) | 리뷰어 |

**뿌리 지적**: O-57·O-84·O-93·O-94·O-95는 전부 **`CardSource.PermanentOfThisCard()`/필드 조회가 배틀에어리어만 보는 하나의 뿌리**에서 파생. AS-IS는 `GetFieldPermanents()`(배틀+육성). 수리는 뿌리 1곳에서.
| O-98 | CEntity_EffectController.cs | **`CEntityUseCycle` 스테이징 기제가 AS-IS에 없음** — AS-IS는 `UseEffectsThisTurn.Add/Remove` 즉시 반영, 미러는 사이클 중 지연 커밋 + 조회 시 스테이징분 가산 보정. 환불은 참조동일성 대신 `IsSameEffect` 논리 일치로 매칭 | 미확인(사이클 Begin 호출자 생존 여부 미검증) | 리뷰어 |
| O-99 | CEntity_EffectController.cs | AS-IS `AddCardEffect(string ID, string ClassName)` **전체 부재** — 타입 해석 기제가 `CardEffectDispatch`(카드번호/effectClass 메타 기반)로 대체, 트리거 조건·폴백 경로 상이 | 미확인 | 리뷰어 |
| O-100 | CardEffectFactory.cs | `PlaySelfDigimonAfterBattleSecurityEffect`가 하위 substrate 오버로드 직결 → AS-IS `CanEnterField(activateClass)`(「등장 불가」 스캔) 미실행. 같은 파일 타 호출부는 AS-IS 오버로드 사용 | **LIVE** | 리뷰어 |
| O-101 | CardEffectCommons | **`CanPlayAsNewPermanent`에 `root` 인자·빈 프레임 검사 부재** (AS-IS는 `fieldCardFrames.Some(빈프레임 && CanPlayCardTargetFrame(...))`) → 필드 만석에도 통과. CardEffectFactory 5개 호출부 영향. 뿌리①(프레임/필드 축소) | **LIVE** | 리뷰어 |
| O-102 | CardEffectFactory.cs | AS-IS에 없는 포트 전용 팩토리 16종(`CanNotReduceCostStaticEffect` 등) — AS-IS는 각 카드가 kind-class를 인라인 생성. 등가 여부 미검증 | 미확인 | 리뷰어 |
| O-103 | CardEffectFactory.cs | `Gain1MemoryTamerOwnerDigimonConditionalEffect`가 빈 설명문일 때 기본 문장 치환 + `condition`/`permanentCondition` null 허용(AS-IS는 비허용) | 미확인 | 리뷰어 |
| O-104 | CardEffectCommons.cs | **`PlayToken`의 CardSource-전용 오버로드가 AS-IS 용량 게이트 생략** — AS-IS는 `빈 배틀프레임 >= quantity` 전량-or-무. 포팅 카드 **P_165가 이 게이트 없는 오버로드를 호출** | **LIVE** | 리뷰어 |
| O-105 | CardEffectCommons.cs | **`AddActivateMainOptionSecurityEffect`에서 `effectDiscription` 파라미터 삭제** — AS-IS 182장이 넘기던 작성자 설명문이 전부 기계 치환("[Main]"→"[Security]")으로 대체. 헤드리스 호출부 22곳 | **LIVE** | 리뷰어 |
| O-106 | CardEffectCommons.cs 외 | `Gain*`/`Change*` CardSource-전용 계열이 실제 causing effect 대신 **합성 `BareCauseEffect`**를 `CanNotBeAffected` 검사에 전달(약 12종+) | 미확인 | 리뷰어 |
| O-107 | CardEffectCommons.cs | AS-IS `OptionSecurityEffect(CardSource)` **전체 부재**(미러 전역 정의 0) | 미확인 | 리뷰어 |
| O-108 | CardEffectCommons.cs | `TrashDigivolutionCardsAndProcessAccordingToResult` **동명 다른 시그니처**가 추가돼 이름만으로 해석 시 AS-IS 아닌 멤버로 연결 | 미확인 | 리뷰어 |
| O-109 | CardEffectCommons.cs | `IsExistOnBreedingArea`에 **링크 카드 arm 부재** — AS-IS `PermanentOfThisCard()`는 cardSources∪linkCards를 봄 | 미확인 | 리뷰어 |
| O-110 | NewModelContinuousScan.cs | **병렬 스캔 층 전체가 "호출자가 합쳐준다"는 전제로 검사를 생략** — 그 호출자(ContinuousKeywordGate·MatchStateMutationSink·ContinuousRestrictionGate·BattleDeletionGate)는 **이번 세션에 전부 삭제돼 존재하지 않음**. 생략분이 보충되지 않음 | 구조 | 리뷰어 |
| O-111 | NewModelContinuousScan.cs | `HasPierce`가 AS-IS `CanTrigger(PierceCheckHashtableOfPermanent)` 게이트 전무 → 효과 존재만으로 true | 경로없음(호출자 0) | 리뷰어 |
| O-112 | NewModelContinuousScan.cs | `HasBlocker`가 AS-IS의 **공격자 Collision 단락**(Blocker 효과 없어도 true) 누락 | 경로없음(호출자 0) | 리뷰어 |
| O-113 | NewModelContinuousScan.cs | `HasDecoy`가 AS-IS `CanUseCondition`(배틀에어리어·진행중 제거이벤트·상대 효과 유래) 전부 제거, 이름 접두사만으로 true | 경로없음(호출자 0) | 리뷰어 |
| O-114 | NewModelContinuousScan.cs | `CanNotDigivolve`가 AS-IS `IsToken` 가드 2개 + "자기 효과" 3번째 영역 누락(같은 미러 `CardSource.CanNotEvolve`엔 존재) | 경로없음(호출자 0) | 리뷰어 |
| O-115 | NewModelContinuousScan.cs | `HasCannotReturnToHand/Library`가 AS-IS 중첩루프 동작(필드 퍼머넌트 0인 플레이어의 player-scope 효과 미평가)을 평탄화 — **BT10_086 경유 라이브** | **LIVE** | 리뷰어 |
| O-116 | NewModelContinuousScan.cs | `FoldLinkCost`의 `linkCondition==null → 0` 조기반환 누락 · `FoldCardDp`의 `HasDP` 게이트 누락 | 경로없음(호출자 0) | 리뷰어 |
| O-117 | SelectDigiXrosClass.cs | AS-IS `EndSelectDigiXros()`는 iterator라 bare 호출 5곳에서 **본문 미실행**(AS-IS 버그), 미러는 동기 메서드라 **본문 실행** — 현재 플래그 소비자 없음 | 경로없음(소비자 0) | 리뷰어 |
| O-118 | Permanent.cs | `AddDigivolutionCardsBottom` 배틀에어리어 re-parent 경로에서 **`CanNotBeAffected` 면역 검사 미수행**(AS-IS는 `IPlacePermanentToDigivolutionCards` 경유로 수행) — O-19 동일 뿌리 | 미확인 | 리뷰어 |
| O-119 | SelectDigiXrosClass.cs | AS-IS `[PunRPC] SetTargetDigiXrossIndex` 부재 — ChoiceProvider가 AS-IS의 3분기(본인 패널/원격 인간/AI 균등난수)를 재현하는지 미검증 | 미확인 | 리뷰어 |
| O-120 | DNADigivolveEffects.cs | `PlayTempPermanent` **미포팅** — 프레임 인덱스 범위 밖이면 후보 제외하던 null 반환 경로가 2곳(`CardFulfillsRequirement`·`PermanentFulfillsRequirement`)에서 소실. 뿌리① | 미확인 | 리뷰어 |
| O-121 | DNADigivolveEffects.cs | 재료 확정이 **프레임 용량 검사 있는 `CreateNewPermanent(Permanent,int)` 대신 검사 없는 `CreateNewPermanent(CardSource,...)`** 호출 → 용량 초과 거부 경로 부재. 뿌리① | 미확인 | 리뷰어 |
| O-122 | DNADigivolveEffects.cs | 배틀에어리어 디지몬 2 미만 시 AS-IS는 `failedProcess()`를 무가드 호출(널이면 NRE로 코루틴 중단), 미러는 널가드 후 조용히 return — **AS-IS 호출자 20+가 failedProcess 생략** | 미확인 | 리뷰어 |
| O-123 | RestrictionHelpers.cs | **AS-IS 대응 0의 발명 층**(제한을 문자열키+레코드로 일반화). 실행 멤버 전 호출자 0, 상수 8개만 스위치 라벨로 소비. **AS-IS의 `_cardEffectCondition` 술어(예: "상대 효과로만")를 이 모델은 표현 불가**(id 완전일치만) — 살아났다면 조건부 제한이 뭉개짐. 원장 삭제대상 Y | 구조(호출자 0) | 리뷰어 |
| O-124 | RevealLibrary.cs | **`IsBeingRevealed` 플래그를 설정/해제하는 코드 전무** → AS-IS는 공개 중 트래시된 카드의 "덱에서 버려졌을 때" 트리거를 억제하는데 미러는 게이트가 항상 false라 전부 발동. `TrashCardAsync(isRevealTrash:)`도 `_ = isRevealTrash;`로 폐기 | **LIVE** | 리뷰어 |
| O-125 | RevealLibrary.cs | `selectCardCoroutine`을 **모든 Mode에서 호출**(AS-IS는 `Mode.Custom`만) — 카드 **BT16_094**에서 AS-IS 미실행 콜백이 실행되어 후속 `PlaceDelayOptionCards` 입력이 달라짐 | **LIVE** | 리뷰어 |
| O-126 | RevealLibrary.cs | `RevealDeckTopCardsAndSelect`의 `Mode.AddHand`에서 **디지타마 예외·디지볼브루트 분리 누락** → AS-IS는 디지타마를 덱 바닥으로, 미러는 핸드로 | **LIVE** | 리뷰어 |
| O-127 | RevealLibrary.cs | 빈 메시지 시 기본 프롬프트가 AS-IS는 Mode별 5종, 미러는 단일 문자열 | 미확인 | 리뷰어 |
| O-128 | HashtableSetting.cs | `OnDeletionHashtable`의 `CardColors`가 문자열→enum 라운드트립(`ToCardColorList`) → `Enum.TryParse` 실패 색을 **무음 누락**(AS-IS는 네이티브 `List<CardColor>`라 누락 불가). "특정 색 파괴 시" 조건이 색을 놓칠 수 있음 | 미확인(미매칭 문자열 생성 가능성 미검증) | 리뷰어 |
| O-129 | HashtableSetting.cs | 포트 전용 오버로드 2종이 AS-IS에 없는 마커키(`byBattleCause`/`byEffectCause`)를 기록 — `IsByBattle`/`IsByEffect`가 그 폴백을 읽도록 동반 개조됨(O-92와 동일 뿌리) | 미확인 | 리뷰어 |
| O-130 | SelectAssemblyClass.cs | `AddDigivolutiuonCards`/`ByEffect`가 호스트 퍼머넌트를 **루프 전 1회 캐싱**(AS-IS는 매 반복 재해석). 소재 추가마다 `OnAddDigivolutionCards` 창이 열리므로 그 사이 퍼머넌트가 바뀌면 낡은 대상에 부착 | 미확인 | 리뷰어 |
| O-131 | SelectAssemblyClass.cs | 정적 `TryMatchMaterials`(**HeadlessLegalActionDispatcher가 사용 → 에이전트 후보 결정**)가 `CanSelectAssembly`의 3분기 누락: `excludedCards` 자기제외·예약된 트래시카드 제외·퍼머넌트 대체 폴백 → AS-IS가 배제하는 카드를 후보로 제시 | 미확인(실행측 소비자 미확인) | 리뷰어 |
| O-132 | SelectAssemblyClass.cs | `AddDigivolutionCardsBottom`에 `ICardEffect` 대신 **소스카드 id만** 전달 → 창 소비자가 효과 인스턴스를 못 봄 | 미확인 | 리뷰어 |
| O-133 | CardController.cs (AceOverflowClass) | 미러가 루프 안에서 `OverflowFor()`를 재계산해 **ACE/뒷면 게이트를 육성에어리어 항목에도 재적용** — AS-IS는 육성 디지몬을 그 게이트 없이 통과시키고 `OverflowMemory`를 차감. ACE 아닌 육성 디지몬의 오버플로우가 미러에선 0 (O-10과 동일 함수) | 미확인(해당 카드데이터 존재 여부 미검증) | 리뷰어 |
| O-134 | SelectAttackEffect.cs | `_noSelect` 필드가 private→**public** 변경(외부에서 임의 조작 가능) | 경로없음(외부 쓰기 없음) | 리뷰어 |
| O-135 | SelectAppFusionEffect.cs | `SelectWheterToAppFusion`/`SelectLink`가 **호출자 0(사문)** — App-Fusion 선택이 `HeadlessLegalActionDispatcher`의 사전 열거로 대체됨. 그 열거가 AS-IS 선택 로직(필터·조건)과 동등한지 미검증. **O-131과 동일 계통(대화형 선택 클래스 우회)** | 구조 | 리뷰어 |
| O-136 | SelectAppFusionEffect.cs | 같은 클래스 내 두 메서드가 서로 다른 컨텍스트 해석 경로 사용(`RequireContext()` vs `GManager.instance`) | 경로없음 | 리뷰어 |
| O-137 | SelectCountEffect.cs | **오너 자동-단축 분기 전삭**(AS-IS :118-123) — `isYou && ((!_isDigivolutionCost && autoMaxCardCount && !_preferMin) \|\| (_isDigivolutionCost && autoMinDigivolutionCost && _preferMin))`이면 프롬프트 없이 `_preferMin ? Min : Max` 즉시 확정. 미러는 `_preferMin`/`_isDigivolutionCost`/토글을 `ChoiceRequest`에 실을 필드조차 없음(`autoMaxCardCount`/`autoMinDigivolutionCost` src 출현 0) → 설정 ON에서도 매번 선택 요청 | **LIVE**(`CardController:3285` 육성비용 픽, `SetPreferMin(true)`) | 리뷰어 |
| O-138 | SelectCountEffect.cs | **`isYou`/AI/상대-사람 3분기 붕괴**(AS-IS :116-161) — AS-IS는 AI 좌석에 선택권을 주지 않고 결정적 min/max를 직접 `SetCount`. 미러는 좌석 구분 없이 전 좌석 `ChoiceProvider.ChooseAsync` → 산출값이 프로바이더 구현에 의존. **O-131/O-135와 동일 계통(대화형 선택 클래스 우회)** | 구조 | 리뷰어 |
| O-139 | SelectCountEffect.cs | `Message_Enemy`가 `_ = Message_Enemy;`로 폐기(:120) — 필드 저장·독출 0. 상대 선택시에도 오너용 메시지가 요청에 실림(AS-IS :159는 별도 enemy 텍스트) | 경로없음(문자열 소비자=UI) | 리뷰어 |
| O-140 | SelectCountEffect.cs | `[PunRPC] SetCount(playerID, count)`(AS-IS :188-199)와 그 `GetPlayerFromID` 널가드 무대응 — 미러는 ID→Player 해석 자체를 하지 않음 | 구조 | 리뷰어 |
| O-141 | SelectCountEffect.cs | 단일-후보 경로 기제 상이 — AS-IS(:109-112)는 `SetCount`로 `ValueSelection`을 **큐잉 후 WaitUntil/Dequeue 동일 경로 통과**, 미러(:193-197)는 `_selectedCount = candidates[0]` 직접 대입으로 큐·프로바이더 전량 우회 | 미확인(외부 관찰자 미추적) | 리뷰어 |
| O-142 | SelectCountEffect.cs | 포트 전용 표면 4종 사문: 3-arg `SetUp`(:42-52)·`SetUpMessage`(:54-60)·`BuildRequest`(:65-71)·static `ReadSelectedCount`(:75-79) 호출자 0. 3-arg `SetUp`은 AS-IS에 없는 `ArgumentOutOfRangeException` 검증 추가(AS-IS는 음수 MaxCount를 가드로 무시)이며 `_preferMin`/`_isDigivolutionCost`/`_candidates` 리셋 누락(AS-IS :27-30) | 경로없음(호출자 0) | 리뷰어 |
| O-143 | SelectCountEffect.cs | `public int SelectedCount => _selectedCount;`(:236) — AS-IS 무대응 신설 독출면 | 경로없음 | 리뷰어 |
| O-144 | SelectCountEffect.cs | 라이브 `Activate()`(:209-215)가 **자체 헤더에 `⛔ DELETION-TARGET · DO-NOT-REFERENCE · 미러발명(AS-IS 무대응)`으로 표기된** `EffectChoiceHelpers.CreateCountRequest`에 의존 | 구조 | 리뷰어 |
| O-145 | GManager.cs / TurnStateMachine.cs | **`GManager.instance`가 매 접근마다 `new GManager(context)`, `TurnStateMachine.For`가 매 호출 `new TurnStateMachine`**(형제 `AutoProcessing.For:81`·`AttackProcess.For:70`은 `TryGetService`/`RegisterService` 캐시). AS-IS는 매치당 단일 컴포넌트 → `GManager.instance.turnStateMachine.X = v` 쓰기가 전부 즉시 폐기되는 일회용 객체로 감 . 미러 자체 주석이 `Passed`/`isExecuting` 2개만 `ConditionalWeakTable`로 우회했다고 적고 있고 나머지 10+ 필드는 평범한 auto-prop | **LIVE**(아래 O-146·O-147의 뿌리) | 직접(For 본문·instance 게터 실측) |
| O-146 | AttackProcess.cs:555 | `GManager.instance.turnStateMachine.EndGame(attackerOwner, false)` → `endGame=true`가 **일회용 인스턴스에 기록되고 소멸**. `TurnFlowPump`(:263-264)는 자기 장수명 인스턴스의 `endGame`을 루프조건(:286,:334)으로 읽음 → 이 쓰기를 못 봄. 종료가 성립하는 건 `MarkLose`→`RuleQueryService.IsTerminal()` 우연한 이중경로 덕분. AS-IS `endGame` 독출부 8곳(AutoProcessing:291·CutInProcess:16·MultipleSkills:400·TSM:307/318/327/936/976) 중 미러 대응은 TSM 내부만 | **LIVE** | 직접 |
| O-147 | AttackProcess.cs:398/416/574, CardController.cs:4687/4984/5143 | `GManager.instance.turnStateMachine.IsSelecting = true` 6곳 전부 일회용 인스턴스 대상 → 무조건 no-op. (AS-IS에서 `IsSelecting` 소비자는 `NextPhaseButton`뿐이라 현 시점 관측 결과는 동일하나, 기제는 절단됨) | 경로없음(확인범위: AS-IS 독출부=NextPhaseButton 단독) | 직접 |
| O-148 | GManager.cs:114-223 | 손수 구현한 `GetComponent<T>()`가 12-케이스 밖 `T`에 `NotSupportedException` throw. AS-IS는 Unity `Component.GetComponent`(미부착 시 **null 반환, 예외 없음**) → AS-IS의 null-가드 관용구가 미러에선 예외 | 미확인(AS-IS 전 호출부 null-가드 전수확인 미실시) | 리뷰어 |
| O-149 | GManager.cs | `GetPlayerFromID(int)`(AS-IS :494-506) 무대응 — src 출현 0. AS-IS 의존 호출부 13곳(SelectCardEffect:1016·SelectHandEffect:931·MultipleSkills:428·SelectCountEffect:191·OptionalSkill:137 등)이 각자 다른 방식으로 재작성됨(O-140과 동일 뿌리) | 구조 | 리뷰어 |
| O-150 | GManager.cs | `IsAI`(AS-IS :213) 무대응 — `!Owner.isYou && GManager.instance.IsAI` 게이트를 쓰는 AS-IS 분기가 미러에선 주석만 남고 전삭(`CardSource:1515`·`CardController:3278`). **O-138과 동일 뿌리** | 구조 | 리뷰어 |
| O-151 | GManager.cs | `OnClickSurrenderButton()`(AS-IS :431-442, `endGame` 가드 후 TSM 위임) 무대응 — 서렌더가 전용 호출 대신 `IsSurrender` 불리언 페이로드 필드로 대체(HeadlessActionFactory:129/141 등). AS-IS의 `endGame` 가드 재현 여부 미검증 | 구조 | 리뷰어 |
| O-152 | GManager.cs | 정적 이벤트 3종(`OnReverseOpponentsCardsChanged`/`OnCardFlippedChanged`/`OnSecurityStackChanged`, AS-IS :222-225) 무대응. **발화부가 카드효과 로직 파일**(BT20_055:115·EX11_031:209·BT23_045:283·BT23_043:104/184) 및 상태변이부(CardController:5427/5475·CardSource:83-96)에 있음(구독자는 UI) | 경로없음(확인범위: AS-IS 구독자=UI 단독) | 리뷰어 |
| O-153 | CardSource.cs:1747 | **`HasCardColor`가 `CardColors`만 검사** — AS-IS(:1564-1577)는 `AllCardColors.Contains` = `CardColors.Concat(DualCardColors)`. 듀얼(디지몬+옵션) 카드의 옵션-요구색이 색 판정에서 통째로 누락. **AS-IS 호출부 593곳**(포트 74곳)의 공유 인프라. (부수: AS-IS의 `isOptionOnly`/`isDigimonOnly` 파라미터는 포트에 없으나 AS-IS에서 true로 넘기는 호출부 0 → 무해. `HasDigimonColor`/`HasOptionColor`는 1:1 일치 확인) | **LIVE**(듀얼 카드 전부) | 직접(AS-IS :1555-1577 대조) |
| O-154 | ~~OnDeletion.cs:111-132~~ **오판정 정정** | Partition 리뷰어가 "`IsByEffect` 폴백이 `cardEffectCondition`을 평가하지 않아 소유자-조건이 소실된다(LIVE)"고 보고했으나, 후속 감사에서 **`ByEffectCauseKey`/`ByBattleCauseKey` 라이브 프로듀서 0**으로 실측됨(bool 오버로드 `OnDeletionHashtable(...,bool,bool,bool)`·`WhenPermanentWouldRemoveFieldCheckHashtable(...,bool,bool)` 호출부 0, 유일 언급은 TestFixtures 주석). 폴백은 항상 false → AS-IS `return false`와 결과 동일. **근거였던 미러 주석("삭제 싱크엔 라이브 ICardEffect 없음")도 프로듀서 실측과 불일치** | 정정: 결함 아님 | 직접(grep 실측) |
| O-163 | OnDeletion.cs:19-28/106/132 + HashtableSetting.cs:148/223 | **cause-marker 층 전체가 사문(미러 발명)** — 프로듀서 2(bool 오버로드), 라이브 호출부 0. 소비자 3(`ByBattleCauseKey`/`ByEffectCauseKey` const + `ReadCauseMarker`)과 `IsByBattle`/`IsByEffect`의 `||`·폴백 분기가 AS-IS에 없는 추가 코드경로로 상주. AS-IS `IsByBattle`(:82-85)·`IsByEffect`(:89-105)는 단일 경로. **은퇴 대상 후보**(O-92·O-129와 동일 층) | 경로없음(프로듀서 0, 실측) | 직접 |
| O-164 | DataBase.cs | `IsContainingXAntibodyString(string)`(AS-IS :443) 미포팅. AS-IS 호출 체인 `CardSource.HasXAntiBodyName`(:1671) → `EX5_015.cs:42,143`·`EX5_023.cs:43`. 해당 카드 포트는 현재 7줄 스켈레톤 → **카드 포팅 시 즉시 발현**(O-07과 동일 성격) | 경로없음(확인범위: 소비 카드 EX5_015/EX5_023 스텁) | 리뷰어 |
| O-165 | CardEffectCommons.cs:2071-2074 | `IsPermanentExistsOnBattleArea`에 AS-IS 무대응 `SnapshotZone` 분기 존재(AS-IS `GameContextDeterminarion.cs:348-362`는 단일 경로). 정상 경로(SnapshotZone null)에선 AS-IS 술어로 환원되나, leave-gate subject view가 제거-전 스냅샷으로 답하는 추가 경로가 상주 | 미확인 | 리뷰어 |
| O-166 | CardEffectCommons.cs:1994-1998 | `IsExistOnBattleAreaDigimon` 재구성 — AS-IS(:188-199)는 `card.Owner.GetBattleAreaDigimons().Contains(card.PermanentOfThisCard())`(**소유자 배틀에어리어 디지몬 목록 멤버십**), 포트는 `IsExistOnBattleArea(card) && new Permanent(ctx, TopCard.InstanceId, card.Owner).IsDigimon`(멤버십 대신 신규 Permanent 구성 후 IsDigimon 술어) | 미확인 | 리뷰어 |
| O-167 | UserSelectionManager.cs:99-121 | **AI 좌석 랜덤 픽 분기 전삭** — AS-IS `SetIntSelection`(:127-139)/`SetBoolSelection`(:181-194)는 `!isYou && IsAI`일 때 `canSelectValue[Random.Range(0, Count)]`로 **균등 랜덤** 즉시 확정. 포트는 전 좌석을 `ChoiceProvider.ChooseAsync` 단일 경로로 접고, 기본 `PolicyChoiceProvider.DefaultChoice`(:34-47)는 `Candidates.Where(IsSelectable).Take(MinCount)` = **항상 index 0 결정적**. 예: BT1_111 "Suspend 1/2"가 AS-IS AI 상대에선 ~50:50, 포트 기본 프로바이더에선 항상 "Suspend 1". **O-138·O-150과 동일 뿌리(AI 좌석 3분기 붕괴)** | **LIVE**(기본 프로바이더 배선 시) | 리뷰어 |
| O-168 | UserSelectionManager.cs:123-132 | AS-IS(:90-93) `yield return new WaitWhile(() => !_endSelect)`(무한 폴링 — 이후 `SetInt`/`SetBool`이 오면 해제 가능) → 포트는 `throw new InvalidOperationException` (복구 불가). 동일 시작상태에서 회복가능→치명 전환 | 미확인(호출부 ~100+ 중 3곳만 확인) | 리뷰어 |
| O-169 | UserSelectionManager.cs | 빈 후보 리스트 시 AS-IS는 행(hang), 포트는 `ChoiceResult.ThrowIfInvalid`(:96-103)로 즉시 예외 | 경로없음(빈 리스트 전달 호출부 미발견) | 리뷰어 |
| O-170 | **미러 전역** | **AS-IS 멤버가 다른 파일로 이사 = 동일경로 위반 53건**(기계측정, 목록=`docs/audit/member_home_moved.md`). 압도적 다수 **48건이 AS-IS `CardEffectCommons/GameContextDeterminarion.cs` → 미러 모놀리스 `CardEffectCommons.cs`로 흡수**(`IsExistOnBattleArea`·`HasMatchCondition*` 계열 전부). 나머지: `CanSuspend.cs`/`CanUnsuspend.cs`/`OptionEffect.cs` 각 1건도 같은 모놀리스행, `MatchConditionPermanentCount`는 `GameContextDeterminarion.cs`→**키워드 파일 `KeyWordEffects/Save.cs`**, `Create`는 `Networking/GamePacketFactory.cs`→`CEntity_EffectController.cs`, `CEntity_Effect.cs`→`CardEffectInterfaces.cs` 1건. **측정은 보수적**(양쪽 단일선언 멤버만 889개 대상) → 실제 위반 수는 이보다 큼. **에이전트 독립 확인**: Blitz.cs 담당이 `IsExistOnBattleArea`·`IsPermanentExistsOnBattleArea` 2건을 같은 이동으로 보고 | **구조(전역)** | 직접(기계측정) + 리뷰어 확인 |
| O-171 | ~~미러 전역~~ **오측정 정정** | 앞서 "`using` 목록 불일치 4,255/4,262"를 결함으로 올렸으나, import 줄은 substrate 치환(Unity/Photon 제거·헤드리스 타입 추가)으로 **정당하게 달라질 수 있음**. 텍스트-diff는 동일경로 지표가 아님 → 철회. 대체 지표 = O-170(호출 대상 멤버의 홈파일 일치 여부) |
| O-172 | AttackProcess.cs:650-668 | `DeleteSelfAtEndOfAttackKey` 메타데이터 플래그 삭제경로 — 주석은 "Execute 재배치"라 주장하나 **src 전역 write 0(사문)**. Execute의 실제 자기삭제 경로는 `UntilEndAttackEffects`→`EffectList_Added(OnEndAttack)`→`StackSkillInfos`→`MultipleSkills`로 별도 확인됨 | 경로없음(프로듀서 0) | 리뷰어 |
| O-173 | Retaliation.cs:149/161 (뿌리: CardEffectCommons.cs:3308-3313, 51-83) | `DeletePeremanentAndProcessAccordingToResult` 브릿지가 라이브 `activateClass`를 **`EffectSourceCard`만 남기고 `BareCauseEffect.For(sourceCard)`로 교체** → `DestroyPermanentsClass.Destroy()`가 적용하는 `CanNotBeAffected(cardEffect)`·`CanBeDestroyedBySkill(cardEffect)` 술어가 원인-효과의 `RootCardEffect`(항상 null)·`IsInheritedEffect`(항상 false)·구체 타입을 볼 수 없음. **F-02(SwitchDefender cause 폐기)와 동일 계통** | 미확인(`.RootCardEffect` 읽는 카드 0 확인, `.IsInheritedEffect` 12건 미추적) | 리뷰어 |
| O-174 | **미러 전역** | namespace 선언 오염 — 선언 보유 735개 중 **217개가 자기 디렉터리와 불일치**(예: `CardEffect/BT2/Yellow/BT2_003.cs`가 `...BT2.**Blue**` 선언, `Script/CardEffectFactory/*.cs` 다수가 `...Script.**CardEffectCommons**` 선언). O-170과 달리 substrate 사유가 없는 순수 복붙 오염 | **구조(전역)** | 직접(기계측정) |
| O-175 | Vortex.cs:81/84/102 | `PermanentHasVortexCanAttackPlayers`의 플레이어 열거 출처 변경 — AS-IS(:23/:39)는 `GManager.instance.turnStateMachine.gameContext.Players`(앰비언트), 미러는 `permanent.TopCard.Context`에서 파생. **O-40(MatchConditionPermanentCount 컨텍스트 불일치)과 동일 계통** — 병렬 워커에서 다른 보드를 스캔할 위험 | 미확인 | 리뷰어 |
| O-176 | **미러 전역** | **`partial class` → `static partial class` 변조**: AS-IS `public static partial class` 보유 파일 **1개**, 미러 **120개**. 예: AS-IS `public partial class CardEffectCommons`(GetFromHashtable.cs:7 등 전 파일) ↔ 미러 `public static partial class CardEffectCommons`. 인스턴스 멤버 보유 자체가 봉쇄되는 선언 변경 | **구조(전역)** | 직접(기계측정) |
| O-177 | Vortex.cs | 멤버 선언 순서 변경 — AS-IS `CanActivateVortex→PermanentHasVortexCanAttackPlayers→VortexProcess→GainVortex`(:7/19/56/81), 미러 `GainVortex→CanActivateVortex→PermanentHasVortexCanAttackPlayers→VortexProcess`(:23/61/79/124) | 경로없음(런타임 무영향) | 리뷰어 |
| O-178 | **미러 전역** | **AS-IS에 없는 `ArgumentNullException.ThrowIfNull` 240건 / 22파일**(최다 `CardEffectCommons.cs` 120·`NewModelContinuousScan.cs` 40·`Permanent.cs` 29). AS-IS 게임로직의 `ArgumentNullException` 출현은 **0**(전체 11건은 전부 서드파티 `ProfanityFilter/`). AS-IS는 null이 오면 지연 NRE 또는 그대로 진행하는데 미러는 진입 즉시 throw → 예외 종류·시점이 다르고, **AS-IS가 통과시켰을 경로를 차단**. 개별 파일 감사는 "현 호출부에 null 없음"으로 no-scenario 판정 중이나, 계통 자체가 AS-IS 무대응 | **구조(전역)** | 직접(기계측정) |
| O-179 | GiveEffectToPermanentOrPlayer.cs:26-29 | `AddEffectToPermanent`에 AS-IS 무대응 `targetPermanent is null \|\| InstanceId.IsEmpty → return` **무음 조기반환** 추가 — AS-IS(:11-13)는 가드 없이 switch로 진입. 효과 등록이 조용히 누락될 수 있는 경로 신설(O-178의 특수형: throw가 아니라 무음 스킵) | 미확인(현 호출부 전량 사전가드 확인) | 리뷰어 |
| O-180 | RestrictionCarriers.cs | **파일 자체가 AS-IS 무대응 병합 파일** — AS-IS `CardEffects/CanNotSelectBySkillClass.cs`(31줄)와 `CardEffects/CanNotMoveClass.cs`(31줄) 두 파일을 미러가 `RestrictionCarriers.cs` 하나로 합침. 클래스명도 `CanNotSelectBySkillClass`→`CanNotSelectBySkillEffect`, `CanNotMoveClass`→`CanNotMoveEffect`로 개명. **O-170(홈파일 이동)의 파일-레벨 형태** | **구조** | 리뷰어 |
| O-181 | RestrictionCarriers.cs:62, :123 | **분리 술어 2개를 조인트 술어 1개로 뭉개며 타입 축소** — AS-IS는 `PermanentCondition(Permanent) && CardEffectCondition(ICardEffect)`, 미러는 `_predicate(permanent.TopCard, cardEffect.EffectSourceCard)`로 **`Permanent`→`CardSource`, `ICardEffect`→`CardSource`** 축소. 탭/링크/필드위치/효과 정체성 등 Permanent·ICardEffect 수준 상태를 술어가 볼 수 없음. 작업규약 [fidelity-over-coverage] 위반 유형 | 미확인(AS-IS `CanNotSelectBySkillClass` 생산자 0, `CanNotMove` 생산자 1=EX7_014가 인자 무시) | 리뷰어 |
| O-182 | RestrictionCarriers.cs:44/45, :107/108 | `SetUpICardEffect`(내부에서 `SetNotShowUI(false)`) 직후 **무조건 `SetNotShowUI(true)` 덮어쓰기** — AS-IS 유일 실생산자 `EX7_014.cs:178-179`는 이후 호출이 없어 `IsNotShowUI=false`. 카드가 opt-out할 수단도 없음 | 미확인(`IsNotShowUI` 비-UI 소비자 유무 미확인) | 리뷰어 |
| O-183 | RestrictionCarriers.cs:44, :107 | `SetUpICardEffect`의 effectName이 **호출자 인자 → 하드코딩 리터럴**("Can't be selected by skill"/"Can't move"). AS-IS는 카드가 지정(EX7_014: "Can't move Digimon with 6000 DP or less") → 모든 인스턴스가 동일 이름. 부수: AS-IS 2단계 셋업(`SetUpCanNotMoveClass` 등) 무대응, 포트전용 `Card` 프로퍼티 추가, 클래스 `sealed` 추가 | 미확인 | 리뷰어 |
| O-184 | AddDigivolutionRequirement.cs:75 (뿌리: CardSource.cs:302-315/376) | `CardSource.CardColors` 타입이 `List<CardColor>`(열거형) → `IReadOnlyList<string>`으로 변경되어, 색 비교가 AS-IS의 직접 `.Contains(cardColor)`(:62) 대신 **AS-IS 무대응 헬퍼 `CardSource.ToCardColorList()`를 경유**. 그 헬퍼는 `Enum.TryParse` 실패 문자열을 **무음 드롭**(:308-311) → AS-IS에선 구조상 불가능한 "색이 그냥 없는 것으로 취급되는" 실패모드 신설. **O-153(HasCardColor)과 동일 뿌리(CardColors 문자열화)** | 미확인(카드 색 데이터에 파싱실패 문자열 존재 여부 미검증) | 리뷰어 |
| O-185 | CardEffectCommons.cs:1258-1265 (경유: KeyWordEffects/Blitz.cs:13) | **Blitz 발동 게이트가 재작성됨.** AS-IS(`KeyWordEffects/Blitz.cs:10-27`) 4항 중 2항 `cardSource.PermanentOfThisCard().CanAttack(activateClass)`(공격-적법성 전체 검사, 원인효과 포함)이 미러에선 **`!IsSuspended(...)` 한 줄로 축소**. 나머지 3항도 substrate 술어로 치환. 게다가 AS-IS-시그니처 오버로드(`Blitz.cs:13`)가 `activateClass`를 **버리고** 1-인자 버전에 위임하며, 그 주석은 *"AS-IS itself ignores activateClass in this gate (reads only the card/board state)"*라 적었으나 **AS-IS :14가 명백히 `CanAttack(activateClass)`로 전달** — 주석 허위(W-07 계통). `CardEffectCommons.cs:1255` 헤더는 "verbatim"이라 표기 | **LIVE** | 직접(양측 원문 대조) + 독립 리뷰어 1인 재확인 |
| O-186 | MindLink.cs:56 | **Tamer 언더카드 필터에서 `!IsFlipped` 제외항 삭제** — AS-IS(:25)는 `DigivolutionCards.Count(cs => cs.IsTamer && !cs.IsFlipped) == 0`(**뒤집힌** Tamer 언더카드는 선택을 막지 않음), 미러는 `Count(cs => cs.IsTamer) == 0`. 미러에도 `CardSource.IsFlipped`(:1288-1289)가 라이브 구현으로 존재하므로 substrate 제약이 아님. 유일한 Tamer 언더카드가 뒤집힌 상태인 디지몬이 AS-IS에선 MindLink 대상, 미러에선 제외 | **LIVE**(호출 카드 11장: BT14_086/087·BT15_086/087·BT16_086/087·BT17_086/091·BT20_089·BT24_086·EX11_070) | 리뷰어 |
| O-187 | MindLink.cs:110-118 | **배치 호출이 완전히 다른 클래스로 라우팅** — AS-IS(:77)는 `new IPlacePermanentToDigivolutionCards({_tamer, selected}, false, _activateClass).PlacePermanentToDigivolutionCards()`(선언: `CardController.cs:2838`, `_activateClass`를 cardEffect로 전달), 미러는 `Permanent.AddSourcesBottomAsync`/`MoveSourcesBottom`(선언: `Permanent.cs:5111/5235`)에 `causeSourceId`만 전달. AS-IS 클래스의 `CanNotBeAffected` 게이팅·`removeFieldPermanents`/`willBeRemoveField` 부기·`WhenRemoveField` 타이밍 패스(:2867-2958) 재현 여부 미검증. **F-02·O-173과 동일 계통(cause 소실)** | **LIVE** | 리뷰어 |
| O-188 | MindLink.cs:37-43 | 생성자가 `activateClass`를 `_ = activateClass;`로 **전량 폐기**(필드 미보관). AS-IS는 `_activateClass` 필드로 보관해 가드 5종(:40-44)과 배치 호출에 사용. 그 가드 5종(`_tamer.TopCard==null`·`_digimonCondition==null`·`_activateClass==null`·`EffectSourceCard==null`)이 전부 무대응 | **LIVE**(O-187의 뿌리) | 리뷰어 |
| O-189 | MindLink.cs:77-89 | AS-IS 외곽 게이트 `if (HasMatchConditionPermanent(CanSelectPermanentCondition))`(:48) 무대응 — 후보 0이면 AS-IS는 선택 UI 자체를 열지 않고 무동작인데, 미러 `BuildRequest()`는 후보 0에서도 `ChoiceRequest`를 무조건 생성(`maxCount: Min(1, 0)`) | 미확인(후보0 요청의 하류 취급 미확인) | 리뷰어 |
| O-190 | MindLink.cs:53-54/61-74/125-129 | **미러에 이미 이식된 공유 헬퍼를 쓰지 않고 사설 재구현** — AS-IS는 `IsPermanentExistsOnBattleArea`·`IsPermanentExistsOnOwnerBattleArea`·`HasMatchConditionPermanent`·`MatchConditionPermanentCount`(전부 `GameContextDeterminarion.cs`) 호출, 미러는 `TamerOnBattleArea()`/`OnTamerOwnersBattleArea()`/`Candidates()`로 `zones.GetCards()` 직접 순회. 미러 트리에 해당 헬퍼가 이미 존재함(`CardEffectCommons.cs:2063/2095/2363`, `Save.cs:24`)에도 미호출 | **구조** | 리뷰어 |
| O-191 | MindLink.cs:82-88 | `SelectPermanentEffect.SetUp`의 설정면이 `EffectChoiceHelpers.CreatePermanentRequest`로 대체되며 **인자 슬롯 자체가 소멸**: 상대용 메시지(`SetUpCustomMessage` 2번째 인자, AS-IS :67)·`canTargetCondition_ByPreSelecetedList`·`canEndSelectCondition`·`canEndNotMax`·`mode: Custom`·`cardEffect`(AS-IS :52-65). 부수: AS-IS 무대응 자기-대상 가드(:100-105) 추가 | **구조** | 리뷰어 |
| O-192 | Overclock.cs:58-61, :75-78 | AS-IS 공유 헬퍼 `CanSelectPermanentCondition(Permanent, string, CardSource)`(:6-11, 모듈 레벨 1개, :18/:30/:36에서 호출)가 미러에서 **삭제되고 두 메서드 안에 로컬 함수로 복제**됨. 현재 두 사본은 텍스트 동일이나 공유 지점이 사라져 이후 한쪽만 바뀔 수 있는 구조 | **구조**(현 시점 동작차 0) | 리뷰어 |
| O-193 | Raid.cs:120-121 | `SwitchDefender` 1번째 인자가 `activateClass`(효과 인스턴스) → `activateClass?.EffectSourceCard?.InstanceId`(소스카드 id). 수신측(`AttackProcess.cs:782`)이 그 id로 `BareCauseEffect.ForOrNull`을 새로 만들어 `hashtable["CardEffect"]`에 넣으므로, `OnBlockAnyone`/`OnAttackTargetChanged` 창이 보는 효과가 **원본 `activateClass`가 아님**(`IsInheritedEffect`·`RootCardEffect`·등록 정체성 소실). **F-02 수리의 잔여 결함이자 O-173과 동일 계통** | **LIVE** | 리뷰어 |
| O-194 | Raid.cs:66/90/101-104 | AS-IS 시그니처 이탈 3종: ① `CanActivateRaid(Permanent)` → `(CardSource, Permanent)`, ② `RaidProcess(Permanent, ICardEffect)` → `(CardSource, Permanent, ICardEffect, CancellationToken=default)` — CT는 AS-IS 무대응 중단 경로 신설, ③ maxCount 도출이 AS-IS `Math.Min(1, MatchConditionPermanentCount(...))`(:52-54) → `HasMatchConditionPermanent` 존재검사 + 리터럴 `maxCount: 1`. **미러 트리에 `MatchConditionPermanentCount` 이식본이 있고 다른 포팅 카드들(BT9_009/021/062·EX8_059)은 AS-IS 패턴 그대로 사용 중** — 이 파일만 이탈. 부수: 상대용 메시지 슬롯 소멸(O-191과 동일) | **구조**(후보0에서 경로 상이, 결과 동일) | 리뷰어 |
| O-155 | CardEffectFactory/KeyWordEffects/Partition.cs:26-27 | `PartitionCondition.Color`/`Color2` 타입이 `CardColor`(enum) → `string?`. AS-IS enum 필드는 미사용 시에도 기본 멤버값을 갖는데 포트는 null 가능. 포팅된 카드 호출부가 `CardColor` 멤버명과 정확히 일치하는 문자열을 넘기는지 미표본(→ O-153 도달성·선택 프롬프트 텍스트에 연동) | 미확인 | 리뷰어 |
| O-156 | CardEffectFactory/KeyWordEffects/Partition.cs:23 | `PartitionCondition.PartitionConditionsKey` — AS-IS 무대응 `public const string` 신설 | 경로없음 | 리뷰어 |
| O-157 | PermanentEffectFactory.cs:22-31 | **`CanNotSwitchAttackTargetEffect`의 `!targetPermanent.TopCard.CanNotBeAffected(activateClass)` 게이트 전삭** — 포트는 `activateClass`를 옵셔널로 받고 `_ = activateClass;`로 폐기. AS-IS(:115-120)는 CanUse 3항 중 1항. 대상이 그 효과에 면역을 얻어도 "공격대상 변경불가" 제약이 계속 걸림. 라이브 호출부 `AD1_011.cs:164`(non-null 전달) | **LIVE** | 직접 |
| O-158 | PermanentEffectFactory.cs:120-131 | **`CollisionEffect`의 `!TopCard.CanNotBeAffected(activateClass)` 게이트 전삭**(동일 패턴). 포트 주석은 *"self grant에선 vacuous, no port surface"*라 적었으나 **거짓** — 라이브 호출부 `Collision.cs:26 GainCollision`이 non-null 가드(:21-22) 후 전달하고, **같은 파일 `AddDetailClass:219`가 동일 형태로 `CanNotBeAffected(activateClass)`를 정상 호출** 중 | **LIVE** | 직접 |
| O-159 | PermanentEffectFactory.cs:34/127/210, CanNotBlock.cs:69 등 | **`Permanent` 동일성 판정이 참조동등 → `InstanceId` 동등으로 교체**. AS-IS `Permanent`는 가변 객체(`cardSources` 성장) → 같은 스택이 디지볼브해도 `permanent == targetPermanent` 유지. 포트 `Permanent`는 불변 스냅샷이고 자체 주석이 "top이 바뀌면 새 InstanceId"라 명시 → 부여 후 재-디지볼브 시 grant가 무효화. **공유 인프라 뿌리**(형제 파일 다수 동일 패턴) | 미확인(재-디지볼브 시나리오 미실증) | 리뷰어 |
| O-160 | PermanentEffectFactory.cs:34 | AS-IS `PermanentCondition`의 `permanent.TopCard &&` 항(TopCard 존재 검사) 누락 | 미확인 | 리뷰어 |
| O-161 | PermanentEffectFactory.cs | AS-IS에 없는 `ArgumentNullException.ThrowIfNull` 4곳 추가(:24/54/87/123)하고 `DeleteSelfEffect`/`AddDetailClass`엔 미추가 — 같은 파일 안에서도 불일치. AS-IS는 클로저 실행 시점의 지연 `NullReferenceException` | 경로없음 | 리뷰어 |
| O-162 | PermanentEffectFactory.cs:120 | `CollisionEffect` 반환형 `CollisionClass` → `ICardEffect`로 확대(kind-class 전용 멤버 접근 불가) | 경로없음 | 리뷰어 |

### B-12. 전수 diff census (2026-07-29, 기계 측정)

출처: `fulltree_diff_census.md`. 주석 삭제(`tools/CommentStripper`)가 노출시킨 것.

| ID | 영역 | 결함 | 도달성 | 검증 |
|---|---|---|---|---|
| O-195 | CutInProcess | **파일 전체 미포팅** — AS-IS 110줄인데 TO-BE엔 코드가 전무하고 전부 TO-BE 작성 STOP 사유 주석뿐이었음. 주석 삭제 후 0바이트. **주석이 미포팅을 은폐하고 있었음**(W-07 계통의 새 실증) | 미확인 | 직접 |
| O-196 | SelectCardPanel | **파일 전체 미포팅** — AS-IS 688줄, 동일 패턴으로 0바이트. 위와 같음 | 미확인 | 직접 |
| O-197 | 전역(650파일) | **네임스페이스 체계가 AS-IS와 전면 불일치 — 단일 뿌리, diff 10,805행(11%)** [실측]. AS-IS는 650파일 중 **128개만** namespace를 갖고(`DCGO.CardEffects.<세트>`) 나머지 522개는 전역 namespace다. TO-BE는 **648개**가 namespace를 갖고, 이름을 **디렉터리 경로에서 기계 생성**한다(`HeadlessDCGO.Engine.Assets.Scripts.<경로>`). ① AS-IS에 namespace가 없는데 TO-BE엔 있는 파일 **520개** ② 나머지도 이름이 다름(`DCGO.CardEffects.BT25` vs `...Assets.Scripts.CardEffect.BT25.*`). 파생: TO-BE 전용 `using` 다수가 이 체계 때문에만 존재. **수리 시 전역 namespace 이동에 따른 substrate/BCL 이름 충돌 검토 필요** | **구조** | 직접 |

| O-198 | ICardEffect / 카드층 61파일 | **`EffectTiming.WhenDigivolving` 발명 — AS-IS enum에 없는 멤버, 두 방언 공존** [실측]. AS-IS `EffectTiming` enum은 **61멤버**이고 TO-BE는 **62멤버**, 차이는 이것 하나뿐(누락 멤버 0). AS-IS는 `EffectTiming.WhenDigivolving`을 **0회** 쓰고, "디지볼브 중" 판정을 런타임 술어 `CanTriggerWhenDigivolving`(**1,326회**) + `ICardEffect.IsWhenDigivolving`(:772)로만 한다. TO-BE는 타이밍 enum에 멤버를 신설해 **트리거 디스패치 자체를 분리**했다(`EffectTiming.WhenDigivolving` 66회 / 61파일). 게다가 AS-IS 방식(`CanTriggerWhenDigivolving` 83회 / 77파일)이 함께 살아 있고 **55파일이 두 방식을 동시에 사용** = 두 방언 공존. [[asis-uniform-activateclass]]에 기록된 "AS-IS의 uniform 메커니즘을 (액션×타이밍)별로 파편화" 병리의 실례 | **구조**(카드층 61파일 파급) | 직접 |

| O-199 | AutoProcessing / Player | **턴종료 메모리 리셋이 좌석 분기를 잃음 — 뿌리는 `Player.PlayerID` 부재** [실측]. AS-IS `AutoProcessing.cs:683-690`은 Main 페이즈 패스 종료 시 `TurnPlayer.PlayerID == 0 ? Memory = 3 : Memory = -3`으로 **좌석에 따라 부호를 갈라** 리셋한다(메모리는 부호로 소유 좌석을 표현). TO-BE `AutoProcessing.cs:1222`는 분기 없이 **항상 `Set(-3)`** — 한쪽 좌석의 리셋이 반대 부호가 된다. 원인: AS-IS `Player.cs:738 public int PlayerID { get; set; }`가 **TO-BE `Player.cs`에 아예 없다**(TO-BE 트리 전체 `PlayerID` 출현 1회, `PlayerID == 0` **0회**). [[substrate-repair-tracks]] 미결③이 예고한 "PlayerID를 int 좌석 인덱스로 복원" 선행조건의 실제 발현 | **LIVE** | 직접 |
| O-200 | AutoProcessing / TurnStateMachine | **AS-IS 메서드 2종이 트리 어디에도 미포팅** [실측]. `ShrinkSecurityDigimonDisplay()` AS-IS 10회 / TO-BE **0회**(호출부 2곳 모두 소실), `TurnStateMachine.SetMainPhase()` AS-IS 15회 / TO-BE **1회**(이 파일 호출부 소실). 부수: TO-BE `AutoProcessing.cs:1251-1253`에 본문이 빈 `if (TurnPhase == Main) { }` 잔해가 남아 있음 | **구조** | 직접 |

전수 diff가 함께 드러낸 것(개별 ID 미부여, census 문서 참조):
- **AS-IS에만 존재하는 파일 88개** = 미포팅 파일 (`fulltree_asis_only.list`)
- **TO-BE에만 존재하는 파일 95개** = 발명/오배치 (`fulltree_tobe_only.list`)
- **완전 동일한 페어 0개** (4,266 페어 전체)
- 실질 모집단 650파일의 차이 98,094행(자명한 줄 제외) 중 **61%가 기존 substrate 15뿌리로 설명되지 않음**.
  잔여는 상위 50파일에 52% 집중 — 전부 코어(`CardController`·`CardSource`·`Permanent`·`CardEffectCommons`·`TurnStateMachine`)

---

## C. 구조적 부채 (WATCH)

| ID | 내용 |
|---|---|
| W-01 | **행동 오라클 부재** — 스위트 폐기로 회귀 탐지 수단 0. 이 대장의 결함 전부가 빌드 0·grep 0을 통과했음 |
| W-02 | `DeferredChoiceProvider` replay 모델 존치(펌프 밖 경로용) — 발명물로 판정됐으나 미은퇴 |
| W-03 | 경고 신호 33건: CS0414 미사용 필드 21(`_notShowCards`·`_isDeckBottom` 등 = 배선 누락 흔적 가능), CS8321 미사용 로컬 4, CS1998 7 |
| W-04 | `GameEventQueue` 휴면(프로듀서 0), batch-id 프로듀서 dead 스캐폴딩 |
| W-05 | `AttackProcess.SnapshotCardSources` 순서 역전(현재 write-only) |
| W-06 | **미검토 표면**: CardEffectCommons·CardSource·GameContext·Select*Effect 내부·AutoProcessing 내부·DrawClass/IDiscardHands/IDestroySecurity·CardEffectFactory·키워드 효과·펌프(TurnFlowPump/Driver)·카드 ~600장 |
| W-07 | **미러 헤더 주석의 ADAPTATION 기술이 코드와 불일치** — `ChangeSAttack.cs`·`ChangeDP.cs` 두 파일 모두 헤더가 "`CanNotBeAffected(<ICardEffect>)`를 `EffectSourceCard?.InstanceId`로 바꿨다"고 적었으나 실제 statement는 AS-IS와 바이트-동일. `PermanentEffectFactory.cs`는 반대로 "게이트가 vacuous·no port surface"라 적었으나 실제로는 게이트가 **누락**(O-158). `CanNotUnsuspend.cs:71-72`는 "`GainCantUnsuspendNextActivePhase` 라이브 호출자 없음"이라 적었으나 `EX4_013.cs:281/410`이 실호출. 주석이 실제보다 나쁘게/좋게 양방향으로 틀림 → 감사·수리 판단에 주석 사용 금지 재확인 |

---

## 운용 원칙
1. **`경로없음`은 "안 고쳐도 됨"이 아니다.** 이 세션에서 잠복 판정이 최소 3회 틀렸다(F-06 → BT7_087/EX11_004, F-04 → BT25_096). 특히 카드 포팅 경로 위의 항목(O-07 등)은 라이브와 동급으로 취급한다.
2. **`미확인`은 리뷰어 보고를 그대로 옮긴 것**이다. 수리 착수 전 AS-IS 원문 직접 대조를 선행한다.
3. 수리 시 이 대장의 ID를 커밋/보고에 인용하고, 상태를 갱신한다.

---
