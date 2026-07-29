# Entity model design — substrate-owned live `Player` / `CardSource` / `Permanent`

**Status:** design only. No production code. Decision to do this is already taken; this document says *how*.
**Repo state at time of writing:** `main` @ `10cfc98d` + uncommitted shim work (tree dirty, expected).
**Build baseline:** all five projects build, 0 errors (`dotnet build tools/RlVectorHost/RlVectorHost.csproj --no-incremental` → 0 errors / 2131 warnings; `dotnet build tools/RuleAudit/RuleAudit.csproj` → 0 errors / 1 warning `CS8321`). [실측]

**Goal.** An id always resolves to the *same mutable instance* for the life of a match, so that the original's
statements compile and behave identically:

```csharp
permanent.IsSuspended = true;
permanent.DPWhenSuspended = permanent.DP;
permanent.LinkedDP += x;
card.Owner.LibraryCards.Count
someList.Contains(cardSource)
permanent == targetPermanent
card.Init();
```

**Evidence grading used throughout:** [실측] = measured/read directly; [게이트] = would be enforced by a build or
run gate; [판단] = my inference from measured facts, labelled as such.

---

## 1. What the original actually is

All citations in this section are into `/home/hg/git/headlessDCGO/DCGO/`. All greps used `--binary-files=text`.
Nothing here is inferred from the mirror.

### 1.1 Type kind

| Type | Declaration | Kind |
|---|---|---|
| `Player` | `Assets/Scripts/Script/Player.cs:12` — `public class Player : MonoBehaviour` | class, MonoBehaviour, not sealed/partial, no interfaces, no namespace |
| `CardSource` | `Assets/Scripts/Script/CardSource.cs:9` — `public class CardSource : MonoBehaviour` | class, MonoBehaviour, not sealed/partial, no interfaces, no namespace |
| `Permanent` | `Assets/Scripts/Script/Permanent.cs:7` — `public class Permanent` | **plain C# class**, no base type, no interfaces |

The asymmetry matters: `Permanent` is a plain heap object with no Unity lifetime at all. `Player` and
`CardSource` are Unity components, but — as §1.4 establishes — nothing ever `Destroy`s either of them during a
match, so their Unity-ness is irrelevant to the rule model.

### 1.2 Construction and ownership

**`Player` — 2 per match, scene-authored, never constructed in code.**
Zero `new Player(`, zero `AddComponent<Player>()`, zero prefab instantiation tree-wide. The two instances are
serialized into `Assets/Scenes/BattleScene.unity` (component fileIDs `487997840` and `519228385`, at scene lines
`:41415` and `:43915`) and wired into the singleton's inspector fields `GManager.cs:12` `public Player You;` and
`GManager.cs:15` `public Player Opponent;` (scene wiring at `BattleScene.unity:17925-17926`). Neither field is
ever assigned in code — the only `You =` / `Opponent =` hits in `Assets/Scripts/` are `GameContext.cs:14-15`,
writing GameContext's own copies.

Per-instance init is `Player.Start()` (`Player.cs:15-80`), which builds `fieldCardFrames` from serialized child
transforms (`:23-53`) and sizes the slot array `FieldPermanents = new Permanent[fieldCardFrames.Count]` (`:78`).

The roster is mirrored into `GameContext` — a plain `[System.Serializable] class` (`GameContext.cs:8-9`) with
`public Player You;` `:38` and `public Player Opponent;` `:39`, constructed exactly once tree-wide at
`TurnStateMachine.cs:91` `gameContext = new GameContext(GManager.instance.You, GManager.instance.Opponent);`.
`GameContext.Players` (`:41-52`) is a **computed property** allocating a fresh `List<Player>` per access — but the
*elements* are always the same two objects. `TurnPlayer` `:80` is a real field; `NonTurnPlayer` `:82-99` is
computed by scanning for `player != TurnPlayer` (`:90`) — i.e. by **reference**.

**Lifetime:** the whole match. `Player.cs` has no `OnDestroy`, no `Init`, no `Reset`; searched
`Destroy\([^)]*[Pp]layer`, `Destroy(You`, `Destroy(Opponent`, `You = null`, `Opponent = null` — nothing.
Between matches the entire scene reloads (`TurnStateMachine.cs:3358` `SceneManager.LoadScene("BattleScene")`).

**`CardSource` — one per physical card, created once at deck build, never re-created.**
No declared constructor, zero `new CardSource(`. The **sole** creation point is
`CardObjectController.CreateCardSource(int, CEntity_Base, bool)` (`CardObjectController.cs:347-366`):

```
351  CardSource cardSource = Instantiate(GManager.instance.CardPrefab, player.CardSorcesParent);
353  cardSource.SetBaseData(cEntity_Base, player);       // sole caller of CardSource.cs:37
355  cEntity_EffectController.AddCardEffect(...)
357  cardSource.SetUpCardIndex(GManager.instance.CardIndex);   // sole caller of CardSource.cs:105
359  cardSource.SetIsToken(isToken);
361  gameContext.ActiveCardList.Add(cardSource);
363  GManager.instance.CardIndex++;
```

Prefab field: `GManager.cs:18 public CardSource CardPrefab;`. Only two callers of `CreateCardSource`: the four
deck loops in `CardObjectController.CreatePlayerDecks` (`:320-340`, itself called exactly once from
`TurnStateMachine.cs:233`) and token creation (`CardEffectCommons.cs:156-159`).

**Two holders.** (a) a permanent global registry, `GameContext.ActiveCardList`
(`GameContext.cs:31-34`, `public List<CardSource> ActiveCardList { get; set; } = new()`), appended at
`CardObjectController.cs:361` and **never cleared or removed from** — it is the `CardIndex`-addressable
table used by the network/action layer (`TurnStateMachine.cs:3076`, `:3093`, `SelectCardEffect.cs:670`,
`SelectHandEffect.cs:610`). (b) exactly one of the seven zone lists on `Player` — `LibraryCards` `Player.cs:498`,
`DigitamaLibraryCards` `:502`, `HandCards` `:506`, `TrashCards` `:510`, `LostCards` `:514`, `SecurityCards` `:518`,
`ExecutingCards` `:522` — or, if on the field, `Permanent.cardSources` (`Permanent.cs:880`) /
`Permanent.LinkedCards` (`:1041`).

**`Permanent` — one ctor, held in a fixed-size slot array.**
Sole ctor `Permanent.cs:9` `public Permanent(List<CardSource> cardSources)`, body = `SetCardSources(cardSources)`
(`:11`). 42 `new Permanent(` sites tree-wide; the only *board-creating* ones are `CardController.cs:1383` (play)
and `CardController.cs:1496` (DNA/Jogress result). The rest are snapshots and empty cost-probe dummies.

Held in `Player.cs:663 public Permanent[] FieldPermanents = new Permanent[16];` (re-sized at `Player.cs:78`),
plus a duplicate back-reference `FieldCardFrame.framePermanent` (`Player.cs:1571`, written by `SetFramePermanent`
`:1573-1576`). Every write to the slot array: `CardObjectController.cs:489` (create), `:539-540` (clear),
`:1068-1071` (move between frames), `DNADigivolveEffects.cs:33/:92/:179`, `BT17_095.cs:334/:339`.

`Player.GetFieldPermanents()` (`:665-681`) allocates a fresh list per call, including slot `i` only if
`FieldPermanents[i] != null && FieldPermanents[i].TopCard != null`.

**⚠ Constructor quirk that must be reproduced deliberately.** `SetCardSources` (`Permanent.cs:14-25`) clones the
argument (`:16`, `List<T>.Clone` = order-preserving shallow copy, `IEnumerableExtension.cs:82-85`) then calls
`AddCardSource` per element (`:23`) — and `AddCardSource` does `cardSources.Insert(0, …)` (`:1047`). **The
constructor therefore reverses stack order.** The author's own dead line `//newCardSources.Reverse();` sits at
`:18-19`. Irrelevant for the single-element play path; material for the six `new Permanent(other.cardSources)`
snapshot sites (§1.5).

### 1.3 Lifetime and identity: does a `Permanent` survive digivolution?

**Yes — same object.** `CardController.cs:1359-1387`:

```
1361      card.Init();
1363      RemoveFromAllArea(card)
1365      if (isEvolution)
1371          permanent = _targetPermanent;         // ← the EXISTING object
1374          permanent.AddCardSource(card);        // ← new top pushed onto its stack
1376      else
1383          permanent = new Permanent(new List<CardSource>(){card}) { IsSuspended = _isTapped };
1385          CardObjectController.CreateNewPermanent(permanent, frameId)
```

`isEvolution` is set at `:1346` when `_targetPermanent != null`. No new `Permanent` is constructed; the slot and
the frame back-reference are untouched; **all 41 pieces of per-permanent mutable state (§1.4) carry over unchanged.**
This is the single most consequential fact in this document.

**DNA / Jogress is the exception** (`CardController.cs:1434-1513`): both evo-root permanents are removed from the
field (`:1484` `DiscardEvoRoots`, `:1486` `RemoveField`), a brand-new `Permanent` is built (`:1496`) and installed
at `targetFrameID` (`:1498`), then the old sources are re-stacked (`:1503` `AddDigivolutionCardsTop`). Same shape
at `DNADigivolveEffects.cs:29-33`. So DNA **breaks** identity; ordinary digivolution does not.

**When the last card leaves the field** — `CardObjectController.RemoveField` (`:513-555`):

```
516      if (permanent.TopCard == null) yield break;              // ← EARLY EXIT
535-541  for i: if (FieldPermanents[i] == permanent) { FieldPermanents[i] = null; SetFramePermanent(null); }
546-554  if (permanent.TopCard != null) {
548-551      foreach (CardSource cs in permanent.cardSources) cs.Init();
553          permanent.cardSources = new List<CardSource>();      // list REPLACED, sources kept
554      }
```

The `Permanent` object is **not destroyed** — it is a plain C# object, nothing disposes it. Its slot is nulled and
its stack list is replaced with a fresh empty one. It lingers exactly as long as something still holds a reference:
battle snapshots, effect hashtables, `CardSource.PermanentJustBeforeRemoveField` (`CardSource.cs:3571` — a
deliberately dangling handle), `ShowingPermanentCard`.

Two structural consequences visible in the source, which the target model inherits verbatim:
- `GetFieldPermanents()` filters on `TopCard != null` (`Player.cs:673`) but `FieldCardFrame.IsEmptyFrame()`
  checks only `framePermanent == null` (`Player.cs:1598-1601`). The two views can legitimately disagree.
- `RemoveField` early-returns when `TopCard == null` (`:516`). If sources were already stripped (e.g. by
  `RemoveFromAllArea` → `RemoveCardSource`) before `RemoveField` runs, the slot is **never** cleared and the frame
  keeps holding an emptied `Permanent`.

**Stack mutators.** `SetCardSources` `Permanent.cs:14-25` (does *not* clear first — it appends onto whatever is
there, and reverses); `AddCardSource` `:1045-1053` (`Insert(0, …)` then `SetFace()`/`SetReverse()` per
`IsFlipped`); `RemoveCardSource` `:1297-1302` (an `IEnumerator`: `yield return null;` then `cardSources.Remove`);
`AddDigivolutionCardsTop` `:1064+` inserts at index **1** (`:1090`), i.e. under the top;
`RemoveLinkedCard` `:1306-1348` decrements `LinkedDP` `:1310`, `LinkedCards.Remove` `:1311`, then
`RemoveCardSource` `:1313`.

**Does a `CardSource` survive a zone move as the same object? Yes.**
`RemoveFromAllArea` (`CardObjectController.cs:370-447`) pulls the *identical reference* out of every container —
field permanents `:392-401`, then seven `while (zoneList.Contains(cardSource)) zoneList.Remove(cardSource);`
blocks at `:407-410`, `:413-416`, `:419-422`, `:425-428`, `:431-434`, `:437-440`, `:443-446`. Every `Add*` is
remove-then-add of that same reference: `AddHandCard :625`, `AddTrashCard :717`, `AddTrashCards :739`,
`AddLibraryTopCards :781`, `AddLibraryBottomCards :863`, `AddExecutingCard :957`, `AddSecurityCard :976`,
`Shuffle :1011` (reassigns `player.LibraryCards` to a reordered list of the *same* references). No
`UnityEngine.Object.Destroy` is ever applied to a `CardSource`.

**`CardSource.Init()`** — `CardSource.cs:345-350`, resets exactly three things:
`cEntity_EffectController.InitUseCountThisTurn()` `:347` (→ `CEntity_EffectController.cs:172-175`,
`UseEffectsThisTurn = new List<ICardEffect>()`), `SetFace()` `:348` (→ `IsFlipped = false` at `:78`),
`SetChangedLocationTime()` `:349` (→ `_changedLocationTime = DateTime.Now` at `:130-133`). It does **not** reset
`BaseDP` `:2377`, `willBeRemoveSources` `:2472`, `IsBeingRevealed` `:3565`, or
`PermanentJustBeforeRemoveField` `:3571` — DP-buff clearing is explicitly the caller's job (`SetDP` at `:2455`,
with the author's note at `:2453`). Ten callers: `CardController.cs:1361`, `:1438`, `:1739`;
`CardObjectController.cs:550`, `:627`, `:732`, `:773`, `:835`, `:929`, `:969`. **`AddSecurityCard` is the one
zone-add that deliberately does not call it.**

### 1.4 Plain mutable state (settable, non-computed)

There are **no** explicit `set { }` blocks on `Permanent` — every settable member is an auto-property or a public
field.

**`Permanent` — 41 pieces, all of which survive digivolution and all of which survive `RemoveField` except
`cardSources`:**

- *stack*: `cardSources` `:880`, `LinkedCards` `:1041`
- *DP/link*: `LinkedDP` `:670`, `Boosts` `:672` (`AddBoost` `:674`, `RemoveBoost` `:683`; `DPBoost` fields
  `:696 ID`, `:697 DP`, `:698 Condition`)
- *suspension*: `OldIsSuspended` `:1955`, `IsSuspended` `:1956`, `DPWhenSuspended` `:1958` (default `114514`)
- *timing/view*: `oldIsTapped_playCard` `:45`, `EnterFieldTurnCount` `:1640` (`= -1`),
  `ShowingPermanentCard` `:1644`, `battle` `:3182`, `willBeRemoveField` `:3434`
- *duration buckets* (10 `List<Func<EffectTiming, ICardEffect>>`): `:1575`, `:1579`, `:1583`, `:1587`, `:1591`,
  `:1595`, `:1599`, `:1603`, `:1607`
- *removal-cause tags*: `:3666 IsDestroyedByBattle`, `:3670 DestroyingEffect`, `:3674 PlaceOtherPermanentEffect`,
  `:3678 HandBounceEffect`, `:3682 LibraryBounceEffect`, `:3686 PlayingEffect`, `:3690 DigivolvingEffect`,
  `:3694 IsPlaceToTrashDueToNotHavingDP` (default **true**)
- *historical snapshots*: `:3890 LevelJustAfterPlayed`, `:3894 PlayCostJustAfterPlayed`,
  `:3898 CardNamesJustAfterPlayed`, `:3902 CardNamesJustAfterDigivolved`, `:3906 TraitsJustAfterPlayed`,
  `:3910 DPJustBeforeRemoveField`, `:3914 LevelJustBeforeRemoveField`, `:3918 CostJustBeforeRemoveField`,
  `:3922 CardNamesJustBeforeRemoveField`, `:3926 CardTraitsJustBeforeRemoveField`
- *play-mode flags*: `:3930 IsReturnedToHandByBurstDigivolution`, `:3934 IsAddedAsSourceByAppFusion`,
  `:3938 IsBurstDigivolved`, `:3942 IsAppFusion`, `:3946 IsPlayedOptionPermanent`

**`Player`:**
- *zones*: the seven `List<CardSource>` at `:498`, `:502`, `:506`, `:510`, `:514`, `:518`, `:522`
- *field*: `FieldPermanents` `:663`, `fieldCardFrames` `:602`
- *game state*: `IsLose` `:116`, `TurnCount` `:227`, `KeyCard` `:492`, `WinCount` `:724`, `PlayerID` `:738`,
  `DigivolveCount_ThisTurn` `:1176`, `isYou` `:529`
- *name/timing*: `_playerName` `:708` behind `PlayerName` `:709-718`; `_turnStartTime` `:953` behind
  `TurnStartTime` `:955-959` (written only by `SetTurnStartTime()` `:961-964`)
- *duration buckets* (8): `:918`, `:922`, `:926`, `:930`, `:934`, `:938`, `:942`, `:946`
- *private queues*: `mainPhaseActions` `:168`, `_playerSelectionQueue` `:193`, `_timerCount` `:131`,
  `_updateFrame` `:132`

`MemoryForPlayer` (`:973-985`) is **computed** from `GameContext.Memory` (`GameContext.cs:27`) with a sign flip
on `PlayerID == 0` — memory is not player state.

**`CardSource`:** `_cEntity_Base` `:13`, `cEntity_EffectController` `:50`, `_changedLocationTime` `:122`,
**`BaseDP` `:2377` (public mutable field — the DP-buff accumulator)**, `PhotonView` `:19`, `Owner` `:25`
(`{ get; private set; }`, **typed `Player`**), `CardIndex` `:31`, `IsFlipped` `:56`, `ShowingHandCard` `:327`,
`IsToken` `:2464`, `willBeRemoveSources` `:2472`, `IsBeingRevealed` `:3565`,
`PermanentJustBeforeRemoveField` `:3571` (a `Permanent` reference cached on a card).

The six members that read like state — `jogressCondition :2707`, `linkCondition :2727`, `digiXrosCondition :2959`,
`burstDigivolutionCondition :2987`, `appFusionCondition :3015`, `assemblyCondition :3043` — are all `{ get { … } }`
blocks deriving from `EffectList(EffectTiming.None)`. **They are computed, not cached.** The mirror's method
conversion (`BurstDigivolutionConditionOf()` etc.) is a syntactic difference only, not a semantic one.

### 1.5 Equality — no overrides anywhere

Searched the entire `DCGO/Assets/Scripts/` tree for `operator\s*==`, `public override bool Equals`,
`override int GetHashCode`, `IEquatable`. **Three hits, none on these three types**: `CEntity_Base.cs:357`
(`class EvoCost : IEquatable<EvoCost>`, `GetHashCode` `:378`) and `DataBase.cs:939` (an `IEqualityComparer`).

So:
- **`Permanent`** inherits `System.Object`. `==` is pure reference comparison. **Two `Permanent`s wrapping
  identical `cardSources` are NOT equal.**
- **`Player` and `CardSource`** inherit `UnityEngine.Object`, whose `==`/`Equals`/`GetHashCode` live in the engine
  assembly. Semantics: instance-ID comparison plus the "destroyed object compares equal to null" special case.
  Since neither type is ever destroyed (§1.2, §1.3), this is reference identity.

Consequence: **every** `List<CardSource>.Contains/Remove/IndexOf` and `List<Permanent>.Contains/IndexOf` in the
original resolves by reference. No `Dictionary<CardSource,…>`, `Dictionary<Permanent,…>` or `Dictionary<Player,…>`
exists anywhere (grepped). The only `ReferenceEquals` calls are `CEntity_Base.cs:370` and
`Hypertext/ObjectPool.cs:48`, neither on these types.

### 1.6 Where the original genuinely depends on reference identity

Representative, not exhaustive:

1. `Permanent.cs:33` — `Array.IndexOf(TopCard.Owner.FieldPermanents, this)`. `PermanentFrame` locates its own
   slot **purely by reference position**.
2. `CardObjectController.cs:537` — `if (permanent.TopCard.Owner.FieldPermanents[i] == permanent)`. The only way
   `RemoveField` finds the slot to null.
3. `Player.cs:1571-1601` — `FieldCardFrame.framePermanent` is a raw reference; `IsEmptyFrame()` is
   `GetFramePermanent() == null` (`:1600`).
4. `CardSource.cs:337-338` — `PermanentOfThisCard()` = `Owner.GetFieldPermanents().Find(p => p.cardSources.Contains(this))`.
   The card→permanent link is a reference scan, on the hot path of nearly every effect.
5. `CardObjectController.cs:392-401`, `:407-446` — the entire zone system is `Contains`/`Remove` by reference.
6. `Permanent.cs:1359` — `TopCard` = `cardSources.First(source => !LinkedCards.Contains(source))`;
   `:888` `DigivolutionCards` = `cardSources.Filter(cs => cs != TopCard && !LinkedCards.Contains(cs))`.
   **Stack-role classification is `!=` on `CardSource` references.**
7. `Player.cs:752,756` — `Enemy` = `gameContext.Players.Contains(this)` then first `player != this`.
8. `CardEffectCommons.cs:831/879/927/1171/1219/1267`, `PermanentEffectFactory.cs:125/141/153` —
   `return permanent == targetPermanent;` in targeting predicates.
9. `CardController.cs:2287/:2453/:2614/:3659/:5568` — `DeckBouncedPermanents.Contains(permanent)`,
   `DestroyedPermanents.Contains(…)`, `SuspendedPermanents.Contains(…)`: batch bookkeeping keyed by reference.
10. `Permanent.cs:1073` — `if (cardSources.Contains(addedDigivolutionCard)) isFromSameDigimon = true;`.
11. `CardObjectController.cs:607` — `addedCards.Map(cs => cs.Owner).Distinct()` — `Distinct()` on `Player` uses
    the default comparer, i.e. reference identity.

**⚠ Mixed-identity hazard already present in the original.** Six sites construct throwaway `Permanent` wrappers
over a *live* permanent's `cardSources` and place them into effect hashtables: `CardController.cs:4541`, `:4549`,
`:4682`, `:4690`; `AttackProcess.cs:99`; `HashtableSetting.cs:26`, `:57`. Any `permanent == targetPermanent`
(item 8) evaluated against such a snapshot is **false** even though it denotes the same board object — and per
§1.2 those snapshots additionally have their stack order **reversed**, so their `TopCard` is the bottom source.
I found no normalisation mapping a snapshot back to the field object. **This is a genuine property of the
original.** The target model reproduces it; the current mirror does not (see §3.5).

---

## 2. What the mirror is now

All citations in this section are into `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/`.
Per the brief, mirror comments are ignored — every claim below was verified against code.

### 2.1 The three view types

All three live in namespace `HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons`.

| Type | Decl | Constructors | Fields actually held |
|---|---|---|---|
| `Player` | `Assets/Scripts/Script/Player.cs:23` `public class Player : MonoBehaviour` | `(EngineContext, HeadlessPlayerId)` `:25-35` | `Context` `:37`, `PlayerId` `:39` |
| `CardSource` | `Assets/Scripts/Script/CardSource.cs:21` `public class CardSource : MonoBehaviour` | `(EngineContext, HeadlessEntityId instanceId, HeadlessPlayerId controller, HeadlessPlayerId? owner = null)` `:23-40` | `Context` `:42`, `InstanceId` `:44`, `Controller` `:46`, `Owner` `:48` |
| `Permanent` | `Assets/Scripts/Script/Permanent.cs:45` `public class Permanent` | `(EngineContext, HeadlessEntityId, HeadlessPlayerId, ChoiceZone? snapshotZone = null)` `:49-55`; `(EngineContext, HeadlessEntityId)` `:61-69` | `_context` `:47`, `InstanceId` `:71`, `OwnerId` `:73`, `SnapshotZone` `:86` |

`MonoBehaviour` is the local shim `Headless/Unity/UnityEngineMonoBehaviour.cs:5`.

Three facts follow immediately:

- **`Permanent` is keyed on its TOP CARD's `InstanceId`.** It has no identity of its own. Under §1.3, ordinary
  digivolution changes the top card — so **in the mirror, digivolution changes the permanent's identity**, which
  the original explicitly does not. This is the root of the `PermanentBookkeepingStore.ReKey` machinery (§2.3).
- **`CardSource.Owner` is a `HeadlessPlayerId`, not a `Player`** — the direct cause of all 17 S7 rows.
- **`CardSource.Controller` (`:46`) has no AS-IS counterpart.** The original has only `Owner` (`CardSource.cs:25`).
  It is an invented member. [실측 — grepped `Controller` in DCGO `CardSource.cs`, no such member.]

Also note `Player.isYou` at `Player.cs:106` — a plain public field on a per-access view. It can never hold a
value across two reads. It is the same defect class as `IsSelecting` (§2.4), currently benign only because
nothing headless writes it.

### 2.2 The equality overrides that exist only to fake identity

All three types carry hand-written value equality. None is a `record`. Verbatim:

```csharp
// Player.cs:49-57
public override bool Equals(object? obj) =>
    obj is Player other && PlayerId.Equals(other.PlayerId) && ReferenceEquals(Context, other.Context);
public override int GetHashCode() => PlayerId.GetHashCode();
public static bool operator ==(Player? left, Player? right) => left is null ? right is null : left.Equals(right);
public static bool operator !=(Player? left, Player? right) => !(left == right);

// CardSource.cs:56-64        — identical shape, keyed on InstanceId
// Permanent.cs:96-104        — identical shape, keyed on InstanceId (i.e. on the TOP CARD)
```

The original has no such overrides anywhere (§1.5). These exist solely because the views are reconstructed per
access and would otherwise be reference-unequal.

### 2.3 The side stores — exhaustive census

**(a) The primary entity table.** `CardInstanceRecord` is a `sealed record`
(`Headless/Services/CardInstanceRecord.cs:5`) whose only extensibility point is
`IReadOnlyDictionary<string, object?> Metadata` (`:46`, defensive copy `:52`), mutated only by whole-record
replacement through `ICardInstanceRepository.Upsert` (`Headless/Services/ICardInstanceRepository.cs:5`, read via
`TryGetInstance` `:7`). Implementation `Headless/Services/InMemoryCardInstanceRepository.cs` (reset `:28-31`).
This dictionary carries roughly 35 per-card fields with no schema.

**(b) `ConditionalWeakTable` stores.** [실측 — full sweep of `ConditionalWeakTable<` in the engine]

| Store | Decl | Keyed by | Holds | AS-IS field it stands in for |
|---|---|---|---|---|
| `PermanentBookkeepingStore` | `Headless/State/PermanentBookkeepingStore.cs:95` | `ICardInstanceRepository` → `Dictionary<HeadlessEntityId, PermanentBookkeepingEntry>` | 11 fields (`:41-79`) | `Permanent` `PlayingEffect :3686`, `DigivolvingEffect :3690`, `PlaceOtherPermanentEffect :3674`, `LevelJustAfterPlayed :3890`, `PlayCostJustAfterPlayed :3894`, `CardNamesJustAfterPlayed :3898`, `CardNamesJustAfterDigivolved :3902`, `TraitsJustAfterPlayed :3906`, `IsBurstDigivolved :3938`, `IsAppFusion`, `IsReturnedToHandByBurstDigivolution :3930` |
| `DigivolutionStackReader.Cache` | `Headless/State/DigivolutionStackReader.cs:42` | `CardInstanceRecord` | parsed stack cache | (derived read of `Permanent.cardSources`) |
| `Player._mainPhaseActionStore` | `Assets/Scripts/Script/Player.cs:237` | `EngineContext` → `Dictionary<HeadlessPlayerId, Queue<MainPhaseAction>>` | the main-phase intent queue | `Player.mainPhaseActions :168` |
| `PlayerEffectListStore` | `Assets/Scripts/Script/Player.cs:1022` | `EngineContext` | the 8 player duration buckets | `Player :918-946` |
| `PlayerSelectionQueueStore` | `Assets/Scripts/Script/Player.cs:1039` | `EngineContext` | `_playerSelectionQueue` | `Player :193` |
| `PermanentEffectListStore` | `Assets/Scripts/Script/Permanent.cs:5879` | `EngineContext` → `ConcurrentDictionary<instanceId, …>` | the 9 permanent duration buckets | `Permanent :1575-1607` |
| `CEntity_EffectControllerStore` | `Assets/Scripts/Script/CEntity_EffectController.cs:347` | `EngineContext` → `ConcurrentDictionary<HeadlessEntityId, …>` | per-card effect controller | `CardSource.cEntity_EffectController :50` |
| `CEntityUseCycle.ByContext` | `Assets/Scripts/Script/CEntity_EffectController.cs:449` | `EngineContext` | use-count/cap cycle | `CEntity_EffectController.UseEffectsThisTurn` |
| `TurnStateMachine._isExecutingStore` | `Assets/Scripts/Script/TurnStateMachine.cs:50` | `EngineContext` → `StrongBox<bool>` | `isExecuting` | `TurnStateMachine.isExecuting :23` |
| `TurnStateMachine._passedStore` | `Assets/Scripts/Script/TurnStateMachine.cs:64` | `EngineContext` → `StrongBox<bool>` | `Passed` | `TurnStateMachine.Passed :3150` |
| `GameContext._isSecurityLookingStore` | `Assets/Scripts/Script/GameContext.cs:65` | `EngineContext` → `StrongBox<bool>` | `isSecurityLooking` | `GameContext` |

**(c) `EngineContext`-registered service stores** — a second, distinct mechanism reached through
`TryGetService`/`RegisterService` (`EngineContext.cs:273`, `:293`):
`OldIsTappedPlayCardStore` (`Permanent.cs:1736`, accessed `:1721-1730`),
`DestroyingEffectStore` (`Permanent.cs:1810`),
`HandBounceEffectStore` (`Permanent.cs:1835`),
`LibraryBounceEffectStore` (`Permanent.cs:1860`),
`PermanentJustBeforeRemoveFieldStore` (`CardSource.cs:2433`).

**(d) Other per-entity/per-player controllers:**
`PlayerTurnCounterController` (`Headless/Runtime/PlayerTurnCounterController.cs:13`, `DigivolveCountKey` `:16`,
`Increment` `:25`) standing in for `Player.DigivolveCount_ThisTurn` (`Player.cs:1176`);
`SecurityFaceState` (`Headless/Runtime/SecurityFaceState.cs:25` `FaceUpKey`, `:67`) standing in for
`CardSource.IsFlipped`; `InMemoryHeadlessPlayerStatusController` (lose flags, standing in for `Player.IsLose`);
`InMemoryZoneMover._zones` — **the state of record for every zone**, standing in for the seven `List<CardSource>`
fields on `Player`.

**(e) Metadata key constants: 75 `const string …Key = "…"` declarations in the engine** [실측:
`grep -rn 'const string [A-Za-z]*Key = "'` excluding `obj/` → 75]. The per-entity subset standing directly in for
AS-IS live fields, all in `Permanent.cs` / `CardController.cs` / `CardSource.cs`:
`SourceIdsKey :4869`, `IsSuspendedKey :4873`, `CanSuspendKey :4876`, `CannotBeDeDigivolvedKey :4881`,
`DeletedByBattleMetadataKey :4885`, `DeletedByEffectMetadataKey :4886`, `DeletedByOwnEffectMetadataKey :4887`,
`PendingDeletionMetadataKey :4888`, `PlaceToTrashDueToNoDpKey :4893`, `IsPlayedOptionPermanentKey :4897`,
`LinkedCardIdsKey :4928`, `LinkedDpKey :4931`, `LinkedMaxKey :4935`, `LinkDpKey :4938`, `DpBoostsKey :2556`,
`DpJustBeforeRemoveFieldKey :1876`, `LevelJustBeforeRemoveFieldKey :1878`, `CostJustBeforeRemoveFieldKey :1880`,
`CardNamesJustBeforeRemoveFieldKey :1882`, `CardTraitsJustBeforeRemoveFieldKey :1884`,
`WillBeRemoveSourcesKey` (`CardController.cs:1345`), `DpWhenSuspendedKey` (`CardController.cs:1792`),
`IsAceKey :2178`, `OverflowMemoryKey :2181`, `IsFlippedKey :2185`, plus
`ZoneMoveMetadataKeys.EnteredThisTurnKey` (`Headless/Services/ZoneMoveMetadataKeys.cs:16`) and raw literals
`"willBeRemoveField"`, `"isBeingRevealed"`, `"baseDp"`, `"isToken"`.

Plus the two writer/reader helpers with **no AS-IS counterpart at all**: `WithLinked` (`Permanent.cs:5042`) and
`ReadLinkMetadataInt` (`Permanent.cs:5059`).

### 2.4 The lifetime-emulation machinery, and the two confirmed live defects

**`PermanentBookkeepingStore` is a hand-rolled emulation of object identity across a top swap.** Its own header
(`:9-18`) states the mapping: CREATE = `Reset(topId)` on field entry, PERSIST = **`ReKey(oldTop, newTop)`** at
every op that owns a top swap, DIE = `Reset(topId)` on field exit. The chokepoint is
`InMemoryZoneMover.MoveCard`, which resets on any field-membership change — except when the move carries
`PermanentBookkeepingStore.PermanentContinuityKey = "permanentContinuity"` (`:88`, shared bag `:92-93`), the
marker meaning "this leave+enter pair is one top swap; the AS-IS object persists". Written at `Permanent.cs:4346`,
`:4354`, `:4725`, `:4731`, `:5460`, `:5464` and `CardEffectCommons.cs:896`, `:899`; read at
`InMemoryZoneMover.cs:482`, suppressing the reset at `:484`.

**This entire mechanism exists to simulate the one sentence in §1.3: the `Permanent` object survives
digivolution.** In the target model it is deleted, not ported.

**Defect (a) — `endGame` writes land on a throwaway.** [실측]
`GManager.instance` is `AmbientMatchContext.Current is { } context ? new GManager(context) : null`
(`Assets/Scripts/Script/GManager.cs:53-54`) — **a fresh `GManager` per read**. Its ctor sets
`turnStateMachine = TurnStateMachine.For(context)` (`GManager.cs:28`), and
`TurnStateMachine.For` is `return new TurnStateMachine(context);` (`TurnStateMachine.cs:1008-1012`) — **a fresh
`TurnStateMachine` per read**. `endGame` is a plain auto-property on that view
(`TurnStateMachine.cs:112 public bool endGame { get; set; } = false;`).

The pump captures **one** instance in a local — `Cec.TurnStateMachine turnStateMachine = Cec.TurnStateMachine.For(context);`
(`Headless/Runtime/TurnFlowPump.cs:263`) — and its loop condition reads that instance:
`while (!turnStateMachine.endGame && …)` (`:286`), plus `Ended()` at `:334`.

But `AttackProcess.cs:556` calls `GManager.instance.turnStateMachine.EndGame(attackerOwner, false);` — a
throwaway. `EndGame` sets `endGame = true` (`TurnStateMachine.cs:996`) on that throwaway and it is discarded.
The pump never sees it. The match still terminates, but only via the *other* half of the `&&` —
`context.RuleQueryService.IsTerminal()`, fed by the `PlayerStatusController.MarkLose` side-effect at
`TurnStateMachine.cs:1000` which does reach the substrate. So the flag is dead and the termination is accidental.
(The sibling call at `TurnStateMachine.cs:437` is `EndGame(...)` on `this` and is therefore *conditionally* fine —
correct only when `this` happens to be the pump's captured instance.)

**Defect (b) — six `IsSelecting` writes discarded.** [실측] `IsSelecting` is declared
`TurnStateMachine.cs:109 public bool IsSelecting { get; set; } = false;` — again a plain property on a per-access
view. Exactly six writes go through `GManager.instance`:

| # | Site |
|---|---|
| 1 | `Assets/Scripts/Script/AttackProcess.cs:399` |
| 2 | `Assets/Scripts/Script/AttackProcess.cs:417` |
| 3 | `Assets/Scripts/Script/AttackProcess.cs:575` |
| 4 | `Assets/Scripts/Script/CardController.cs:4698` |
| 5 | `Assets/Scripts/Script/CardController.cs:4995` |
| 6 | `Assets/Scripts/Script/CardController.cs:5154` |

Every one is `GManager.instance.turnStateMachine.IsSelecting = true;` on a freshly minted view. Three further
writes — `TurnStateMachine.cs:471`, `:512`, `:612` — are on `this` and therefore land wherever `this` came from.
AS-IS declares `public bool IsSelecting = false;` at `DCGO/…/TurnStateMachine.cs:20` and reads it at
`NextPhaseButton.cs:46` and `:108`.

`TurnStateMachine` is not one of the three types in scope, but it is the *same defect*: a plain mutable field on
a type whose instances are minted per access. The design must therefore state a rule that covers it (§3.7).

### 2.5 Call-site volume

[실측] Commands run from `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine`, `grep -rn --binary-files=text
'<pattern>' --include=*.cs .` with `/obj/` filtered:

| Pattern | Total | Under `Assets/Scripts/CardEffect/` | Elsewhere |
|---|---|---|---|
| `new Player(` | 626 | 445 | 181 |
| `new CardSource(` | 66 | 13 | 53 |
| `new Permanent(` | 121 | 17 | 104 |

`find ./Assets/Scripts/CardEffect -name '*.cs' | wc -l` → **4009** files.

Distribution of the type names themselves (non-comment lines, per the earlier survey):
`Player` 669 in `Assets/` vs **31** in `Headless/`; `CardSource` 2623 vs **12**; `Permanent` 2496 vs **13**.
All four downstream projects (`HeadlessDCGO.Rl`, `RuleAudit`, `RlBridgeHost`, `RlVectorHost`) reference these
three types **zero** times — they talk only to `DcgoMatch`, `EngineContext`, `HeadlessPlayerId`, `LegalAction`,
`ObservationSnapshot`. **The blast radius outside `Assets/Scripts/` is very small.**

---

## 3. The target model

### 3.1 Ownership

One new substrate type, following the pattern already established for every other per-match global in this
codebase (§4.4):

```
Headless/State/MatchEntityTable.cs
    static readonly ConditionalWeakTable<EngineContext, MatchEntityTable> ByContext

    sealed class MatchEntityTable
        Player[]                                Seats          // exactly 2, minted at match init
        Dictionary<HeadlessEntityId, CardSource> Cards         // minted at deck build / token creation
        // Permanents are NOT held here — see §3.4
```

**Not a `static Dictionary`.** A process-wide static registry would be the first thing in this engine to break
in-process parallel workers (`RlVectorHost --mode tasks`, `Program.cs:109-112`); the `ConditionalWeakTable<EngineContext, …>`
keying is what every existing store already does and is the reason the current parallel-determinism claim holds
(§4.4). This is a hard constraint, not a preference.

### 3.2 `Player` — construction, lookup, lifetime

- **Two instances per match**, created at match initialisation (the mirror equivalent of scene load), seated
  against the two `HeadlessPlayerId`s from `Context.TurnController.Current.PlayerOrder`.
- **Lookup:** `Player.For(EngineContext, HeadlessPlayerId)` returns the canonical instance. The public
  `(EngineContext, HeadlessPlayerId)` constructor becomes `private`.
- **Lifetime:** the whole match. Never destroyed, never re-created. `ResetMatchState` (`EngineContext.cs:319-335`)
  re-seats them (the reuse path is currently dead — §4.3 — but the seam must exist).
- **State:** the fields in §1.4 become plain fields/auto-properties on that instance. `PlayerEffectListStore`,
  `PlayerSelectionQueueStore`, `_mainPhaseActionStore`, and `PlayerTurnCounterController.DigivolveCountKey` are
  deleted; their contents become fields.
- **`isYou`, `IsLose`, `WinCount`, `PlayerName`, `TurnStartTime`, `DigivolveCount_ThisTurn`** become writable
  and actually retain values. `IsLose` continues to *also* drive `PlayerStatusController` because the terminal
  verdict is read by the substrate (`RuleQueryService.IsTerminal()`); the field is the AS-IS surface, the
  controller call is the substrate projection. Both, not either.
- **Zone lists** (`LibraryCards` … `ExecutingCards`) become the real `List<CardSource>` fields, held by the
  `Player`. See §3.6 — this is the highest-risk part.

### 3.3 `CardSource` — construction, lookup, lifetime

- **One instance per physical card**, minted exactly where the original mints it: the mirror
  `CardObjectController.CreateCardSource` (mirror `CardObjectController.cs:124`) and token creation. The mirror
  already has both call paths (`CreatePlayerDecks` at `:72`, per AS-IS `:16-341`).
- **Lookup:** `CardSource.For(EngineContext, HeadlessEntityId)`. Public constructor becomes `private`.
  `HeadlessEntityId` remains the wire/substrate currency — nothing about the ids changes.
- **Lifetime:** the whole match. **A zone move never re-creates it** — this is already what the mirror's zone
  mover does to the *record*; the change is that the same *object* is what moves.
- **State:** `BaseDP`, `IsFlipped`, `willBeRemoveSources`, `IsBeingRevealed`, `PermanentJustBeforeRemoveField`,
  `IsToken`, `CardIndex`, `_changedLocationTime`, `cEntity_EffectController` become plain fields.
  `CEntity_EffectControllerStore`, `CEntityUseCycle`, `SecurityFaceState`, `PermanentJustBeforeRemoveFieldStore`,
  and the `"baseDp"`/`"isToken"`/`"isBeingRevealed"`/`IsFlippedKey`/`WillBeRemoveSourcesKey` metadata are deleted.
- **`Owner` is re-typed `Player`** (matching `DCGO/…/CardSource.cs:25`). The invented `Controller` member (§2.1)
  is deleted — the original has no such concept; every read of it must be re-derived from the AS-IS source of
  that answer before deletion, and any that cannot be is a defect to log, not to silently keep.
- **`Init()` acquires a body** — the exact three statements of `CardSource.cs:345-350` and nothing more. Every one
  of the ten AS-IS call sites is restored (the three currently-removed ones are TSV rows `r040`, `r044`, `r058`).
- **`ActiveCardList`.** The original's never-cleared global index (`GameContext.cs:31-34`) is the natural home for
  `MatchEntityTable.Cards`. Restoring it as a real `List<CardSource>` on the mirror `GameContext`, appended at
  `CreateCardSource`, gives the AS-IS surface *and* the lookup table for free. `CardIndex` addressing
  (`TurnStateMachine.cs:3076` etc.) becomes expressible again.

### 3.4 `Permanent` — the hard one

`Permanent` has **no id in the original**. Its identity is the object reference, and its registry is
`Player.FieldPermanents[]` (§1.2). Two consequences that the design must accept rather than route around:

**(a) The slot array is restored as real state.** `Player.FieldPermanents` becomes a real `Permanent?[]`, and
`Player.fieldCardFrames` a real `List<FieldCardFrame>`. Without them, items 1, 2, 3 of §1.6 have no expression:
`PermanentFrame` *is* `Array.IndexOf(FieldPermanents, this)`. The mirror's current
`FrameID = index in GetFieldPermanents()` (`Permanent.cs:118-…`) is a compacted-list adaptation that changes
answers whenever a slot is empty; it is retired here.

**(b) `Permanent` instances are NOT registered by id.** They are reachable only the way the original reaches
them — through `FieldPermanents[]`, through `FieldCardFrame.framePermanent`, through `cardSources`, and through
whatever reference an effect happens to be holding. `MatchEntityTable` does not index them. Any code that today
does "give me the Permanent for entity id X" must be rewritten to the AS-IS route:
`CardSource.PermanentOfThisCard()` = `Owner.GetFieldPermanents().Find(p => p.cardSources.Contains(this))`
(`DCGO/…/CardSource.cs:337-338`).

**Construction:** the sole ctor `Permanent(List<CardSource>)` is restored, **including its order-reversing
`SetCardSources` behaviour** (§1.2). Reproducing this is not optional — the six snapshot sites depend on it.

**Lifetime:**
- CREATE — `new Permanent(...)` at the play site (mirror `CardController` play path, AS-IS `:1383`), installed by
  `CardObjectController.CreateNewPermanent(permanent, frameID)` writing `FieldPermanents[frameID] = permanent`
  and `SetFramePermanent(permanent)` (AS-IS `:489`, `:491`).
- DIGIVOLUTION — **nothing happens to the object.** `permanent.AddCardSource(card)` (AS-IS `:1374`). The
  `Permanent` keeps its slot, its frame back-reference, and all 41 fields.
- DNA/JOGRESS — a genuinely new `Permanent` replaces the old (AS-IS `:1496`, `:1498`).
- DIE — `RemoveField` (AS-IS `:513-555`) nulls the slot and the frame, calls `Init()` on each source, and
  replaces `cardSources` with a fresh empty list. **The object is not destroyed and may still be referenced.**
  The early-return at `:516` and the `IsEmptyFrame` / `GetFieldPermanents` disagreement (§1.3) are reproduced,
  not fixed.

**Deleted by this stage:** `PermanentBookkeepingStore` in its entirety, `PermanentContinuityKey`, `ReKey`, the
`InMemoryZoneMover.cs:482-484` continuity suppression, `PermanentEffectListStore`, `OldIsTappedPlayCardStore`,
`DestroyingEffectStore`, `HandBounceEffectStore`, `LibraryBounceEffectStore`, and the ~20 `Permanent.cs` metadata
key constants listed in §2.3(e), together with `WithLinked` and `ReadLinkMetadataInt`.

### 3.5 `==` / `Equals` / `GetHashCode` — delete them, at a precisely defined moment

**Endpoint: all twelve members (four per type, `Player.cs:49-57`, `CardSource.cs:56-64`, `Permanent.cs:96-104`)
are deleted.** The original has none (§1.5); reference identity is the correct and complete semantics once an id
resolves to one instance.

**Timing is load-bearing.** For each type the override must be deleted in the *same* change that makes the type
canonical — not earlier, not later:

- **Delete earlier** → every `Contains` / `==` in the mirror silently becomes false. Catastrophic and silent.
- **Delete later** → worse than it sounds, because the override does not merely duplicate reference equality; for
  `Permanent` it *contradicts* it in two directions:
  - it compares by **top-card id**, so a `Permanent` before digivolution and the same object after would compare
    **unequal** — the opposite of §1.3;
  - it makes a **snapshot** `new Permanent(p.cardSources)` compare **equal** to the live `p` — the opposite of
    §1.6's confirmed AS-IS hazard.

  So keeping it after the type is canonical actively masks the very behaviours the migration is restoring.

**Behavioural change to expect and to watch for [판단].** Deleting `Permanent`'s override *changes live outcomes*
at the six snapshot sites of §1.6: comparisons that succeed today will start failing, which is AS-IS-correct but
will look like a regression. Every one of `CardController.cs:4541`, `:4549`, `:4682`, `:4690`,
`AttackProcess.cs:99`, `HashtableSetting.cs:26`, `:57` should be enumerated and individually reasoned about in
the stage that deletes the override. This is the single highest-risk behavioural delta in the whole migration and
it must not be discovered by accident.

`Player`'s override is the least risky (two fixed seats; reference and value identity coincide once the seats are
canonical). `CardSource`'s is next. `Permanent`'s is the dangerous one.

### 3.6 Zone lists: which side becomes the state of record

Today `InMemoryZoneMover._zones` is the state of record and `Player.HandCards` is a projection —
`zones.GetCards(PlayerId, ChoiceZone.Hand).Select(id => new CardSource(...)).ToList()`
(`Player.cs:162-175`, same shape at `:181`, `:199`, `:217`, `:281`, `:303`). Every getter allocates a fresh list
of fresh views, so no `Add`/`Remove`/`Insert` on the result is observable, and `Contains(cardSource)` only works
because of the equality override (§2.2).

The original's state of record is the seven `List<CardSource>` fields (§1.2). The target model must make the
`Player` lists authoritative, because AS-IS mutates them directly — `HandCards.Remove(cardSource)`
(`CardObjectController.cs:409`), `TrashCards.Insert(0, cardSource)` (`:729`),
`player.LibraryCards = shuffled` (`:1015`).

**Direction: `Player`'s lists become authoritative; `IZoneStateReader.GetCards` becomes a projection over them.**
The inverse (keeping `_zones` authoritative and giving `Player` a live-view wrapper) is rejected because it cannot
express `player.LibraryCards = <new list>` at `:1015` and cannot make `Insert(0, …)` positional semantics
(`AddTrashCard`, `AddSecurityCard`, `AddLibraryTopCards`) come out right without re-implementing them twice.

**This is the highest-risk stage of the migration**, because `InMemoryZoneMover` is not only a store — it is the
lifetime chokepoint that today drives `PermanentBookkeepingStore` reset (§2.4), the `EnteredThisTurn` stamp
(`ZoneMoveMetadataKeys.cs:16`; writers `CardObjectController.cs:257`, `TurnFlowPump.cs:364`, `Permanent.cs:3297`,
`:4431`), and the zone-event stream `_events` that `MatchEventLog` consumes. Each of those responsibilities has to
be re-seated, on AS-IS terms, before `_zones` stops being authoritative. Sequencing it after `Permanent` (which
deletes the bookkeeping-reset responsibility outright) removes the largest of the three.

### 3.7 The general rule this establishes

**No plain mutable field may live on a type whose instances are minted per access.** Today the codebase violates
this in at least three places beyond the three subject types: `TurnStateMachine.endGame` and `IsSelecting`
(§2.4), and `Player.isYou` (§2.1). The mirror's own workaround for exactly this problem —
`TurnStateMachine._isExecutingStore` / `_passedStore` (`:50`, `:64`) — proves the pattern was recognised and
applied inconsistently.

`TurnStateMachine`, `GameContext`, `GManager`, `AttackProcess`, `AutoProcessing` and `ContinuousController` are all
per-access views today. They are **out of scope** for this design, but the same treatment (one canonical instance
per match, held in `MatchEntityTable`) is the obvious follow-on, and §5 Stage 2 makes it cheap by building the
table generically rather than `Player`-specifically.

---

## 4. Interaction with what already exists

### 4.1 The pump holds a C# stack across agent turns — this model is compatible, and improves it

`TurnFlowPumpTask._running` (`Headless/Runtime/TurnFlowPump.cs:183`) is a single `Task` holding the whole game as
one async method (`RunAsync` `:255-330`). A park stores a `TaskCompletionSource` in `TurnFlowGate._park` (`:41`);
`TryRelease()` (`:71-81`) completes it **synchronously on the caller's stack**, so the parked body resumes in
place. The step contract is hard: if the pump neither completes nor re-parks within a step it **throws** (`:245-246`).

**Effect on this design: none, and one direct benefit.** Live objects are strictly *better* for a model where
the C# stack is the state of the in-flight turn: a local variable holding a `Permanent` across an `await` is
today a stale id-pinned view (TSV rows `r166`, `r176`, `r219` all rebuild `new Permanent(context, currentTopId, …)`
per loop iteration precisely because the view cannot observe its own mutation); after the change it is a live
reference that sees the mutation, exactly as an AS-IS coroutine local does.

The pump's own `Cec.TurnStateMachine turnStateMachine` local (`:263`) is the exact same pattern — and is the
reason defect (a) in §2.4 is invisible. Making the entity model live does not fix `TurnStateMachine`; §3.7 covers
it separately.

### 4.2 Nothing serialises match state, so nothing breaks — but the ceiling changes

[실측] There is **no round-trippable serialisation of match state** anywhere in the five projects. No Newtonsoft,
no MessagePack, no protobuf, no `BinaryWriter`, no live `[Serializable]`. Zero hits for `DeepCopy`,
`MemberwiseClone`, `ICloneable`, `Rollback`, `Undo`, `Checkpoint`. No `Save*` method exists.

What crosses the wire is a **read-only projection**: the observation vector and action mask
(`RlTrainingDatasetJsonlExporter.cs`, `ObservationEncoder.cs`, `FactoredActionEncoder.cs:179-201`), the seat
protocol's hand-built `JsonObject`s (`SeatMatchHost.cs` `welcome :133`, `schema :163`, `turn :405`, `result :439`),
and the append-only event log (`MatchEventLog.cs:99-122`). `ObservationSnapshot` cannot rehydrate a match: hidden
zones reduce to `Count` (`HeadlessGameLoop.cs:346-360`), the whole effect dimension is hardwired empty (`:215-217`,
`:225`), there is no continuation state, and `EngineContext.CurrentState` has a `private set` (`:226`).

`Headless/State/MatchState.cs` looks like a state model but is **vestigial**: its only consumers are
`StateFingerprintService` and `GameContextStateAccessor`, both with zero live callers, and its mutable bags'
writers (`PlayerState.SetFlag :60`, `CardInstanceState.SetFlag :153`) have **zero callers repo-wide**. A new
entity model must not treat it as the state of record.

**So: no serialisation path breaks. [실측]**

**But say it plainly — this does make match state harder to snapshot.** Today the live state is a flat, acyclic
`id → immutable record` table plus a zone map: mechanically snapshottable, if anyone ever wanted to. The target
model is an object graph with cycles by construction — `Player.FieldPermanents[i].cardSources[j].Owner` closes a
loop, and `CardSource.PermanentJustBeforeRemoveField` (`DCGO/…/CardSource.cs:3571`) is a *deliberately dangling*
back-reference. Any future snapshot/rollback/MCTS work would need a graph walk with identity preservation instead
of a dictionary copy. That is a real cost and it is being paid knowingly.

It is not, however, a *new* blocker: even today a snapshot would be incomplete, because (i) the RNG's mid-stream
position is unrecoverable — `GameRandomSource._s0.._s3` are private with no getter (`GameRandomSource.cs:39-42`),
only the original `CurrentSeed` is readable (`:49`) — and (ii) the parked pump's C# stack cannot be captured at
all. Determinism in this repo is achieved by **re-derivation from a seed**, verified by fingerprint comparison
(`HeadlessEpisodeFingerprint.cs`, `HeadlessDeterminismVerifier.cs`), never by state diff. That remains true.

### 4.3 Reset

Two mechanisms exist; only one is used.

- **Path B (live):** every production host builds a **brand-new** `EngineContext` + `DcgoMatch` per episode —
  `SeatMatchHost.cs:223` (`CreateDefault(randomSeed: seed, …)`) then `:280-284` `CreatePumpDriven` +
  `InitializeAsync`; `HeadlessEpisodeBatchRunner.cs:93-94`; `RuleAudit/Program.cs:60`, `:73-75`.
  **This model requires nothing of Path B.** A fresh context ⇒ a fresh `MatchEntityTable` ⇒ fresh entities. The
  `ConditionalWeakTable` keying means the old table is collected with the old context, with no explicit teardown.
- **Path A (`DcgoMatch.ResetAsync` `:180-212`, reuse in place): exactly one caller,
  `HeadlessRlEnvironment.cs:54`, and no production host reaches it.** [실측] For completeness the design must add
  entity re-seating to `EngineContext.ResetMatchState()` (`:319-335`) — re-mint the two `Player`s and clear
  `Cards` — because after the change the entities *are* the state. Not doing so would make the reuse path
  silently carry state across episodes.

  Note this path already carries a latent bug independent of this work: `MatchEventLog._zoneCursor` is set only in
  `Attach` (`MatchEventLog.cs:34-38`, called once from `DcgoMatch.cs:51-52`) and `ResetAsync` clears
  `InMemoryZoneMover._events` (`:386`) without rewinding it. Reviving Path A means fixing that too.

### 4.4 Concurrency — the binding constraint

Concurrent matches **do** run in one process: `RlVectorHost --mode tasks` (`Program.cs:109-112`
`Task.Run` per worker, `:131-133` `new InProcTransport()` per worker, `:885-888` one `SeatMatchHost` each) and
`HeadlessEpisodeBatchRunner` (`:53-62`, though `MaxDegreeOfParallelism` defaults to `1` at `:147`).

The engine's isolation rests on two mechanisms, both of which this design must respect:
`AsyncLocal<EngineContext?>` for the ambient (`AmbientMatchContext.cs:18`), and
`ConditionalWeakTable<EngineContext, …>` for every store (§2.3(b)).

**Therefore `MatchEntityTable` is keyed on `EngineContext`, never static.** (§3.1.)

One inherited caveat to preserve, not worsen: `Player.cs:237` and `PermanentBookkeepingStore.cs:95` hold plain
`Dictionary` inside the weak table — safe for the observed topology (one match per worker, one worker per
context, never two threads on one `EngineContext`), unsafe if a single match were ever driven from two threads.
`MatchEntityTable` inherits the same assumption and should carry the same explicit note. There is **no `lock`
anywhere in the Engine** [실측], and the pump's synchronous-completion invariant (`TurnFlowPump.cs:245-246`)
depends on there being none.

The only genuinely process-shared mutable state in the engine today is the nine `Tfx*` test-fixture statics
(`Assets/Scripts/CardEffect/TestFixtures/Tfx*.cs`). This design adds nothing to that list.

### 4.5 Observations and legal actions

Observation building is ~99% substrate reads (`HeadlessGameLoop.cs:207-229`, `:322-366`;
`CardObservation.cs:51-95` reading `instance.Metadata`). Exactly **one** mirror-domain read exists:
`CardObservation.cs:112` `new …Permanent(context, instance.InstanceId, instance.OwnerId).DP` for field cards.
Under the target model that line becomes a lookup through the owning `Player`'s `FieldPermanents` /
`PermanentOfThisCard()` instead of a construction. `Player` and `CardSource` are read nowhere in the observation
builders. **So the observation vector is essentially unperturbed.**

**Legal-action enumeration is the real mirror-dependency surface.** `HeadlessGameLoop.GetLegalActions` (`:200-205`)
merges `RuleQueryService.GetLegalActions` (substrate) with `_legalActionDispatcher.GetLegalActions`, whose lane
bodies consult mirror getters (e.g. `CardSource.CanPlayFromHandDuringMainPhase`, `CardSource.cs:1004-1012`).
Changes to `Player`/`CardSource`/`Permanent` move this surface directly. [부분검증 — the earlier survey read the
choice lane, the availability gate `:248-260`, and the PlayCard lane, not all ~2000 lines of the dispatcher.]

**This is where the evidence for each stage comes from (§5.7).**

---

## 5. Staged migration order — 7 stages

Constraints: every stage ends with **all five projects building** (`src/HeadlessDCGO.Engine`,
`src/HeadlessDCGO.Rl`, `tools/RuleAudit`, `tools/RlBridgeHost`, `tools/RlVectorHost`). There is no solution file;
two commands cover the DAG:

```bash
dotnet build tools/RlVectorHost/RlVectorHost.csproj   # pulls Engine, Rl, RlBridgeHost
dotnet build tools/RuleAudit/RuleAudit.csproj         # pulls Engine, Rl
```

**There is no test suite** [실측: `git ls-files "*.csproj"` → 7 projects, zero test projects; no xunit/nunit/MSTest
package reference in any tracked file]. §5.7 defines what evidence replaces it.

**Ordering principle: cheapest lifetime first.** `Player` has 2 instances and no create/destroy events.
`CardSource` has N instances, all created at one point, never destroyed. `Permanent` has create, destroy,
identity-preserving mutation (digivolution), identity-breaking mutation (DNA), and a slot array. Do them in that
order so that each stage's lifetime machinery is available to the next.

---

### Stage 1 — `MatchEntityTable` substrate, unused

**Does:** adds `Headless/State/MatchEntityTable.cs` (§3.1) and its `EngineContext.ResetMatchState` hook. Nothing
reads it. Adds no members to `Player`/`CardSource`/`Permanent`.

**Call sites moved:** 0.

**Build risk:** nil — pure addition.

**Reversible.** Delete the file.

---

### Stage 2 — `Player` becomes canonical

**Does:**
1. Mint the two seats into `MatchEntityTable` at match init; add `Player.For(context, id)`.
2. Mechanically rewrite all **626** `new Player(` sites to `Player.For(` — a single-shape substitution
   (`new Player(X, Y)` → `Player.For(X, Y)`), 445 of them inside `Assets/Scripts/CardEffect/`.
3. Make the constructor `private`.
4. **Delete `Player.cs:49-57`** (`Equals`/`GetHashCode`/`==`/`!=`) in this same change (§3.5).
5. Move `PlayerEffectListStore` (`Player.cs:1022`), `PlayerSelectionQueueStore` (`:1039`),
   `_mainPhaseActionStore` (`:237`) and `PlayerTurnCounterController.DigivolveCountKey` contents onto plain
   fields. Delete the three stores.
6. Make `isYou`, `IsLose`, `WinCount`, `PlayerName`, `TurnStartTime`, `DigivolveCount_ThisTurn` real fields.

**Call sites moved:** ~626 mechanical + ~15 hand-written (the store accessors).

**Zone lists are NOT touched here** — they stay projections until Stage 6.

**⚠ Irreversible point:** step 4. Once the override is gone, reverting step 2 without reverting step 4 leaves
every `Player` comparison silently false.

---

### Stage 3 — `CardSource` becomes canonical

**Does:**
1. Mint one `CardSource` per card in the mirror `CardObjectController.CreateCardSource` (`:124`) and in token
   creation; register in `MatchEntityTable.Cards` and in the restored `GameContext.ActiveCardList` (§3.3).
2. Rewrite the **66** `new CardSource(` sites to `CardSource.For(context, id)`. Note the current ctor takes a
   `controller` *and* an `owner`; the AS-IS has only `Owner`, so each site must be inspected, not
   blind-substituted — this is **not** a pure mechanical pass. 53 of the 66 are outside `CardEffect/`.
3. Make the constructor `private`; **delete `CardSource.cs:56-64`** in the same change.
4. Move `CEntity_EffectControllerStore` (`CEntity_EffectController.cs:347`), `CEntityUseCycle` (`:449`),
   `PermanentJustBeforeRemoveFieldStore` (`CardSource.cs:2433`) and the `SecurityFaceState` face flag onto
   plain fields. Delete those stores and the `"baseDp"` / `"isToken"` / `"isBeingRevealed"` / `IsFlippedKey` /
   `WillBeRemoveSourcesKey` metadata.
5. Restore `Init()`'s three-statement body and all ten AS-IS call sites (TSV `r040`, `r044`, `r058` restored).
6. Delete the invented `Controller` member — after re-deriving every read of it from AS-IS.

**Call sites moved:** ~66 construction + ~40 store/metadata accessors + 10 `Init` sites.

**⚠ Irreversible points:** step 3 (same reason as Stage 2) and step 6 (deleting an invented member forces a
decision at each of its readers; those decisions are the real work and are not trivially undoable).

---

### Stage 4 — `CardSource.Owner` re-typed to `Player`

**Does:** flips `Owner` from `HeadlessPlayerId` to `Player` (matching `DCGO/…/CardSource.cs:25`), then collapses
every `new Player(card.Context, card.Owner).X` back to `card.Owner.X`, and reverts the ctor/field pairs
`(EngineContext, HeadlessPlayerId)` to the single `Player player` parameter. This is the 17 S7 rows plus their
fan-out.

**Split from Stage 2 deliberately:** it cannot happen before `CardSource` is canonical (Stage 3), and doing it
inside Stage 2 would double that stage's diff on the largest call-site population in the repo.

**Call sites moved:** the majority of the 445 `CardEffect/` `Player.For(` sites introduced in Stage 2 collapse
away here. Net: the repo *loses* call sites.

**Reversible in principle** (retype back), but expensive.

---

### Stage 5 — `Permanent` becomes a live object with a slot array

The largest and riskiest stage. Do not split it: the slot array, the lifetime ops and the identity change are one
atomic semantic unit — a half-migrated `Permanent` has no coherent meaning.

**Does:**
1. Restore `Player.FieldPermanents` as a real `Permanent?[]` and `Player.fieldCardFrames` as real state.
   Restore `FieldCardFrame.framePermanent` / `SetFramePermanent` / `GetFramePermanent` / `IsEmptyFrame`.
2. Restore `Permanent(List<CardSource>)` as the sole constructor, **including the order reversal** (§1.2), and
   `SetCardSources` / `AddCardSource` / `RemoveCardSource` / `AddDigivolutionCardsTop` with their AS-IS insert
   positions (`0`, `1`).
3. Restore `CreateNewPermanent` writing the slot, `RemoveField` nulling it (with the `:516` early return
   preserved), `MovePermanent` moving between slots.
4. Make digivolution `permanent.AddCardSource(card)` on the existing object (AS-IS `:1374`). Make DNA build a new
   one (AS-IS `:1496`).
5. Rewrite the **121** `new Permanent(` sites to the `List<CardSource>` ctor. The 17 in `CardEffect/` and the
   ~11 empty-dummy cost probes are the easy ones; the six snapshot sites are the dangerous ones.
6. **Delete `Permanent.cs:96-104`** in this same change, and individually reason about each of the six snapshot
   sites (§3.5) — the expected behavioural delta.
7. Retire `PermanentBookkeepingStore` entirely: the store, `PermanentContinuityKey`, `ReKey`, and the
   `InMemoryZoneMover.cs:482-484` suppression. The 11 bookkeeping fields become plain fields on `Permanent`.
8. Retire `PermanentFrame`'s compacted-list `FrameID` adaptation in favour of `Array.IndexOf(FieldPermanents, this)`.

**Call sites moved:** ~121 construction + ~8 continuity-stamp sites + ~30 bookkeeping accessors.

**⚠ Irreversible points:** step 6 and step 7. Step 7 in particular deletes the only existing mechanism for
permanent-identity continuity; if step 4 is wrong, there is no fallback.

---

### Stage 6 — `Player`'s zone lists become the state of record

**Does:** the seven `List<CardSource>` fields become authoritative; `IZoneStateReader.GetCards` becomes a
projection over them; `InMemoryZoneMover` retains the event stream and the shuffle/RNG seat but stops owning
`_zones` as truth. `RemoveFromAllArea` and the `Add*` family revert to the AS-IS `Contains`/`Remove`/`Insert`
shape (`CardObjectController.cs:370-1019`).

**Sequenced last among the substantive stages** because §3.6's three re-seating problems shrink to two once
Stage 5 has already removed the bookkeeping-reset responsibility from `MoveCard`. The remaining two —
`EnteredThisTurn` stamping and the `MatchEventLog` event stream — must both be explicitly re-seated here, not
left implicit.

**Call sites moved:** the 9 S3 rows plus every `zones.GetCards(...)` in the mirror (~30 [판단 — not counted
precisely]).

**⚠ Irreversible point:** the moment `_zones` stops being truth. Until then both models are live and can be
cross-checked (see §5.7 — this is the one stage that can carry a genuine differential gate).

---

### Stage 7 — sweep

**Does:** deletes what is now unreachable — `WithLinked` (`Permanent.cs:5042`), `ReadLinkMetadataInt` (`:5059`),
the ~20 residual `Permanent.cs`/`CardController.cs` metadata key constants (§2.3(e)),
`ZoneMoveMetadataKeys.EnteredThisTurnKey` if Stage 6 re-seated it onto `Permanent.EnterFieldTurnCount` as a real
`int`, and `Headless/State/MatchState.cs` + `StateFingerprintService` + `GameContextStateAccessor` +
`HeadlessEntityRegistry` if they are still callerless.

**Also:** applies §3.7's rule to `TurnStateMachine.endGame` and the six `IsSelecting` sites — one canonical
`TurnStateMachine` per match in `MatchEntityTable`. This is where defect (a) and defect (b) are actually fixed.

**⚠ Irreversible:** deletions.

---

### 5.7 Evidence, in the absence of a test suite

Each stage can offer, in increasing order of strength:

1. **Build across all five projects** — the only automatic gate. [게이트] It catches the type-flip stages
   (2, 3, 4) well and the semantic stages (5, 6) barely at all.
2. **Structural counters** — the progress metric this repo already uses: `new Player(` / `new CardSource(` /
   `new Permanent(` counts, live store count, live metadata-key count. A stage that does not move its counter to
   its target did not land. [게이트, trivially scriptable]
3. **`tools/RuleAudit`** — 20 games over 6 cross-set matchups with real ST1/ST2/ST3 decks, random-legal
   self-play, checking DCGO invariants against **live state** each step (`Program.cs:8-19`, sequential `foreach`
   `:31`). This is the closest thing to a behavioural test in the repo and it exercises exactly the surfaces this
   design perturbs. **It should be run before and after every stage** and its invariant failures diffed.
4. **Determinism fingerprints** — `HeadlessEpisodeFingerprint` / `HeadlessDeterminismVerifier` /
   `HeadlessBatchParallelDeterminismVerifier`. Same seed ⇒ same episode. A stage that is behaviour-preserving
   should leave fingerprints **byte-identical**; a stage that is not (5 and 6 certainly, 3 probably) will change
   them, and the value of the fingerprint is then that it tells you *exactly which step* diverged, which is
   strictly better than a pass/fail assertion.
5. **The fuzzing campaign** — `RlVectorHost` campaign mode (`--total-games` default 10 000, `Program.cs:48`),
   strict mode, crash/hang detection. Catches null-reference and lifetime regressions at volume. Note campaign
   mode is hardwired to `procs` (`Program.cs:397-402`), so it does not exercise in-process concurrency.
6. **Differential run, available only in Stage 6** — while both `_zones` and `Player`'s lists are live, assert
   they agree after every zone move. This is the strongest evidence any stage can offer and it exists only
   because Stage 6 has a natural dual-write window. **Take it.**

Fingerprint-identity (4) is the primary gate for Stages 1, 2 and 4 (which should be behaviour-neutral).
RuleAudit invariant-diff (3) plus the campaign (5) are the primary gates for Stages 3, 5 and 7.
The differential (6) is the primary gate for Stage 6.

---

## 6. What this does NOT fix

Stated plainly.

1. **It does not restore the presentation layer.** S1 — 73 of the 291 rows, the largest single root. `Effects`,
   `PlayLog`, `BrainStormObject`, `SecurityObject`, `MemoryObject`, `FieldPermanentCard`, `WaitForSeconds`.
   `Permanent.ShowingPermanentCard` (`DCGO/…/Permanent.cs:1644`) stays absent; `Player.brainStormObject`
   (`:231`) stays absent. Untouched by this design.
2. **It does not restore `GManager.instance` as a true singleton.** S2 — 45 rows. `AmbientMatchContext` remains
   an `AsyncLocal` scope that must be entered. The `new GameContext(_context).Players_ForTurnPlayer` roster
   derivations remain. §3.7 makes the *views* canonical, which fixes the discarded-write defects, but the
   AS-IS spelling `GManager.instance.turnStateMachine.gameContext.Players` still is not what the mirror writes.
3. **It does not make `ICardEffect` cross a zone move.** S5 — 24 rows. Effect causality stays a
   `HeadlessEntityId` cause id plus a batch id.
4. **It does not restore Unity/Photon types.** S11 — `Photon Hashtable`, `Mathf`, the AS-IS `List<T>.Clone()`
   extension. S15 — `CEntity_Base` as a `ScriptableObject`.
5. **It does not un-async the coroutines.** S12 — `IEnumerator` → `async Task` stays, `ref` parameters stay
   illegal, `SelectCardEffect.Activate()`'s synchronous choice stays a parked async request. The pump model
   (§4.1) depends on this and is not in question.
6. **It does not restore per-player memory.** S14 — memory stays a single signed gauge on `GameContext`
   (`GameContext.cs:27`), matching AS-IS's own `MemoryForPlayer` computed sign-flip (`Player.cs:973-985`), so
   this one is arguably already faithful; either way this design does not touch it.
7. **It does not give the engine snapshot, rollback, undo, or tree search.** It makes them *harder*, not easier
   (§4.2): a cyclic object graph replaces a flat id→record table. The two things that look latent —
   `MatchState`/`StateFingerprintService` and `ReplayActionRecord.Serialize/Deserialize` — are dead code, and
   neither captures the pump stack or the RNG's mid-stream position (`GameRandomSource._s0.._s3` have no getter).
8. **It does not fix the orphan metadata keys.** The earlier census found reads with no writer anywhere —
   `LinkedMaxKey`, `LinkDpKey`, `PendingDeletionMetadataKey`, `IsAceKey`, `OverflowMemoryKey`,
   `CannotBeDeDigivolvedKey`, `AdditionalCardTypesKey`, `OptionColorRequirementsKey`, the `"strike"` and
   `"hasSave"` literals, `CardObservation.FaceUpKey`. Each is a rule that silently defaults forever. **Deleting
   the key in Stage 7 removes the vestige but does not restore the rule.** Each is a separate defect and must be
   entered on the defect ledger rather than closed by deletion.
9. **It does not fix `Player.LostCards`.** AS-IS declares it (`Player.cs:514`) and only ever removes from and
   reads it — there is no `LostCards.Add`/`Insert` anywhere in DCGO. Restoring it as a live list faithfully
   reproduces a zone that is never populated. That is AS-IS-correct and is not this design's problem to solve.
10. **It does not resolve `GameContext.SetPlayerID`'s Photon gate.** `GameContext.cs:17-20` calls `SetPlayerID`
    only `if (PhotonNetwork.IsConnected)`. There is no headless equivalent, so the original's own non-networked
    behaviour (both `PlayerID`s stay `0`, `PlayerFromID(0)` returns `You` twice, `Players` becomes `[You, You]`)
    has no faithful reproduction. The mirror's seat-order source is an adaptation and stays one.

### 6.1 What genuinely cannot be reproduced

Per the brief, stated rather than designed around:

- **The field-frame layout is scene data, not source.** `Player.Start()` builds `fieldCardFrames` from
  `BattleAreaFrameParent.childCount` and `BreedingAreaFrameParent.childCount`
  (`DCGO/…/Player.cs:19-53`) and sizes `FieldPermanents = new Permanent[fieldCardFrames.Count]` (`:78`). The
  frame **count**, and the battle/breeding split that `FieldCardFrame.isBattleAreaFrameID` (`Player.cs:1557-1560`)
  encodes, exist only in `DCGO/Assets/Scenes/BattleScene.unity`. They cannot be derived from any `.cs` file.
  Stage 5 must read them off the scene asset and record them as named constants with the scene line cited — and
  must say so, because a hardcoded constant sourced from a YAML asset is not the same class of fidelity as a
  mirrored statement. **[미확정 — I did not extract the numbers; the scene is in-repo, so this is a lookup, not a
  gap.]**
- **`UnityEngine.Object` equality semantics** for `Player`/`CardSource` (§1.5): instance-ID comparison plus
  "destroyed object compares equal to null". The destroyed-object arm has no reproduction. It is also
  unreachable — nothing destroys either type during a match (§1.2, §1.3) — so plain reference equality is
  exactly equivalent *for this codebase*. Recorded because the equivalence is contingent, not definitional.
- **`Player` and `CardSource` as `MonoBehaviour`s** — scene load and `Instantiate`-from-prefab construction, the
  `CardSorcesParent` transform parenting, `PhotonView` (`CardSource.cs:19`). None of it is rule state. The
  `CardSource` prefab's pre-attached `CEntity_EffectController` component (`CardSource.cs:50`) is the only
  rule-relevant consequence and is reproducible as a plain field (Stage 3 step 4).

---

## 7. Open items the implementer must resolve, not inherit

1. **The six `Permanent` snapshot sites** (§1.6, §3.5). Each needs an individual determination *before* Stage 5
   step 6 lands. This is the top risk in the document.
2. **`CardSource.Controller`** (§2.1, Stage 3 step 6). Every reader needs an AS-IS source; readers with none are
   defects to log.
3. **Whether every lane of `HeadlessLegalActionDispatcher` reads mirror or substrate** (§4.5). Only three lanes
   were surveyed. This is the surface Stages 3–6 perturb most and it is not fully mapped.
4. **The frame count and battle/breeding split** from `BattleScene.unity` (§6.1).
5. **`EngineContext.CurrentPayCostRoot`** (`:224`) is not cleared by `ResetMatchState` (`:319-335`). Whether that
   is a live leak was not determined; Stage 1's reset hook is the natural place to settle it.
