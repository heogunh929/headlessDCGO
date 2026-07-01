# 키워드 단절 예측 맵 (전체 키워드 스윕)

> 단절 패턴(GR-005에서 확정): 자기-정적 키워드가 **EffectRegistry 바인딩**으로 등록되는데, 소비측은 인스턴스 **메타 플래그**(`hasX`)를 읽고, 그 플래그는 *다른 카드가 부여(Grant)*할 때만 세팅됨 → 자기-정적은 라이브에서 inert. **pull 게이트**(`ContinuousKeywordGate`/`ContinuousModifierGate`)로 레지스트리를 read-time 조회하면 해소.
>
> 이 문서는 **예측**(코드 패턴 기반)이다. ✅=실측/확정, ⚠️=동일 패턴 강한 예측(미실측), ❓=메커니즘 달라 별도 검증 필요.

## 키워드 전체 (Batch1 4 + Batch2 10 = 14종)

| 키워드 | Batch | 소비 플래그/메커니즘 | 자기-정적 factory | 게이트 | 예측 |
|---|---|---|---|---|---|
| **Blocker** | 1 | `hasBlocker` @ BlockTiming | 있음 | ✅ GR-005 | ✅ **해소(실측 0→62)** |
| **Jamming** | 1 | `preventBattleDeletion` @ SecurityResolver | 있음 | ✅ GR-005 | ✅ 해소(미실측, 대칭) |
| **Piercing** | 1 | `hasPiercing` @ BattleResolver | 있음 | ✅ GR-005 | ✅ 해소(미실측, 대칭) |
| **Reboot** | 1 | `hasReboot` @ HeadlessEarlyPhaseFlow | 없음 | 없음 | ⚠️ **잠재 단절** — 존재-플래그, 포팅 시 게이트 필요 |
| **Rush** | 2 | `hasRush` @ AttackPermanentAction(소환멀미) | 없음 | 없음 | ⚠️ **잠재 단절** — 존재-플래그 |
| **Blitz** | 2 | `hasBlitz` @ AttackPermanentAction(메모리패스) | 없음 | 없음 | ⚠️ **잠재 단절** — 존재-플래그 |
| Retaliation | 2 | `hasRetaliation` @ BattleResolver(배틀삭제 반격) | 없음 | 없음 | ⚠️ 존재-플래그 추정 → 잠재 단절(검증 필요) |
| ArmorPurge | 2 | `hasArmorPurge` (de-digivolve 회피) | 없음 | 없음 | ❓ 메커니즘 검증 필요 |
| Decode | 2 | `GrantDecode` | 없음 | — | ❓ 검증 필요 |
| Alliance | 2 | 공격시 선택(AllianceAttackBoost 핸들러) | 없음 | — | ❓ 핸들러 존재 → 트리거/액션 계열일 가능성(라이브 가능성 높음), 검증 필요 |
| Vortex | 2 | 공격 능력(EffectDrivenAttack/VortexAttack) | 없음 | — | ❓ 핸들러 존재 → 검증 필요 |
| Overclock | 2 | 턴종료 능력(OverclockEffect 핸들러) | 없음 | — | ❓ 핸들러 존재 → 검증 필요 |
| Partition | 2 | `WhenRemoveField` 타이밍 | 없음 | — | ❓ 트리거 계열 추정, 검증 필요 |
| Progress | 2 | `GrantProgress` | 없음 | — | ❓ 검증 필요 |

## 분류 요약

**A. 해소(GR-005):** Blocker / Jamming / Piercing — 유일하게 자기-정적 factory가 있던 3종. 게이트로 해소.

**B. 잠재 단절 → ✅ 선제 봉합 완료:** **Reboot / Rush / Blitz / Retaliation**
- 넷 다 **연속-정적 "존재 플래그"**(`hasReboot`/`hasRush`/`hasBlitz`/`hasRetaliation`)를 소비측이 read. (Retaliation은 `BattleResolver:122` `HasFlag(dead, HasRetaliationKey)`로 확인 → B 확정.)
- 자기-정적 factory가 아직 없어 활성 버그는 아니었으나(latent), **소비 4지점이 `ContinuousKeywordGate`도 OR로 조회하도록 선제 봉합**:
  - Reboot → `HeadlessEarlyPhaseFlow`(언서스펜드)
  - Rush → `AttackPermanentAction`(소환멀미 우회)
  - Blitz → `AttackPermanentAction`(메모리패스 공격)
  - Retaliation → `BattleResolver`(배틀삭제 반격)
- 게이트에 `Reboot/Rush/Blitz/Retaliation` 상수 추가. **검증:** `tests/GR-005`의 "B-group seal" 테스트 — 배치 바인딩 등록 시 게이트가 4키워드를 도출(카드가 없어 self-play 실측은 불가, 게이트 프리미티브로 회귀 방지).
- → 향후 이 키워드들의 자기-정적 카드를 포팅하면 소비측 재수정 없이 **그냥 작동**.

**C. 검증 결과 — 대부분 존재-플래그 단절(예측 정정):** ArmorPurge / Decode / Alliance / Vortex / Overclock / Partition / Progress
- **정정:** "액션 계열이라 플래그 단절 아님"이라던 예측은 **틀렸음**. 6개(Alliance/Overclock/Progress/ArmorPurge/Decode/Partition)는 presence 판정이 **`hasX` 플래그 read**(`HasAllianceKey`/`HasOverclockKey`/`HasProgressKey`/`HasArmorPurgeKey`/`HasDecodeKey`/`HasPartitionKey`)로 **Blocker와 동일 단절 패턴**. 핸들러는 presence 확정 *후* 동작할 뿐.
- **✅ 봉합(clean):** **Alliance / Overclock / Progress** — 각 `HasX(context, id)` helper(AllianceAttackBoost/OverclockEffect/ProgressImmunity)에 게이트 OR 1줄.
- **✅ 봉합(선행, deletion-replacement):** **ArmorPurge / Decode / Partition** — `DeletionReplacementTiming`의 offer 3지점(context 보유)에 게이트 OR. ArmorPurge resolution(`DeletionReplacementGate.TryArmorPurgeAsync`)은 context 미수용 → **`EffectRegistry?` 파라미터를 추가(`ContinuousKeywordGate.HasKeyword(registry,...)` 오버로드)**하고 유일 호출자가 `context.EffectRegistry`를 전달하도록 스레딩 → offer/resolution 일관 봉합. 10개 봉합 키워드 모두 `tests/GR-005` seal 루프에서 게이트 도출 검증.
- **Vortex:** `hasVortex`를 세팅만 하고 **읽는 소비처가 Runtime에 없음** → presence-플래그 단절 아님(EffectDrivenAttack 경로 별도 처리 추정).

### C군 결론
존재-플래그 단절은 **키워드 전반의 systemic 패턴**(예측의 "액션은 다르다"가 부정확). clean 3개 봉합 완료, deletion-replacement 3개는 risk/latent로 카드 포팅 시 봉합, Vortex는 다른 메커니즘. systemic 대안(등록 시 `hasX` push)도 가능하나 conditional/leave 정합 때문에 read-time 게이트(pull) 유지.

**D. 부여-전용 플래그(낮은 위험):** `hasEvade/hasBarrier/hasDecoy/hasFortitude/hasFragment/hasAscension/hasScapegoat/hasSave/hasIceclad/hasCollision` 등
- mutation-grant 맵엔 있으나 Batch1/2 enum엔 없음 → **다른 카드 효과가 Grant**하는 방식(자기-정적 키워드 바인딩 경로 아님)으로 추정 → Grant mutation이 플래그를 세팅 → 정상.
- 단 자기-정적 factory가 어딘가 생기면 동일 위험 → factory 추가 시 점검.

## 권장 후속

1. **B군(Reboot/Rush/Blitz) 선제 봉합**: `ContinuousKeywordGate`에 3키워드 추가 + 소비 3지점이 게이트 OR. 카드가 없어 self-play 실측은 불가하나, 게이트 프리미티브 테스트(GR-005 방식)로 회귀 방지. → 향후 해당 키워드 카드 포팅이 "그냥 작동".
2. **C군 키워드별 실측**: 각 키워드를 가진 카드(또는 픽스처)로 라이브 발동 확인. ArmorPurge/Partition은 트리거 타이밍 emit 여부, Alliance/Vortex/Overclock은 핸들러 경로 발동 여부.
3. **Retaliation 소비 코드 확인** → B 또는 C로 확정.
