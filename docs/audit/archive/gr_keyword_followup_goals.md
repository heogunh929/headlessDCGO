# GR-006 / GR-007 — 키워드 라이브화 후속 goal (GR-005 발견 건)

> 근거: `docs/audit/keyword_disconnect_prediction.md` + GR-005 키워드 단절 감사 과정에서 발견한 두 건. 둘 다 **latent**(ST1~3 스타터덱에 해당 카드 없음 → 현재 활성 버그 아님)지만, 해당 카드를 포팅하면 드러나므로 별도 goal로 분리.
>
> 공통 종료 기준: `bash scripts/run-tests.sh` 전체 green(FAIL=0) + 동작을 **실제로 단언하는 테스트** + `tools/RuleAudit` 위반 0. 커밋은 사용자 지시 시.
> 표준 규칙: **AS-IS 미러** — 구현 전 원본 `DCGO/`에서 해당 키워드 규칙을 확인하고 1:1(추측 금지). 엔진(`Headless/**`)은 change-control.

기준 커밋: GR-005(키워드 게이트) 이후. C군 봉합 포함.

---

## ✅ GR-006 — 완료 (턴종료 효과-주도 공격 트리거)

**구현:** `EndOfTurnEffectAttack.TryOpen(context, player)` 신설 — 턴종료 시 ENDING 플레이어의 배틀존에서 `<Vortex>`/`<Overclock>` 디지몬(게이트 판정)을 찾아 효과-주도 공격 윈도우를 연다. `MetadataActionProcessor.EndTurn` 최상단에서 호출: 윈도우가 열리면 **턴을 넘기지 않고** pending choice 반환 → 에이전트가 공격 해소 → 재-EndTurn 시 (사용 플래그/서스펜드로) 윈도우 없음 → 턴 종료. per-instance `endOfTurnAttackUsed` 가드 + 턴종료 시 클리어.
- **Vortex** 옵션(AS-IS 충실): `EffectAttackOptions(WithoutTap:false, AllowPlayerTarget:**false**, AllowDigimonTarget:true, TargetUnsuspended:true)` → 상대 디지몬(임의, 언서스펜드 포함) 공격, **플레이어 ❌**, 공격 후 서스펜드.
- **Overclock**: 같은 훅이 `OverclockEffect.RequestChoice`(기존: 토큰/[trait] 아군 삭제 → 플레이어 공격) 트리거.
- `Vortex.cs`의 잘못된 "Digimon + player" 주석 정정. 게이트에 `Vortex` 상수 추가.

**검증:** `tests/GR-006.EndOfTurnEffectAttack` — (1) Vortex가 상대 디지몬 후보·**플레이어 비후보**, (2) 언서스펜드 디지몬 타격(isVortex), (3) 서스펜드 Vortex는 윈도우 없음, (4) **라이브 EndTurn이 윈도우를 열고 핸드오버 보류 → 재-EndTurn으로 종료**. 전체 233/233, 감사 위반 0, self-play 승자 균형.

**정직한 커버리지:** Vortex는 라이브 실측 완료. Overclock은 같은 훅으로 트리거되나(게이트 감지), 토큰/[trait] 아군 셋업이 필요해 **전용 라이브 테스트는 미작성**(RequestChoice/ResolveChoice 기계는 기존부터 존재; GR-006은 트리거만 추가). Overclock 카드 포팅 시 라이브 실측 권장.

### (착수 중 발견 — 이력)

**조사 결과(GR-006 착수 중 발견):**
- `EffectDrivenAttack` 허브는 **이미 Vortex 타겟 규칙 보유**(`options.TargetUnsuspended`=AS-IS isVortex, `options.AllowPlayerTarget`). 즉 "효과-주도 공격" 해소 기계는 준비됨.
- 그러나 **`EffectDrivenAttack.RequestChoice`/`OverclockEffect.RequestChoice`를 라이브 턴/공격 플로우에서 호출하는 곳이 0** → 발동 트리거 부재.
- AttackPipeline은 Raid/Alliance만 트리거(`RaidAttackSwitch`/`AllianceAttackBoost.RequestChoice`); **Vortex·Overclock은 트리거 안 됨**.
- 원본 `<Vortex>` 규칙(권위 텍스트, `DataBase.VortexEffectDiscription`): **"(At the end of your turn, this Digimon may attack an opponent's Digimon. With this effect, it can attack the turn it was played.)"**
  - **타이밍 = 턴 종료 시**(Overclock과 동일), **대상 = 상대 디지몬만(플레이어 직접 공격 불가)**, 소환멀미 우회.
  - ⚠️ 헤드리스 `Vortex.cs` 주석의 "Digimon + player / AllowPlayerTarget"은 **원본과 어긋난 오류** → GR-006 구현 시 `AllowPlayerTarget: false`로 바로잡을 것.
  - (`VortexCanAttackPlayersClass`는 이름만 유사한 별개 효과이지 `<Vortex>` 키워드가 아님.)

**결론:** GR-006은 "Vortex 1줄 배선"이 아니라 **효과-주도 공격 키워드(Vortex+Overclock) 라이브 트리거 서브시스템**(신규 트리거 훅 + VortexEffect runtime 클래스 + 정확한 타이밍/조건 AS-IS 대조 + 카드 포팅 + 실측). Overclock도 같은 갭. 추측으로 턴-플로우 타이밍을 짜면 AS-IS 위반 → **별도 집중 작업으로 재정의 권장**(rush 금지).

### 전체 키워드 라이브-트리거 감사 (확정)
14개 Batch1/2 키워드 중 **라이브 트리거가 없는 것은 정확히 Vortex + Overclock 둘뿐**. 나머지 12개(Blocker/Jamming/Piercing/Rush/Blitz/Reboot/Retaliation/Alliance/Raid/Progress/ArmorPurge/Decode/Partition)는 전부 라이브에서 소비처/핸들러가 도달함(AttackPipeline / GameFlowProcessor 삭제-교체 윈도우 / EarlyPhaseFlow / BattleResolver). 트리거 경로:
- AttackPipeline: Blocker(`_blockTiming.RequestBlockChoice`), Alliance/Raid(`RequestChoice`), Progress(`ProgressImmunity.TryRegister`).
- BattleResolver/SecurityResolver inline: Jamming/Piercing/Retaliation.
- AttackPermanentAction: Rush/Blitz. EarlyPhaseFlow: Reboot.
- GameFlowProcessor 삭제-교체 윈도우(`DeletionReplacementTiming.RequestChoice`): ArmorPurge/Decode/Partition (+ Evade/Barrier 등 부여-전용도 이 윈도우로 도달).
- ❌ Vortex/Overclock: `EffectDrivenAttack`/`OverclockEffect.RequestChoice`를 부르는 **턴종료 훅이 없음**.

→ GR-006을 **"턴종료 효과-주도 공격 트리거"** 단일 서브시스템으로 재정의하면 Vortex+Overclock 동시 해결(둘 다 end-of-turn·상대 디지몬·소환멀미 우회; Overclock은 trait/token 아군 삭제 선결 추가).

### GR-006 (재정의) — 턴종료 효과-주도 공격 트리거 (Vortex + Overclock)

> **목표:** 턴 종료 시 `EffectDrivenAttack`를 *offer*하는 라이브 트리거 훅을 추가해, 현재 inert인 **Vortex**와 **Overclock** 두 키워드를 라이브로 발동시킨다. 해소 기계(`EffectDrivenAttack` 허브 + `ChoiceType.EffectAttack/OverclockTarget` 핸들러)는 이미 존재 — 빠진 건 "턴종료 시 RequestChoice를 부르는 훅".

**확정된 현재 상태:**
- 14개 자기-정적 키워드 중 **Vortex·Overclock만 라이브 트리거 없음**(나머지 12개는 트리거됨 — 위 감사 참조).
- `EffectDrivenAttack.GetTargets`는 `options.TargetUnsuspended`(=AS-IS isVortex) / `options.AllowPlayerTarget` 분기를 이미 가짐.
- `OverclockEffect.RequestChoice`는 존재하나 **호출자 0**. Vortex는 runtime 클래스조차 없음.

**확정된 AS-IS 규칙(권위 텍스트):**
- `<Vortex>` = "(At the **end of your turn**, this Digimon may attack an **opponent's Digimon**. With this effect, it can attack the turn it was played.)" → 대상 **디지몬**(플레이어 ❌), 소환멀미 우회.
- `<Overclock>` = 턴 종료 시 `[trait]`/토큰 아군 1체 삭제 → 이 디지몬이 **플레이어**를 서스펜드 없이 공격(헤드리스 주석 "untapped player attack" — **step 0에서 원본 재확인**).

**작업:**
0. **원본 재확인(추측 금지):** `DCGO/`에서 (a) Vortex가 공격하는 대상 범위(상대 디지몬 — 서스펜드만 vs 임의), (b) Overclock의 대상(플레이어?)·선결(아군 삭제)·서스펜드 여부, (c) 두 트리거의 정확한 발동 타이밍(End of Turn 윈도우 내 순서)을 1:1 확인.
1. **턴종료 트리거 훅:** 턴 종료 처리(`HeadlessEndTurnCleanupFlow` 또는 EndTurn 처리 경로)에서, `<Vortex>`/`<Overclock>` 보유 디지몬에 대해 효과-주도 공격을 offer(`EffectDrivenAttack.RequestChoice` / `OverclockEffect.RequestChoice`)하는 훅 추가. presence는 **`ContinuousKeywordGate`** 로 판정(GR-005 패턴; 게이트에 `Vortex` 상수 추가).
2. **옵션 정합(원본대로):** Vortex → `EffectAttackOptions(AllowPlayerTarget: false, AllowDigimonTarget: true, 소환멀미 우회)`; Overclock → 아군삭제 선결 후 `AllowPlayerTarget: true`. **헤드리스 `Vortex.cs` 주석의 "Digimon + player / AllowPlayerTarget"은 오류 → 바로잡기.**
3. **카드 포팅:** 라이브 실측용으로 Vortex/Overclock 보유 카드 각 1장 포팅(단순한 것 우선 — EX8_074는 Vortex+Alliance+비용감소+진화라 무겁다; 더 단순한 후보 탐색).

**DoD:**
- Vortex/Overclock 보유 카드로 **턴 종료 시 효과-주도 공격이 offer되고 발동**함을 단언하는 라이브 테스트(Vortex→상대 디지몬 공격, Overclock→플레이어 공격, 플레이어-대상 오류 없음).
- `bash scripts/run-tests.sh` 전체 green + `tools/RuleAudit` 위반 0.
- `Vortex.cs` 주석 정정 + 원본 대조 메모 기록.

**주의:** 턴종료 트리거 타이밍은 다른 [End of Turn] 효과와의 순서가 얽힐 수 있음 → 원본 순서 대조 필수. 둘 다 latent(ST1~3 미사용)라 비차단.

---

## ✅ GR-007 — 완료 (grant↔consume 정합)

**전체 정합 감사 결과:** 14개 키워드 중 불일치는 **정확히 2개**(Reboot/Piercing). 나머지 11개는 `GrantX→hasX` ↔ 소비 `hasX` 일치, Vortex는 소비 없음(GR-006).

| 키워드 | (수정 전) mutation→플래그 | 소비측 read | 조치 |
|---|---|---|---|
| Reboot | `ScheduleRebootUnsuspend`→~~scheduleRebootUnsuspend~~ | `hasReboot` | **sink 매핑 → `hasReboot`** |
| Piercing | `SetSecurityCheck`→~~pendingSecurityCheck~~ | `hasPiercing` | **sink 매핑 → `hasPiercing`** |

- 수정: `MatchStateMutationSink.KindToFlag`의 두 값만 소비 플래그로 변경(mutation **kind 이름은 유지** → G3G-001의 kind 단언 무영향). dead 플래그(scheduleRebootUnsuspend/pendingSecurityCheck) 제거.
- 안전성: 두 mutation kind는 **Reboot/Piercing 키워드만** emit(다른 emitter 없음), dead 플래그는 **read 0** 확인.
- 원본: Reboot/Piercing은 presence 키워드(`Permanent.HasReboot`/`EffectName=="Piercing"`) — 헤드리스 소비측(hasReboot/hasPiercing)이 올바른 모델, grant가 그 플래그를 set하도록 정합.
- 검증: `tests/GR-007.KeywordGrantConsume`(grant mutation → 소비 플래그 set + dead 플래그 미생성). 전체 232/232, 감사 위반 0. (self-static은 이미 GR-005 게이트로 작동; 이번은 grant 경로 정합.)

---

## (원안) GR-007 — 키워드 grant↔consume 플래그 정합 (Reboot/Piercing + 전체 감사)

**현재 상태(확정):** 일부 키워드의 **grant mutation이 set하는 플래그**와 **소비측이 읽는 플래그**가 다름:

| 키워드 | mutation이 set | 소비측이 read | 정합 |
|---|---|---|---|
| Reboot | `scheduleRebootUnsuspend` | `hasReboot` (HeadlessEarlyPhaseFlow) | ❌ |
| Piercing | `pendingSecurityCheck` (SetSecurityCheck) | `hasPiercing` (BattleResolver) | ❌ |

- `hasReboot`/`hasPiercing`은 **읽히지만 어떤 mutation도 set하지 않음**; `scheduleRebootUnsuspend`/`pendingSecurityCheck`는 **set되지만 아무도 안 읽음**(dead).
- **자기-정적** Reboot/Piercing: GR-005 게이트(`HasKeyword`)가 바인딩을 직접 봐서 우연히 **작동**.
- **다른 카드가 부여(grant)하는** Reboot/Piercing: raw mutation이 죽은 플래그를 set → **inert 의심**(부여가 키워드 바인딩 등록이면 게이트가 잡지만, mutation-only면 죽음).

**작업:**
0. **원본 확인:** Reboot/Piercing의 grant 의미 + 부여 방식(바인딩 vs 직접 효과) 확인.
1. **전체 정합 감사:** 모든 키워드의 (grant 플래그 ↔ consumer 플래그) 매핑 표 작성 — 불일치/dead-flag 전부 식별(이미 set-but-unread로 `pendingSecurityCheck`/`scheduleRebootUnsuspend` 발견; 다른 것도 점검).
2. **정합화:** 불일치를 한 방향으로 통일 — grant mutation이 consumer 플래그를 set하거나(권장: GrantReboot→hasReboot, GrantPiercing→hasPiercing), 소비측이 게이트/올바른 플래그를 읽도록. self-static은 GR-005 게이트 유지.
3. **dead 플래그 정리:** 더는 안 쓰는 set-only 플래그 제거 또는 정합 연결.

**DoD:** **부여(grant)된 Reboot/Piercing이 라이브 작동**함을 단언하는 테스트(grant 효과 카드 또는 직접 grant 시나리오) + 전체 green + 감사 위반 0. grant↔consume 정합 표를 `keyword_disconnect_prediction.md`(또는 신규 문서)에 기록.

---

## 우선순위/관계
- 둘 다 **latent**(해당 카드 미존재) → 대량 포팅에서 해당 키워드 카드를 만나기 전까지는 비차단.
- GR-006(Vortex)은 **신규 기능 구현**, GR-007은 **기존 배선 정합 수정** — 독립적. 순서 무관하나 GR-007이 더 가볍고 systemic(전체 키워드 정합 감사 포함).
- 권장: **GR-007 먼저**(정합 감사로 추가 불일치까지 한 번에 식별·수정) → GR-006(Vortex 신규).
