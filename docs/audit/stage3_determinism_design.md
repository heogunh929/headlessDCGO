# 단계 3 · 결정성 설계 — 같은 시드 → 같은 트래젝토리

2026-07-29 수립. 전제: 자기대전 전환 완료(커밋 164308d9) — AS-IS 상대 AI의 난수 소비가 사문화된
최종 형상에서 설계한다. **주의: A/B 다이제스트는 불변성(비결정성 부재)만 보증한다. 정확성 오라클이 아니다.**

## 게이트

같은 (매치 시드, 덱 쌍, 정책 시드)로 2회 실행 → 트래젝토리 다이제스트 완전 일치.
- 동일 프로세스 내 2회 **그리고** 프로세스 분리 2회(해시 랜덤화·주소 의존을 잡는 건 후자다)
- 3덱 자기대전 + 크로스덱 3조합 × 각 N판, 불일치 0

## 난수·비결정 원천 지도 [실측 2026-07-29]

| # | 원천 | 성격 | 조치 |
|---|---|---|---|
| 1 | **GameRandom** (Script/GameRandom.cs, Xoshiro256**) | 게임플레이 난수 전부(셔플·확률). **카드층 난수 사용 0** | 시드 주입만 하면 됨 — §시드 주입 |
| 2 | GameRandom의 시드원 `GetSecureRandom()` (OS 엔트로피) | CC 초기화 2곳 + TSM:224 `SetRandom` RPC. 마지막 Seed가 이김 | 하네스 재시드로 덮음 |
| 3 | UnityEngine.Random 심 (`Source = new System.Random()` 비시드) | 엔진층 12곳: AIモード·상대석 자동응답(자기대전으로 **사문**) / 연출 2곳(SecurityBreakGlass:121, BGM TSM:295) / DeckDatas 선택 CardObjectController:37(1개 컬렉션이면 Range(0,1)=0 **결정적**) / 샘플덱 폴백 :80,:88(크로스덱 공급 시 안 탐) | 심 Source를 시드 가능하게 + 사문 여부 실측으로 확증 |
| 4 | `IEnumerableExtension.GetRandom` — **호출마다 `new System.Random()`** | Select{Card,Permanent,Hand}Effect 4곳 — 선택기 자동응답 분기로 추정 | **사문 여부 실측 필수** (미확증). 살아있으면 비결정 원천 1순위 |
| 5 | `Guid.NewGuid` DeckData:176 | 덱ID 문자열 생성(코스메틱) | 무해. 단 다이제스트에 덱ID 포함 금지 |
| 6 | **`CardSource.ChangedLocationTime = DateTime.Now`** (CardSource:132) | "타임스탬프 지속효과"의 시작 판정 — **룰 실동** (CardController:4897,5074,5940 / CardObjectController:1100) | §시간 참조. 위험 1순위 |
| 7 | `Player.TurnStartTime = DateTime.Now` | 판독처 없음(제한시간 표시용 추정) | 무해 추정, 게이트가 재판정 |
| 8 | `GetInstanceID` = 주소 해시 | **판정 사용처 0** (엔진·카드층) | 무해. 다이제스트에 포함 금지 |
| 9 | 정책 RNG (RandomVirtualPlayer) | 시드 분리 완료(2n/2n+1) | 완료 |
| 10 | 컬렉션 순회(Dictionary/HashSet/동률 OrderBy) | 미조사 | §순회 census |

## 시드 주입 설계

**핵심 관찰**: AS-IS 스스로 `GameRandom.Seed(long)` 단일 지점으로 수렴시켜놨다. 초기화 시드 2곳은
매치 시작 시 `SetRandom` RPC가 덮고(TSM:224→CC:1329), 첫 게임플레이 소비(덱 셔플)는 그 완료 대기
(TSM:227 `DoneSetRandom`) 이후다. 따라서 **SetRandom 완료 직후·셔플 전에 한 번 재시드**하면 전 소비가
결정화된다.

3안 비교:
- A. RPC 디스패치에서 SetRandom 인자 치환 — substrate가 RPC 의미를 위조. 기각
- B. 미러 GetSecureRandom 변환(AsIsSync 규칙) — 변환 최소 원칙 위배, 불필요. 기각
- **C. 하네스 재시드 (채택)** — MatchSmoke 틱 루프가 `CC.instance.DoneSetRandom == true`가 된 틱을
  감지(SetRandomCoroutine이 세우고 TSM은 다음 틱에 소비하므로 틱 경계에서 관측 가능) →
  `GameRandom.Seed(matchSeed)` 1회. 미러·substrate 무변경, 하네스 소관. RL 하네스 공용화를 위해
  substrate 헬퍼(`Headless/Determinism/MatchSeed.cs` 등)로 빼되 호출은 하네스가 한다.

UnityEngine.Random 심도 같은 시점에 시드(사문 확증 전 보험 + 연출 2곳의 A/B 소음 제거).

## 시간 참조 (§원천 6)

`ChangedLocationTime` 위험 분해:
- 프로세스 간: 절대값은 다르지만 판정이 **상대 비교**면 순서 보존 → 무해. 비교 연산 사용처 정독 필요
- 동일 프로세스: DateTime.Now 해상도(~15ms)에서 같은 틱 내 두 이동이 **동률** 가능 → 동률 시 하위
  판정(순회 순서)으로 갈라짐 → 비결정 위험 실재
- 조치 후보(정독 결과에 따라): ① 판정이 순서 비교뿐이고 동률 처리가 안정적이면 무조치 등재
  ② 아니면 AsIsSync **선언된 변환**으로 `DateTime.Now` → substrate 논리 시계(틱 카운터 기반 단조 시각).
  변환 최소 원칙의 정당한 탈출구 — BCL이라 substrate가 이름으로 흡수 불가

## 순회 census (§원천 10)

엔진층 한정(카드층은 난수 0이므로 후순위): `Dictionary<`/`HashSet<` 필드의 foreach 순회가 판정에
닿는 곳, `OrderBy` 동률 키(불안정 정렬), `GroupBy`/`Distinct` 순서 의존. 산출물: 사이트 목록 + 무해/위험 판정.

## 트래젝토리 다이제스트

- **무엇을**: 존 이동 이벤트 스트림(틱, 카드 CardID+ordinal, from→to)과 턴/페이즈 전이, 메모리 변화,
  경기 결과(승자·종료 사유·총 틱). FNV-1a 등 안정 해시로 접기. 러닝 다이제스트 + 최종값
- **금지**: GetInstanceID·덱ID·DateTime·객체 해시 등 프로세스-로컬 값 일체
- **어디서**: 존 이동의 단일 초크포인트를 census 후 하네스가 후킹(이벤트/폴링). 미러 무변경 —
  MatchSmoke 틱 루프에서 존 스냅샷 diff로 시작(초크포인트 불명확 시 안전한 차선)
- **모드**: `runner N DECK --digest` → 판별 다이제스트 출력, A/B 비교는 셸에서

## 작업 분해 — 실행 결과 (2026-07-29 밤)

1. **완료 [실측]** 사문 확증: 16곳 전원 사문(isYou else→`#region AI`) / 무해(연출 2) / 결정적(Range(0,1))
   — 전제: 자기대전 배선 + 크로스덱 공급. GetRandom 4곳도 전부 AI 분기 안 → 사문
2. **완료 [실측] — 갈림길 소멸**: `ChangedLocationTime`·`TurnStartTime` **판독처 0/전수 9** —
   기록만 되고 판정에 안 읽힘. 무조치 등재, 논리 시계 변환 불필요
3. **완료** `Headless/Determinism/MatchSeed.TryPin` — DoneSetRandom 관측 틱에 GameRandom+
   UnityEngine.Random(심 InitState 실동화) 재고정. 셔플(CreatePlayerDecks, TSM:233)이 그 뒤임을 확증
4. **완료** FNV-1a 러닝 다이제스트(상태 변화 틱마다 CardIndex 서열·존·메모리·페이즈 접기,
   프로세스-로컬 값 배제) + `--digest`
5. **완료 [실측]** 순회 census: 순서→판정 경로 0건 — CardPermanenceMap 유일 순회(:14)는 키별
   독립 갱신(순서 무관), GamePacketFactory 조회 전용, DataBase enum-키 정적 테이블, **카드층 0**
6. **게이트 통과 [게이트]**: 예비 A/B/C(프로세스 분리 시드 5 + 시드1 단독 잔여상태 독립성) 전부
   일치 → 본게이트 **6구성(ST1·ST2·ST3 자기대전 + 크로스 3조합) × 8판 × 프로세스 분리 A/B =
   다이제스트 96/96 일치, 완주 96/96, 불일치 0**. 시드1 단독 일치는 teardown의 static 청소가
   프로세스 내 매치 간 독립성까지 보증함을 함께 실증
