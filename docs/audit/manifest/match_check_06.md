# Match check — manifest part 6/13

Scope: `docs/audit/manifest/both_part_06.txt` (6 files). AS-IS = `DCGO/Assets/Scripts/<relpath>`,
TO-BE = `src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`. Both sides read in full for every file.

---

## 1. `Script/ContinuousController.cs` — PARTIAL / disclosed stub, one real gap found

AS-IS is 1843 lines: a Unity `MonoBehaviour` (scene load, Photon session/matchmaking, PlayerPrefs settings,
deck-file I/O, GUI HUD) plus two file-scope non-MonoBehaviour classes (`RandomUtility`, `PhotonUtility`) and
the `Language` enum. TO-BE is a 7-line skeleton stub (`// TODO: Skeleton only. Port or implement deterministic
.NET logic later.`).

Verified before accepting the stub (rather than assuming "substrate, so fine"):

- **`CreateTokenData()`** (AS-IS :151-506, the 17 `new CEntity_Base{...}` token specs incl.
  `VoleeZerdruckenToken`, `UkaNoMitamaToken`, `PipeFoxToken`) — confirmed present, all 17, in
  `src/.../Script/CardEffectCommons.cs` as the `TokenSpecs` dictionary + `PlayToken` (comment cites
  "AS-IS `ContinuousController.CreateTokenData()`" explicitly, values checked field-by-field against AS-IS —
  match). Not a gap; correctly relocated.
- **`RandomUtility.ShuffledDeckCards(...)`** (Fisher-Yates over `GameRandom`, AS-IS :1521-1578) — AS-IS has
  40+ call sites; TO-BE call sites for the *ported* cards route through `IZoneMover.Shuffle*Async` /
  `GameRandomSource` (same Xoshiro256** family, confirmed in `Headless/Services/GameRandomSource.cs`). Not a
  gap; correctly relocated to the zone-mover substrate.
- **`RandomUtility.IsSucceedProbability(float)`** (AS-IS :1497-1518, a probability coin-flip used by
  `TurnStateMachine.cs` for redraw / auto-hatch 85% / AI 99% checks and by `SelectAttackEffect.cs:438` for the
  3rd-security evasion roll) — **zero occurrences anywhere in `src/HeadlessDCGO.Engine`** (`grep -r
  IsSucceedProbability` returns nothing). No `NextDouble`/threshold-based equivalent was found substituted at
  any call site either. This function's only AS-IS declaration is inside this stub file, so from this file's
  point of view it is simply unported. **Flagged as a real gap** — its call sites (TurnStateMachine.cs,
  SelectAttackEffect.cs, OptionalSkill.cs, TrialDraw.cs) sit outside this manifest chunk, so I cannot confirm
  whether those files independently reimplement the probability check or whether it is silently absent from
  those flows too; either way, the canonical helper does not exist in TO-BE.
- `RandomUtility.GetSecureRandom()` (crypto RNG session seed), `PhotonUtility` (Photon connect/lobby
  plumbing), deck-file save/load, PlayerPrefs settings, GUI HUD — legitimately Unity/session-only, no engine
  logic; correctly out of scope for a headless engine.

**Verdict**: the stub itself is defensible for ~95% of the file's content (Unity/Photon/session concerns), and
the two substantive game-logic pieces I checked (`CreateTokenData`, `ShuffledDeckCards`) are verified present
elsewhere. `IsSucceedProbability` is a genuine unaccounted-for piece of AS-IS game logic with no mirror
anywhere in the tree.

---

## 2. `Script/CardEffectCommons/GetFromHashtable.cs` — MATCH, clean 1:1

859 AS-IS lines vs 868 TO-BE lines (the +9 is the mirror's header comment block). Read both in full,
side-by-side: every method (`GetCardEffectFromHashtable`, `GetSkillFromHashtable`, `GetRootFromHashtable`,
`IsAttack`, `IsBlock`, `IsBurst`, `IsFromSameDigimon`, `IsFromDigimon`, `IsFromDigimonDigivolutionCards`,
`GetTopCardFromEffectHashtable`, `GetEvoRootTopsFromEnterFieldHashtable`,
`GetPlayedPermanentsFromEnterFieldHashtable`, `GetAttackerFromHashtable`, `GetHashtablesFromHashtable`,
`GetTopCardFromOneHashtable`, `GetCardFromHashtable`, `GetFaceDownFromHashtable`, `GetBattleFromHashtable`,
`IsDPZeroDelete`, `IsOnly1CardPlayed`, `GetPlayCardClassFromHashtable`, `IsEvolution`, `GetPlayerFromHashtable`,
`GetPlayersFromHashtable`, `GetPermanentsFromHashtable`, `GetWinnerPermanentsRealFromHashtable`,
`GetLoserPermanentsFromHashtable`, `GetDiscardedCardsFromHashtable`, `GetCardSourcesFromHashtable`,
`GetDigivolutionSourcesFromHashtable`, `GetDeckBottomCardsFromHashtable`,
`GetDigivolutionRootsFromEnterFieldHashtable`, `GetPermanentFromHashtable`,
`IsDigivolvedFromSameLevelFromEnterFieldHashtable`, `IsAlliance`, `IsJogress`, `IsLeavingForDigiXros`,
`IsDijiXros`, `GetDigiXrosCount`) is present, same signature, same body, same string keys
(`"CardEffect"`, `"hashtables"`, `"AttackingPermanent"`, `"isEvolution"`, ...), same order. Only changes:
`UnityEngine` using stripped, `namespace` wrapper added, class made `static`. No findings.

---

## 3. `Script/AttackProcess.cs` — substrate rewrite, no mismatches found

AS-IS 628 lines (`MonoBehaviourPunCallbacks`, coroutine-driven state machine: `Attack` → `CounterTiming` →
`BlockTiming` → `DetermineAttackOutcome` → `EndAttack` → `Cleanup`, plus `SwitchDefender`). TO-BE 1025 lines: a
plain `sealed class AttackProcess` translating the coroutine suspensions into an explicit
`AttackPhase`/`AttackAdvanceResult` step machine (`ProcessNextState` consumed once per
`GameFlowProcessor.RunToStableAsync` iteration). Every AS-IS method has a corresponding TO-BE method or stage
handler: `ActiveAttack`, `ProcessNextState`, `Attack`, `CounterTiming`, `BlockTiming`(→`BlockStage` +
`BlockTiming` helper class), `DetermineAttackOutcome` (split into `DetermineAttackOutcome` +
`ResumeDeletionReplacement` + `ResumePiercingSecurity` for the two AS-IS coroutine-suspension points),
`EndAttack`/`EndAttackStage`, `Cleanup`, `SwitchDefender`. Every AS-IS boundary check
(`IsEndAttack`/`TopCard==null`/`!IsDigimon` at :106,221,258,277,301,325,386,410) is present with an inline
AS-IS line-number citation. The file's header and inline comments cite AS-IS line numbers for every
substrate decision and name the specific design items for each intentional relocation (Raid/Alliance keyword
relocation, Execute self-delete relocation) rather than silently absorbing them. Spot-checked the
`SwitchDefender` double-`if(isBlock)` AS-IS pattern (:542-564) against its TO-BE single combined guard — the
AS-IS second branch is only ever reached with a non-null `newDefendingPermanent` in practice (the sole caller,
`BlockTiming`, already null-checks `selectedPermanent`), so the TO-BE combination is behavior-preserving. No
missing methods, no unexplained signature drift found. This matches the pre-existing "migration goal 1:
AttackProcess — complete" record; I found nothing to contradict that here.

---

## 4. `Script/CardObjectController.cs` — INCOMPLETE, real gap (most significant finding)

AS-IS is 1133 lines of static zone-move / deck-setup helpers. TO-BE is 449 lines and its own header states
the port is **"INCREMENTAL"**, listing exactly what it covers. Verifying against the full AS-IS method list:

| AS-IS method | TO-BE status |
|---|---|
| `CreatePlayerDecks` | not in this file; confirmed relocated to `Headless/Runtime/MatchSetupFlow.cs` |
| `CreateCardSource` | not in this file; confirmed relocated into `CardEffectCommons.PlayToken`'s inline `CardInstanceRepository.Upsert` |
| `RemoveFromAllArea` | ported |
| `AlignHand` | not ported — pure UI (`GridLayoutGroup` spacing), correctly out of scope |
| `CreateNewPermanent` | ported (re-shaped signature, substrate-justified in the doc comment) |
| `RemoveField` | ported |
| `AddHandCards` / `AddHandCard` | ported |
| `AddTrashCard` (singular) | ported |
| **`AddTrashCards` (plural/batch)** | **not defined anywhere in TO-BE.** AS-IS has exactly one caller (`RevealLibrary.cs:66`); verified its TO-BE mirror (file 5 below) reproduces the same behavior inline via a `TrashCardKind` sink mutation. Not a live gap, but the shared AS-IS helper itself is gone. |
| **`AddLibraryTopCards`** | **not defined anywhere in TO-BE** (`grep -rn "Task.*AddLibraryTopCards"` — zero hits). AS-IS has **24 call sites**: `CardController.cs`, `SelectHandEffect.cs`, `RevealLibrary.cs`, and 21 card-effect files (BT18_019, BT8_065, BT14_098/061/005, LM_020/027/028/029/030/031/032, EX7_043, BT19_074/101, EX10_074, EX3_054, BT16_054/065, BT4_074, BT23_057). Within this manifest's own scope, the two callers I audited (`RevealLibrary.cs`, `SelectHandEffect.cs`) both replace the call with an inline `ReturnToDeckTopKind` sink mutation and explicitly document the substitution in a comment ("no mirror method -> the sink ..."). The other 21+ AS-IS call sites are outside this manifest chunk and mostly still unported stubs (confirmed for BT23_057.cs's own comment referencing this same gap), so no shared canonical implementation exists for future porting to build on. |
| **`AddLibraryBottomCards`** | **not defined anywhere in TO-BE.** AS-IS has **50 call sites**, including core flow files `TurnStateMachine.cs`, `CardController.cs`, `SelectCardEffect.cs` (none of which are in this manifest chunk, so I could not verify their TO-BE replacement pattern) plus `SelectHandEffect.cs`/`RevealLibrary.cs` (verified inline substitute, same as above) and ~40 card-effect files. Same situation as `AddLibraryTopCards`: covered ad hoc within this chunk's two files, uncentralized everywhere else. |
| **`Shuffle(Player)`** | not defined in this file. The substantive part (`RandomUtility.ShuffledDeckCards`) is confirmed relocated to `IZoneMover`/`GameRandomSource` (see file 1); the wrapper itself (SE + `ShuffleAnimation`) is UI, correctly dropped. |
| **`MovePermanent(FieldCardFrame, bool toBreeding, ICardEffect)`** | **not defined anywhere in TO-BE**, and I found **no evident substrate substitute** either. AS-IS has 10 call sites: `TurnStateMachine.cs:810` (hatch, breeding→battle — TO-BE comment at `TurnStateMachine.cs:369` confirms this direction is covered by `ZoneMover.MoveBreedingToBattleAsync`), `CardController.cs:1621` (battle-area re-slot, not verified — outside this chunk), and 7 card effects that move a permanent *to* the breeding area (`BT18_086`, `BT20_095`, `BT14_088`, `BT1_089`, `EX9_057`, `EX10_013`, `P_130`, `P_143`). Checked all of `P_143.cs`, `BT18_086.cs`, `BT20_095.cs` in TO-BE: **all three are still unported skeleton stubs** (`// TODO: Skeleton only.`), so this is not causing live incorrect behavior today, but there is currently no reusable AS-IS-equivalent method (with its cut-in "would remove field" window, the `OnMove`/`OnLeaveFieldAnyone` window firing, and the frame-slot bookkeeping) for whoever ports those 7 cards to call. |
| `AddExecutingCard` | ported |
| `AddSecurityCard` | ported |

**Verdict**: the header's "INCREMENTAL" framing is honest disclosure, not a hidden mismatch — but per this
audit's remit to report every gap found regardless of category, three AS-IS helpers
(`AddLibraryTopCards`, `AddLibraryBottomCards`, `MovePermanent`) that are used by dozens of AS-IS call sites
have **no canonical TO-BE implementation anywhere in the tree**, only scattered inline duplicates at the ~2-3
call sites that happen to fall inside already-ported files. This is a real completeness gap in this
specific manifest file, with `MovePermanent` the more concerning of the two since none of its 7
breeding-bound card-effect callers are ported yet and there is nothing for them to call when they are.

---

## 5. `Script/CardEffectCommons/RevealLibrary.cs` — MATCH, high-fidelity rewrite

AS-IS 791 lines vs TO-BE 741 lines. Every AS-IS symbol accounted for:
`RevealDeckTopCardsAndProcessForAll`, `SimplifiedRevealDeckTopCardsAndSelect`, `RevealDeckTopCardsAndSelect`,
`ReturnRevealedCardsToLibraryBottom`, `ReturnRevealedCardsToLibraryTop` (renamed
`ReturnRevealedCardsToLibraryTopAsync`, private, same call sites), `TrashRevealedCards` /
`AddRevealedCardsToHand` / `ReturnRevealedCardsToLibraryTopOrBottom` (folded into the shared
`RouteRemainingRevealedCardsAsync`/`StageRevealMovesAsync` switch, same AS-IS routing table and same "top =
first pick, then reversed on insert" ordering rule, verified line-by-line), plus the two AS-IS namespace types
`SimplifiedSelectCardConditionClass`/`SelectCardConditionClass` (all fields present, `IEnumerator`→`Task`
only) and the `RemainingCardsPlace` enum (identical members/order). `RevealLibraryClass`'s
`RevealLibrary()` coroutine is inlined as a direct `zones.GetCards(...).Take(revealCount)` read (no behavior
loss — it was a private state holder). Confirms the file-4 finding: this file does **not** call
`CardObjectController.AddLibraryTopCards`/`AddLibraryBottomCards`/`AddTrashCards` (which don't exist) but
reimplements their effect inline via `MatchStateMutationSink` mutations — verified this reproduces the AS-IS
behavior for library-sourced cards (the AS-IS `isFromTrash`/ACE-overflow preambles in those helpers are no-ops
here since revealed cards always originate from the library, never the field). One explicitly self-disclosed
limitation carried in a code comment (design item RD-W3-1: an unreachable "un-pick to satisfy
canEndSelectCondition at maxCount" corner for path-dependent select conditions) — disclosed, not a silent
gap. No new problems found.

---

## 6. `Script/SelectHandEffect.cs` — MATCH, high-fidelity rewrite

AS-IS 942 lines (`MonoBehaviourPunCallbacks`, interactive click-based hand selection + a parallel AI
auto-select branch, Photon RPC round-trip) vs TO-BE 550 lines (`sealed class`, both AS-IS branches collapsed
into one `ChoiceProvider` request — the same established collapse pattern used by `SelectCardEffect`, not an
invention specific to this file). Verified present: `Mode` enum (identical 7 members/order), all `SetUp*`
setters (`SetIsLocal`, `SetNotShowCard`, `SetDigiXros`, `SetIsFaceup`, `SetReducedCostTuple`,
`SetFixedCostTuple`, `SetUpCustomMessage_ShowCard`, `SetUpCustomMessage`, `SetNotShowOpponentMessage`),
`active()` (verbatim guard), `Activate()`'s full flow order (active-guard → maxCount==0⇒canNoSelect →
selection → per-card `selectCardCoroutine` for every mode, run *before* the mode switch exactly as AS-IS →
mode routing → deferred batched `IDiscardHands` → `afterSelectCardCoroutine`), and the full `PlayForCost`
reduce/fixed-cost `ChangeCostClass` registration/release block (AS-IS :759-895) reproduced with the same local
predicate names and same closures. `PutLibraryTop`/`PutLibraryBottom`/`AddSecurityCard(...,toTop:false,...)`
match the file-4 finding (no shared `CardObjectController` helper; reimplemented inline here, consistent with
file 5). The AS-IS `Mode.Discard` hashtable-per-card (`{"CardEffect", _cardEffect}`) was restructured into a
single shared `causeId` passed once to `IDiscardHands(..., causeId, _cardEffect)` rather than per-`IDiscardHand`
— semantically equivalent since `_cardEffect` is constant across the batch, but I could not fully verify this
against `IDiscardHand`/`IDiscardHands`'s constructor contract since those types are outside this manifest
chunk. No other problems found; same self-disclosed selection-corner limitation as file 5 (design item
RD-W4-2).

---

## Summary

| # | File | Verdict |
|---|---|---|
| 1 | ContinuousController.cs | Disclosed stub, mostly justified (Unity/session) — but `RandomUtility.IsSucceedProbability` has no mirror anywhere in TO-BE (real gap) |
| 2 | CardEffectCommons/GetFromHashtable.cs | Clean 1:1 match, no findings |
| 3 | AttackProcess.cs | High-fidelity substrate rewrite, no mismatches found |
| 4 | CardObjectController.cs | Disclosed incremental port, but `AddLibraryTopCards`/`AddLibraryBottomCards`/`MovePermanent` have no canonical TO-BE implementation anywhere in the tree (real completeness gap, `MovePermanent` most concerning — 7 dependent card effects still unported with nothing to call) |
| 5 | CardEffectCommons/RevealLibrary.cs | High-fidelity rewrite, no new problems (confirms file 4's gap is handled inline here) |
| 6 | SelectHandEffect.cs | High-fidelity rewrite, no new problems (confirms file 4's gap is handled inline here); one unverified restructuring (per-batch vs per-card discard cause id) |

Two concrete problems found: (1) `RandomUtility.IsSucceedProbability` unmirrored anywhere (file 1); (2)
`AddLibraryTopCards`/`AddLibraryBottomCards`/`MovePermanent` unmirrored as shared helpers anywhere, with
`MovePermanent` having zero substrate coverage for its breeding-bound callers (file 4).
