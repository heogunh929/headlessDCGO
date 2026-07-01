# AS-IS 충실도 감사 — 이번 세션 키워드 구현 (포팅: 게임 룰 불변 원칙)

- 작성일: 2026-06-28
- 원칙: **포팅이므로 게임 룰이 바뀌면 안 된다.** 자동해소/자동선택은 플레이어 결정을 박탈 → 룰 변경.
- 기준 데이터: 원본 `SetUpActivateClass(..., bool optional, ...)` 4번째 인자(true=optional "you may", false=mandatory) + `Process`의 `canNoSelect`/sub-selection.

---

## 1. AS-IS optional/mandatory 원장 (확정)
- **optional(true)**: Evade, Barrier, Decoy, ArmorPurge, Raid, Fragment, Scapegoat, Save, Decode, Partition, Overclock, Vortex, MaterialSave, Execute.
- **mandatory(false)**: Fortitude, Retaliation, Ascension*, Alliance*, Training.
  - *Ascension: trigger는 mandatory지만 Process에 **Yes/No 유저 선택** 존재 → 실질 플레이어 결정.
  - *Alliance: trigger mandatory지만 Process에 `canNoSelect:true` 대상 선택 → 선택 안 함 가능.

## 2. 세션 구현 룰-변경 감사

| 키워드 | AS-IS | 내 구현 | 룰 변경? | 유형 |
|--------|-------|---------|:--:|------|
| C-1 Rush | passive | 소환멀미 플래그 우회, 공격=legal-action | **없음** ✓ | — |
| C-2 Blitz | — | MemoryPass 공격을 legal-action으로 노출(agent 결정) | **없음** ✓ | — |
| C-10 Collision | 강제 블록(키워드 효과) | 강제 블록 + 블로커 선택은 방어자 | **없음** ✓ | (강제가 키워드 본질) |
| C-8 Retaliation | mandatory | 자동 상대 삭제 | **없음** ✓ | mandatory 일치 |
| C-6 Fortitude | mandatory | 자동 replay | **없음** ✓ | mandatory 일치 |
| C-9 Execute | (자기삭제는 mandatory 번들) | 플래그 기반 자기삭제, 공격=legal-action | **경미** | 발동 optional은 grant(포팅) |
| **C-7 Evade** | **optional** | 자동 서스펜드+생존 | **🔴 있음** | 강제 발동 |
| **C-5 Barrier** | **optional** | 자동 시큐리티 trash | **🔴 있음** | 강제 발동 |
| **C-4 Decoy** | **optional** | 자동 + 첫 Decoy 희생 | **🔴 있음** | 강제+자동선택 |
| **C-21 ArmorPurge** | **optional** | 자동 shed+승격 | **🔴 있음** | 강제 발동 |
| **C-3 Raid** | **optional** | 자동 전환 + 첫 최고DP | **🔴 있음** | 강제+동점선택 |
| **C-11 Fragment** | **optional** | 자동 + 가장 깊은 N 소재 | **🔴 있음** | 강제+자동선택 |
| **C-19 Scapegoat** | **optional** | 자동 + 첫 아군 희생 | **🔴 있음** | 강제+자동선택 |
| **C-17 Ascension** | mandatory + **Yes/No** | 자동 시큐리티行 | **🔴 있음** | Yes/No 제거 |
| **C-22 Save** | **optional** | 자동 + 첫 permanent 부착 | **🔴 있음** | 강제+자동선택 |
| C-23 Material Save | optional | 프리미티브만(호출자 결정) | N/A | 미배선 |
| C-24 Training | mandatory | 프리미티브만 | N/A | 미배선 |

### 결론: **9개 키워드(Evade·Barrier·Decoy·ArmorPurge·Raid·Fragment·Scapegoat·Ascension·Save)가 룰을 바꾼다.**
- (a) optional을 **강제 발동**, 그리고/또는 (b) sub-selection을 **자동 선택**.
- 추가 누락: ArmorPurge `WhenTopCardTrashed` 타이밍 미발화.

## 3. 근본 원인 (아키텍처)
- 내 `DeletionReplacementGate`/`RaidAttackSwitch`는 **하드코딩 자동 소비** = optional 트리거 시스템 우회.
- **충실한 모델**: 이 키워드들은 해당 윈도우(예: WhenPermanentWouldBeDeleted=F-6.8)에서 **optional 트리거**로 수집 → `OptionalPromptQueue`로 agent 결정 노출 → 활성 시 effect 해소(sub-selection은 `ChoiceController` 선택) → `willBeRemoveField=false`로 삭제 취소.
- 엔진엔 이미 `OptionalPromptQueue` + optional-trigger 라우팅이 있음(`GameFlowProcessor.QueueOptionalPrompts`). 다만 **삭제 경로가 동기적**이라 "삭제 직전 멈춰서 선택을 묻고 결정에 따라 재개"하는 **재진입 삭제(F-6.8)**가 미구현.

## 4. 수정 아키텍처 (step 1·2)
### 옵션 A (충실·정공법, 권장): F-6.8 재진입 삭제-대체 윈도우
- BattleResolver/ApplyDelete의 삭제를 "삭제 후보 확정 → WhenWouldBeDeleted 윈도우 open(optional 트리거 수집) → 선택 pause/resume → 결과 적용"으로 전환.
- 삭제-대체 키워드(Evade/Barrier/Decoy/ArmorPurge/Fragment/Scapegoat/Save/Ascension)를 그 윈도우의 트리거로 등록(현 자동 소비 제거).
- sub-selection(어느 소재/어느 아군/어느 대상)을 `ChoiceController` 선택으로.
- Raid는 OnUseAttack optional 트리거 + 대상 선택으로.
- **규모 큼**(재진입 삭제 + 8키워드 재배선 + 핫패스). 회귀 위험 → 전체 스위트 필수.

### 옵션 B (경량 스톱갭): 자동해소 유지하되 룰-변경임을 명시
- 현 상태 유지 + LIMITATION을 "룰 변경(포팅 부적합)"으로 강등 표기.
- **포팅 원칙 위반** → 사용자 원칙상 비권장.

## 5. 권장 진행
1. **(완료) 감사** — 본 문서.
2. **F-6.8 재진입 삭제-대체 윈도우 설계 + 구현**(옵션 A) → 9개 키워드 재배선(자동→선택).
3. ArmorPurge 등 누락 타이밍 발화 추가.
4. 재검증(전체 스위트 + 키워드별 optional/선택 테스트) 후 **기능 개발 복귀**.

> 주의: 옵션 A는 사실상 S-시리즈와 같은 "선결 서브시스템(F-6.8)"이며, 이걸 먼저 깔아야 삭제-대체 키워드가 룰-충실해진다. 즉 **C-그룹2/4/5의 삭제계 키워드는 F-6.8 위에 재구축**되어야 한다.
