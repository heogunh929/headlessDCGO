# isYou 판독처 전수 census — 양석 isYou=true(자기대전 seam화) 사전 조사

2026-07-29. AS-IS `DCGO/Assets/Scripts/` 전수. **80건 / 엔진층만 (카드층 0건)**.
목적: 씬 배선에서 양 좌석 모두 `isYou=true`로 세울 때 (AI 분기 `IsAI && !isYou` 무력화 → 양석 seam 구동)
"정확히 한 석만 you"를 전제하는 룰 경로가 있는지 확인.

## 결론: 차단 사유 없음

`Players.First(p => p.isYou)` 같은 유일-you 조회는 0건. 80건 전부 if-분기이며 아래 5부류로 소진된다.

## 분류

### ① 표시·연출·입력 배선 — 무해 (약 50건)
ShowPhaseObject:82 · ShowTurnPlayerObject:20 · BrainStormObject:112 · CardInfo:144 ·
FieldPermanentCard:282,326 · HandCard:410,497,977 · SecurityObject:56 ·
Effects:241,385,771,850,1195,1232,1662,1822,1891,1901,2201,2213 ·
Draggable_HandCard:30,75,110,159,190 (드래그 가드 — headless 무클릭) ·
NextPhaseButton:48,106,156,179 (사람용 페이즈 버튼 — 페이즈 진행 본선은 PassAction→EndTurnProcess) ·
CardObjectController:665,681,706 · CardSource:2303,2339 (PreferredFrame 화면정렬, E-01) ·
CardController:1586,1603 (재배치 연출) · TurnStateMachine:214(이름표),348(주석),379(안내문),1424(클릭 배선) ·
Player:377(트래시 이미지),464(핸드 정렬),558(플레이매트 파일명) · ICardEffect:1167(효과 설명 표시) ·
SelectCardEffect:701,984 · SelectHandEffect:652,905 (카드 공개 표시 — ShowCard 경로 틱만 증가)

### ② 좌석 라우팅 — 질문을 로컬로 여는 분기. 양석 true = 양석 질문이 선택 채널로 옴 (원하는 변화, ~15건)
SelectCardEffect:383 · SelectHandEffect:188 · SelectPermanentEffect:295,777,800,824 ·
SelectCountEffect:116 · UserSelectionManager:109,163 · MultipleSkills:189,266 · OptionalSkill:60 ·
SelectDigiXrosClass:480 · CheckCardPanel:285 · SelectAttackEffect:230 · TurnStateMachine:384(멀리건),725(부화)
— 파킹은 단일 스레드 순차라 패널 동시 개방 충돌 없음. 기존 채널이 좌석 식별 후 해당 PlayerID로 응답.

### ③ AI 분기 게이트 — 무력화가 목적 그 자체 (5건)
TurnStateMachine:981 (`!IsAI || isYou` → 양석 큐 dequeue) · :990 (`IsAI && !isYou` AIモード → 사문) ·
SelectDigiXrosClass:557 · CardController:520 (AI석 코스트 다이얼로그 스킵 → 이제 채널이 응답) ·
DNADigivolveEffects:557 (`isYou || IsAI` — 불변)

### ④ `|| IsAI` / isAuto 사문 — 불변 (5건)
Player:712 (PlayerName) · TurnStateMachine:214 · TurnStateMachine:763,1156 · CardController:527 (isAuto=false 사문)

### ⑤ 룰 표면 변화 — 의도된 변화, 반드시 인지할 것 (3건)
- **CardSource:679, :714** — `//AI` 주석 분기: AS-IS는 AI석(isYou=false·IsAI)의 **코스트 감면 사용을 차단**
  (감면 다이얼로그에 AI가 답할 수 없어서). 양석 isYou=true면 상대석도 감면을 씀.
- **CardSource:783** — `checkAvailability && IsAI`에서 상대 퍼머넌트 대상이면 비용 변경 효과 계산을
  생략하고 원가 반환하는 **AI 가용성 근사**. 양석 isYou=true면 근사 해제, 정식 계산.

셋 다 방향은 같다: **양석이 You석(=사람) 룰 표면으로 통일**된다. vs-AI 기준선 대비 대전 분포는
달라지지만, 자기대전(RL)의 요구가 바로 그것이다. AI석 차단은 AS-IS의 의도적 핸디캡이었다.

## ② 구현 시 변경점 (참고)
- `HeadlessScene.BuildPlayer`: Opponent도 `isYou=true` (씬 배선 = substrate 소관, 미러 무변경)
- 좌석별 `RandomVirtualPlayer` 2개(시드 분리), MatchSmoke 배선
- 검증: 스타터 3덱 × 50판 게이트 재실행
