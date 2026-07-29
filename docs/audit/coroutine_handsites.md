# Coroutine conversion — non-mechanical hand sites

Scope: `src/HeadlessDCGO.Engine/Assets/Scripts/` (whole subtree, `.cs` only), cross-referenced against the
original at `DCGO/Assets/Scripts/`. Facts only — no design, no ranking, no recommendation. No `.cs` file was
modified.

Mirror-tree comments were **not** used as evidence (several are known false); every original claim below comes
from reading `DCGO/Assets/Scripts/` with `--binary-files=text`.

### Provenance of the line numbers

Derived against the **working tree**, not `HEAD`. At the time of the scan the tree carried 309 uncommitted
modified files (`git diff --stat`: 9,260 insertions / 30,439 deletions), including all the files inventoried
below, plus new untracked coroutine infrastructure at `src/HeadlessDCGO.Engine/Headless/Coroutines/`
(`CoroutineDriver.cs`, `UnityEngineYieldInstructions.cs`). Two files (`ContinuousController.cs`,
`CoroutineDriver.cs`) were written while this scan was in progress.

The port-side line numbers are therefore valid **only** against that working-tree state. They will not match
`HEAD`, and they will drift if the in-flight changes are reverted or rebased. The scan was re-run after the
last observed write and reproduced identical figures — 1,864 total `await` statements, 62 consuming sites, 43
`Task<T>` declarations — so the inventory below is self-consistent, but re-derive line numbers before acting
on it if the tree has moved. Original-side (`DCGO/Assets/Scripts/`) line numbers are unaffected: that tree is
clean.

---

## Totals

| set | definition | my count | rough grep | delta |
|---|---|---|---|---|
| **A** | `Task<T>`-returning member declarations that the mirror **awaits** | **42** (of 43 declared) | 24 | **+18** |
| **B** | `await` sites whose value is **consumed** | **62** | 62 | **0 (same number, different membership)** |

### How A differs from the rough grep

The rough grep's 24 is exactly the count of `async Task<` declarations. My 42 differs on two axes:

- **+19 plain `Task<T>` declarations** (no `async` keyword) that are nonetheless awaited by callers, so they
  return a value across a coroutine boundary just like the async ones. These are expression-bodied
  chain-returns: `CardEffectCommons.cs:210` `TrashDigivolutionCardsFromTopOrBottom`, the 17 `PlayXToken`
  wrappers at `CardEffectCommons.cs:1187-1251`, and `Permanent.cs:5279` `TrashSourcesAsync`. A grep for
  `async Task<` cannot see them.
- **−1 never-awaited**: `Permanent.cs:5490` `private static async Task<int> RemoveSourcesAsync` — its **only**
  reference in the whole tree is `Permanent.cs:5290`, called **without** `await` (tail-chained from
  `TrashSourcesAsync`). It is declared but never awaited, so it is excluded from A. Listed below as A-43 for
  completeness.

So: 43 `Task<T>`-returning declarations exist; 42 are awaited; 24 of the 43 carry the `async` keyword.

### How B differs from the rough grep

Both land on 62, but the membership is not the same. A line-oriented regex (`= await`, `return await`,
`(await …)`) misses three forms that a statement-level scan catches, and admits one non-code hit:

- **compound assignment** `progressed |= await …` — 5 sites, all `AutoProcessing.cs` (:342, :345, :348, :363, :376).
- **ternary branch** `… ? 0 : await …` feeding a declaration — 1 site, `CardEffectCommons.cs:188`
  (statement starts at :181, `int trashed = …`).
- **assignment split across lines** (the `=` and the `await` on different lines) — 1 site,
  `SelectCardEffect.cs:457-458`.
- **false positive**: the doc-comment at `Permanent.cs:4706` contains the literal text
  `permanent = await permanent.AddCardSource(card)` and matches a naive line grep; it is a comment, not code.

`return await` occurs **zero** times in the tree.

### Structural note on what the originals actually do

Three distinct patterns account for every A row, and they are not interchangeable:

1. **Player selection queue** — the callee writes via a `[PunRPC]` method
   (`player.QueuePlayerSelection(new CardSelection/PermanentSelection/ValueSelection(...))`) and the caller
   reads after `yield return new WaitUntil(() => player.HasPlayerSelection())` then
   `player.DequeuePlayerSelection<T>()`. This is the genuine callee-writes / caller-reads seam.
2. **Shared UI panel field** — `SelectCardPanel.SelectedIndex` (`SelectCardPanel.cs:44`) is written inside
   `OpenSelectCardPanel` and read by the caller as `SelectedIndex[0]` right after the `yield`.
3. **No carrier at all** — the original method is a `void` `IEnumerator` (or plain synchronous `void`) and
   returns nothing; the mirror's `Task<T>` payload is a port-only invention with no original counterpart. This
   covers the majority of A rows (all the `bool`/`int` "did it happen / how many" returns).

---

## A — `Task<T>`-returning declarations that the mirror awaits

### Script/AutoProcessing.cs

The original `RuleProcess` (`DCGO/Assets/Scripts/Script/AutoProcessing.cs:282`) carries **no progress flag**.
Its loop is `while (DoRuleProcess())` (:284) where `DoRuleProcess()` (:319) is a pure board re-scan returning
`bool`, re-evaluated fresh each pass; every stage coroutine it invokes is a **void `IEnumerator`**. All the
mirror's `Task<bool>` "progressed" returns are therefore port-only.

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-01 | `AutoProcessing.cs:313` `public async Task<bool> RuleProcess(CancellationToken)` | `RuleProcess` | none — original `IEnumerator RuleProcess()` `AutoProcessing.cs:282` returns nothing | none | `bool` is port-only. Original loop re-scans via `DoRuleProcess()` (:319) instead of accumulating a flag. |
| A-02 | `AutoProcessing.cs:525` `private async Task<bool> TrashNonDigimonPermanentProcess(CancellationToken)` | `TrashNonDigimonPermanentProcess` | none — original `IEnumerator TrashNonDigimonPermanentProcess()` `AutoProcessing.cs:409` is void | none — caller `AutoProcessing.cs:294` `yield return …StartCoroutine(TrashNonDigimonPermanentProcess())` reads nothing | `bool` port-only. |
| A-03 | `AutoProcessing.cs:556` `private async Task<bool> TrashNoDPPermanentProcess(CancellationToken)` | `TrashNoDPPermanentProcess` | none — original `AutoProcessing.cs:439` is void `IEnumerator` | none — caller `AutoProcessing.cs:297` bare yield | `bool` port-only. |
| A-04 | `AutoProcessing.cs:591` `private async Task<bool> DigimonLackDPProcess(CancellationToken)` | `DigimonLackDPProcess` | none — original `AutoProcessing.cs:469` is void `IEnumerator` | none — caller `AutoProcessing.cs:300` bare yield | `bool` port-only. |
| A-05 | `AutoProcessing.cs:645` `private async Task<bool> DigimonLackLinkConditionProcess(CancellationToken)` | `DigimonLackLinkConditionProcess` | none — original `AutoProcessing.cs:502` is void `IEnumerator` | none — caller `AutoProcessing.cs:306` bare yield | `bool` port-only. |
| A-06 | `AutoProcessing.cs:675` `private async Task<(bool Progressed, bool Parked)> DigimonLackLinkMaxCountProcess(CancellationToken)` | `DigimonLackLinkMaxCountProcess` | none — original `AutoProcessing.cs:524` is void `IEnumerator` | none — caller `AutoProcessing.cs:309` bare yield | **Both** tuple members are port-only. `Parked` has no original analogue at all: the original simply suspends inside `SelectCardEffect.Activate()` and the coroutine resumes in place, so there is nothing to signal upward. |
| A-07 | `AutoProcessing.cs:703` `private async Task<bool> CardFaceDownProcess(CancellationToken)` | `CardFaceDownProcess` | none — original `AutoProcessing.cs:541` is void `IEnumerator` | none — caller `AutoProcessing.cs:312` bare yield | `bool` port-only. |

### Script/AttackProcess.cs

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-08 | `AttackProcess.cs:205` `public async Task<HeadlessAttackState> Attack(…)` | `Attack` | `AttackProcess.cs:24` property `public AttackState State { get; set; }`, written throughout the coroutine — `:108`, `:223`, `:242`, `:250`, `:260`, `:279`, `:303`, `:319`, `:327`, `:388`, `:404`, `:412`, `:453`, `:467`, `:483`, `:509`; sibling fields `AttackingPermanent`/`DefendingPermanent` at `:11`/`:12` | `TurnStateMachine.cs:943` and `AutoProcessing.cs:707` read `GManager.instance.attackProcess.State` off the singleton | Original `public IEnumerator Attack(…)` `AttackProcess.cs:73` returns nothing; outcome lives on the singleton's mutable properties. Mirror's returned `HeadlessAttackState` (a substrate record, `Headless/Runtime/HeadlessAttackState.cs:5`) is **never consumed** — both awaiters (`SelectAttackEffect.cs:376`, `TurnStateMachine.cs:777`) discard it. |

### Script/CardObjectController.cs

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-09 | `CardObjectController.cs:233` `public static async Task<Permanent> CreateNewPermanent(…)` | `CreateNewPermanent` | `DCGO/…/CardObjectController.cs:489` `permanent.TopCard.Owner.FieldPermanents[frameID] = permanent`; `:501` `permanent.ShowingPermanentCard = fieldPermanentCard` | `DCGO/…/CardController.cs:1383` and `:1496` — the caller built `permanent = new Permanent(new List{card})` **before** the call and keeps that reference | **Direction is inverted.** Original `IEnumerator CreateNewPermanent(Permanent permanent, int frameID)` (`:479`) is void and mutates a caller-supplied object; the mirror constructs inside and returns it, forcing callers to rebind. Awaited 6×. |

### Script/Permanent.cs

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-10 | `Permanent.cs:4708` `public async Task<Permanent> AddCardSource(CardSource, CancellationToken)` | `AddCardSource` | `DCGO/…/Permanent.cs:1047` `cardSources.Insert(0, cardSource)` | `DCGO/…/CardController.cs:1374` `permanent.AddCardSource(card);` as a plain void statement — `permanent` already bound at `:1371` | Original `public void AddCardSource` (`:1045`) is **synchronous void**, not even a coroutine. The mirror's `Task<Permanent>` + caller rebind is a port-only view-replacement. |
| A-11 | `Permanent.cs:4982` `internal static async Task<bool> RemoveLinkCardAsync(…)` | `RemoveLinkCardAsync` | `DCGO/…/Permanent.cs:1310-1311` `LinkedDP -= …; LinkedCards.Remove(cardSource)`, guarded by `if (LinkedCards.Contains(…))` at `:1308` | none — `DCGO/…/CardObjectController.cs:396`, `Permanent.cs:134`, `Permanent.cs:1256` all bare-yield `RemoveLinkedCard(…)` | Original `public IEnumerator RemoveLinkedCard` (`:1306`) is void. The mirror's `bool` surfaces the internal `Contains` guard, which the original never exposes. |
| A-12 | `Permanent.cs:5279` `internal static Task<int> TrashSourcesAsync(…)` (plain, expression-bodied) | `TrashSourcesAsync` | `DCGO/…/Permanent.cs:117-128` — `DiscardEvoRoots` trashes the whole local `evoRoots`/`linkRoots` lists; no count is kept | none — callers (`CardController.cs:2401`, `:2567`, …) bare-yield | Original `IEnumerator DiscardEvoRoots` (`Permanent.cs:106`) is void. `int` port-only. Body is `=> RemoveSourcesAsync(…)` (chain-return, not awaited internally); **is itself awaited** at `CardEffectCommons.cs:188`, so it qualifies for A. |
| A-13 | `Permanent.cs:5294` `internal static async Task<int> TrashSpecificSourcesAsync(…)` | `TrashSpecificSourcesAsync` | `DCGO/…/CardController.cs:5144` field `_trashTargetCards` (filtered `:5158`, `:5198`) | none — `DCGO/…/CardController.cs:2233` bare-yields `new ITrashDigivolutionCards(…).TrashDigivolutionCards()` | Original `ITrashDigivolutionCards.TrashDigivolutionCards()` (class at `:5127`) is void `IEnumerator`. Where AS-IS needs a count it derives it from `selectedCards.Count` (`CardEffectCommons/TrashDigivolutionCards.cs:166`), not from a return. |
| A-14 | `Permanent.cs:5378` `internal static async Task<bool> PlaySpecificSourceAsync(…)` | `PlaySpecificSourceAsync` | `DCGO/…/Permanent.cs:1301` `cardSources.Remove(cardSource)` | none | Original `public IEnumerator RemoveCardSource` (`:1297`) is void. `bool` port-only — and **not consumed in the mirror either**: all three awaiters (`Permanent.cs:4751`, `:4805`, `CardController.cs:1564`) are bare. |
| A-15 | `Permanent.cs:5425` `internal static async Task<bool> ArmorPurgeTopAsync(…)` | `ArmorPurgeTopAsync` | `DCGO/…/CardEffectCommons/KeyWordEffects/ArmorPurge.cs:52` `RemoveFromAllArea(topCard)` + `:56` `AddTrashCard(topCard)`, under guard `if (DigivolutionCards.Count >= 1)` `:46` | none — de-digivolve callers bare-yield the coroutine | Original `ArmorPurgeClass.ArmorPurge()` (`ArmorPurge.cs:40`) is void `IEnumerator`. Mirror's `bool promoted` is port-only but **is** consumed at 3 sites (B-13/B-14/B-16). |

### Script/CardEffectCommons.cs

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-16 | `CardEffectCommons.cs:387` `public static async Task<bool> PlaceDelayOptionCards(…)` | `PlaceDelayOptionCards` | none — original `PlaceDelayOptionCards` `DCGO/…/CardEffectCommons.cs:113` is `IEnumerator`, returns nothing | none | Awaited 6×, value never consumed. `bool` port-only. |
| A-17 | `CardEffectCommons.cs:1105` `public static async Task<IReadOnlyList<HeadlessEntityId>> PlayToken(…)` | `PlayToken` | none — original `PlayToken` `DCGO/…/CardEffectCommons.cs:140` is `IEnumerator`, returns no list | none | Awaited 17×, value never consumed. Returned id list port-only. |
| A-18 | `CardEffectCommons.cs:1416` `public static async Task<int> ActivateMainOfOptionSide(…)` | `ActivateMainOfOptionSide` | none — original `DCGO/…/CardEffectCommons.cs:733` is `IEnumerator` | none | Awaited 2× (`BT25_104.cs:185`, `CardEffectCommons.cs:3173`), value never consumed. `int` port-only. |
| A-19 | `CardEffectCommons.cs:210` `public static Task<int> TrashDigivolutionCardsFromTopOrBottom(…)` (plain) | `TrashDigivolutionCardsFromTopOrBottom` | none — original `DCGO/…/CardEffectCommons.cs:675` is `IEnumerator` | none | Awaited 11×, value never consumed. `int` port-only. |
| A-20 … A-36 | `CardEffectCommons.cs:1187, 1191, 1195, 1199, 1203, 1207, 1211, 1215, 1219, 1223, 1227, 1231, 1235, 1239, 1243, 1247, 1251` — 17 plain `Task<IReadOnlyList<HeadlessEntityId>> PlayXToken(…) => PlayToken(…)` wrappers | `PlayDiaboromonToken`, `PlayAmonToken`, `PlayUmonToken`, `PlayFujitsumonToken`, `PlayGyuukimonToken`, `PlayKoHagurumonToken`, `PlayFamiliarToken`, `PlaySelfDeleteFamiliarToken`, `PlayVoleeZerdrucken`, `PlayUkaNoMitama`, `PlayWarGrowlmonToken`, `PlayTaomonToken`, `PlayRapidmonToken`, `PlayPipeFox`, `PlayAthoRenePorToken`, `PlayHinukamuyToken`, `PlayPetrificationToken` | none | none | Each awaited by ≥1 card caller; **none** has its value consumed. Originals (e.g. `PlayDiaboromonToken` `DCGO/…/CardEffectCommons.cs:182`) are `IEnumerator` `StartCoroutine(PlayToken(…))` forwarders. Returned list port-only. Expression-bodied chain-returns — invisible to an `async Task<` grep. |

### Script/CardEffectCommons/RevealLibrary.cs

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-37 | `RevealLibrary.cs:506` `private static async Task<List<CardSource>> SelectCardsFromRevealPoolAsync(…)` | `SelectCardsFromRevealPoolAsync` | `SelectCardEffect.cs:150` field `_targetCards`, delivered via `_afterSelectCardCoroutine(_targetCards)` at `SelectCardEffect.cs:998-1000` | `DCGO/…/RevealLibrary.cs:331-337` `AfterSelectCardCoroutine(cardSources)` — adds to `chosenCards`, removes from `revealedCards` | **The one A row whose value is genuinely consumed** (at `RevealLibrary.cs:417` and `CardEffectCommons.cs:3255`). Port-only helper name — no single AS-IS method; it is the substrate for the AS-IS `selectCardEffect.Activate()` pass whose result lands in `_targetCards`. |

### Script/CardEffectCommons/KeyWordEffects/MindLink.cs

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-38 | `MindLink.cs:97` `public async Task<bool> MindLink(HeadlessEntityId, CancellationToken)` | `MindLinkClass.MindLink` | none — original `MindLink()` `DCGO/…/MindLink.cs:38` is `IEnumerator`, returns nothing; selection + placement happen internally via `SelectPermanentEffect` at `:52-79` | none | Awaited 1× (`EX11_070.cs:136`), value never consumed. `bool` is a port-only guard result. |

### Script/MultipleSkills.cs

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-39 | `MultipleSkills.cs:370` `async Task<int> ChooseOrderIndexAsync(List<SkillInfo>, Player, bool)` | `ChooseOrderIndexAsync` | `MultipleSkills.cs:435` `selectionPlayer.QueuePlayerSelection(new ValueSelection(skillIndex))` (`SetTargetSkill` RPC); landed into field `_skillIndex` at `MultipleSkills.cs:329` | `MultipleSkills.cs:326` `WaitUntil(player.HasPlayerSelection())` → `:328` `DequeuePlayerSelection<ValueSelection>()` → `:329` `_skillIndex = valueSelection.ValueAsInt()` (field declared `:63`) | **Textbook queue seam.** Original keeps the pick in the class field `_skillIndex`, later used at `:342` and `:353`. |

### Script/SelectHandEffect.cs · SelectCardEffect.cs · SelectPermanentEffect.cs

Three same-named but distinct declarations, one per file.

| id | port file:line | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|
| A-40 | `SelectHandEffect.cs:396` `private async Task<(List<CardSource> Selected, bool NoSelect)> RunAsIsSelectionAsync(EngineContext, List<CardSource>)` | `RunAsIsSelectionAsync` | `SelectHandEffect.cs:938` `SetTargetHandCards` → `selectionPlayer.QueuePlayerSelection(new CardSelection(CardIDs))`, fed by `EndSelect_RPC` `:353-360` / `NoSelect_RPC` `:446-453` (`null` = no-select) | `SelectHandEffect.cs:597-598` `WaitUntil(_selectPlayer.HasPlayerSelection())` → `DequeuePlayerSelection<CardSelection>()` → `.CardIDList` `:604-610`; `_noSelect = cardSeletion.CardIDList == null` `:604` | Port-only wrapper (no original helper of this name); the original does the selection inline in `Activate()`. Both tuple members ride one `CardSelection` — `NoSelect` is encoded as a `null` list, not a second field. |
| A-41 | `SelectCardEffect.cs:705` `private async Task<(List<CardSource> Selected, List<int> SelectedIndices)> RunAsIsSelectionAsync(…)` | `RunAsIsSelectionAsync` | `SelectCardEffect.cs:1023-1024` `SetTargetCardAndIndicies` → **two** queue pushes: `QueuePlayerSelection(new CardSelection(CardIDs))` then `QueuePlayerSelection(new CardSelection(Indicies))` | `SelectCardEffect.cs:660-662` `DequeuePlayerSelection<CardSelection>()` → `.CardIDList` → `_targetCards` `:668-671`; then a **second** `WaitUntil` + dequeue `:674-676` → `.CardIDList` → `_slectedInexesInList` `:682-684` | Port-only wrapper. The tuple's two members map 1:1 onto two **sequential** dequeues — ordering is load-bearing. Upstream UI source is `SelectCardPanel.SelectedList`/`.SelectedIndex` (`:561`/`:566`). |
| A-42 | `SelectPermanentEffect.cs:580` `private async Task<(List<Permanent> Selected, bool NoSelect)> RunAsIsSelectionAsync(EngineContext, List<Permanent>)` | `RunAsIsSelectionAsync` | `SelectPermanentEffect.cs:1052` (`[PunRPC]` at `:1042`) → `selectionPlayer.QueuePlayerSelection(new PermanentSelection(isTurnPlayer, UnitIndex))` | `SelectPermanentEffect.cs:688-689` `WaitUntil(…HasPlayerSelection())` → `DequeuePlayerSelection<PermanentSelection>()` → `.IsTurnPlayerList`/`.PermanentIDList` `:695+` | Port-only wrapper; original selects inline in `Activate()`. |

### Declared but never awaited (excluded from A)

| id | port file:line | member | notes |
|---|---|---|---|
| A-43 | `Permanent.cs:5490` `private static async Task<int> RemoveSourcesAsync(…)` | `RemoveSourcesAsync` | **Not awaited anywhere in the tree.** Sole reference is `Permanent.cs:5290`, a tail chain-return from `TrashSourcesAsync` with no `await`. Backing logic corresponds to `DiscardEvoRoots` (`DCGO/…/Permanent.cs:106`, void `IEnumerator`). |

---

## B — `await` sites whose value is consumed

### Script/AutoProcessing.cs (6)

All six feed the port-only `progressed` accumulator. The original has no such variable — it re-scans with
`while (DoRuleProcess())` (`DCGO/…/AutoProcessing.cs:284`, predicate at `:319`).

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-01 | `AutoProcessing.cs:342` | `progressed \|= await TrashNonDigimonPermanentProcess(cancellationToken)…` | `TrashNonDigimonPermanentProcess` (A-02) | none — `DCGO/…/AutoProcessing.cs:409` void `IEnumerator` | none — `DCGO/…/AutoProcessing.cs:294` bare yield | Compound-assignment consumption; missed by line-regex. |
| B-02 | `AutoProcessing.cs:345` | `progressed \|= await TrashNoDPPermanentProcess(cancellationToken)…` | `TrashNoDPPermanentProcess` (A-03) | none — `:439` void | none — `:297` bare yield | as above |
| B-03 | `AutoProcessing.cs:348` | `progressed \|= await DigimonLackDPProcess(cancellationToken)…` | `DigimonLackDPProcess` (A-04) | none — `:469` void | none — `:300` bare yield | as above |
| B-04 | `AutoProcessing.cs:363` | `progressed \|= await DigimonLackLinkConditionProcess(cancellationToken)…` | `DigimonLackLinkConditionProcess` (A-05) | none — `:502` void | none — `:306` bare yield | as above |
| B-05 | `AutoProcessing.cs:366` | `(bool linkMaxProgressed, bool linkMaxParked) = await DigimonLackLinkMaxCountProcess(cancellationToken)…` | `DigimonLackLinkMaxCountProcess` (A-06) | none — `:524` void | none — `:309` bare yield | **Tuple destructure.** Neither member has an original field. `linkMaxParked` drives an early `return true` at mirror `:371-374`; the original just suspends in place inside `SelectCardEffect.Activate()`. |
| B-06 | `AutoProcessing.cs:376` | `progressed \|= await CardFaceDownProcess(cancellationToken)…` | `CardFaceDownProcess` (A-07) | none — `:541` void | none — `:312` bare yield | as above |

### Script/CardController.cs (10)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-07 | `CardController.cs:274` | `IReadOnlyList<HeadlessEntityId> drawnCards = await _context.ZoneMover.DrawAsync(…)` | `IZoneMover.DrawAsync` — substrate, declared `Headless/Services/IZoneMover.cs`, no DCGO counterpart | `DCGO/…/CardController.cs:1921` local `List<CardSource> DrawCards = new()`, filled `:1931` `DrawCards.Add(DrawCard)` | `DCGO/…/CardController.cs:1939`, `:1948` `if (DrawCards.Count >= 1)` — **same coroutine**, method-local | Original `DrawClass.Draw()` `:1916` is one `IEnumerator`; the drawn list never crosses a coroutine boundary, so there is no field to restore — it is a local. |
| B-08 | `CardController.cs:448` | `IReadOnlyList<HeadlessEntityId> added = await _context.ZoneMover.AddSecurityFromLibraryAsync(…)` | `IZoneMover.AddSecurityFromLibraryAsync` — substrate, no DCGO counterpart | `DCGO/…/CardController.cs:2054` local `int count`, `++` at `:2064`; per-card `AddSecurityCard(StockCard, …)` runs **inside** the loop `:2062` | `DCGO/…/CardController.cs:2068` `if (count > 0)` — same coroutine | Original `IAddSecurityFromLibrary.AddSecurity()` `:2052` is void and never collects an id list. The mirror splits into substrate-returns-ids then a per-card loop `:453-463`; AS-IS keeps the per-card work inside the original loop. |
| B-09 | `CardController.cs:954` | `bool promoted = await Permanent.ArmorPurgeTopAsync(…)` | `ArmorPurgeTopAsync` (A-15) | `DCGO/…/CardEffectCommons/KeyWordEffects/ArmorPurge.cs:52`, `:56` | none in original | Original `ArmorPurgeClass.ArmorPurge()` `ArmorPurge.cs:40` is void; `promoted` is port-only. |
| B-10 | `CardController.cs:1090` | `bool promoted = await Permanent.ArmorPurgeTopAsync(…)` | `ArmorPurgeTopAsync` (A-15) | as B-09 | none | as B-09 |
| B-11 | `CardController.cs:1264` | `_ = await Permanent.TrashSpecificSourcesAsync(…)` | `TrashSpecificSourcesAsync` (A-13) | `DCGO/…/CardController.cs:5144` `_trashTargetCards` | none | **Discard assignment** — the value is thrown away even in the mirror, so this site is mechanically convertible despite matching the consumption pattern. |
| B-12 | `CardController.cs:2792` | `bool promoted = await Permanent.ArmorPurgeTopAsync(…)` | `ArmorPurgeTopAsync` (A-15) | as B-09 | none | as B-09 |
| B-13 | `CardController.cs:4009` | `permanent = await permanent.AddCardSource(card, cancellationToken)…` | `AddCardSource` (A-10) | `DCGO/…/Permanent.cs:1047` `cardSources.Insert(0, cardSource)` | `DCGO/…/CardController.cs:1374` — plain void call; `permanent` bound at `:1371`, reference unchanged | Port-only **view rebind**: original method is synchronous `void` (`Permanent.cs:1045`), the caller's reference is simply still valid. |
| B-14 | `CardController.cs:4017` | `permanent = await CardObjectController.CreateNewPermanent(card, isSuspended: _isTapped, isBreedingArea: _isBreedingArea, cancellationToken)…` | `CreateNewPermanent` (A-09) | `DCGO/…/CardObjectController.cs:489` `FieldPermanents[frameID] = permanent` | `DCGO/…/CardController.cs:1383` — caller's own pre-built `permanent` local | Mirror constructs-and-returns; original caller-constructs-and-holds. |
| B-15 | `CardController.cs:4098` | `permanent = await CardObjectController.CreateNewPermanent(card, isSuspended: false, cancellationToken: cancellationToken)…` | `CreateNewPermanent` (A-09) | `DCGO/…/CardObjectController.cs:489` | `DCGO/…/CardController.cs:1496` — caller's pre-built `permanent` local | as B-14 (the `:1496` construction site) |
| B-16 | `CardController.cs:4486` | `Headless.Choices.ChoiceResult picked = await card.Context.ChoiceProvider.ChooseAsync(request, cancellationToken)…` (spans `:4486-4487`) | `IChoiceProvider.ChooseAsync` — substrate | `DCGO/…/SelectCardPanel.cs:44` field `public List<int> SelectedIndex`, populated inside the `OpenSelectCardPanel` coroutine at `SelectCardPanel.cs:474`, `:482`, `:484` | `DCGO/…/CardController.cs:1869-1871`, after `yield return StartCoroutine(selectCardPanel.OpenSelectCardPanel(…))` at `:1856`: `if (SelectedIndex.Count > 0) skillIndex = SelectedIndex[0];` | **Genuine callee-writes-field / caller-reads-field move.** Mirror `picked.SelectedIds[0]` → `skillIndex` (`:4489-4500`) mirrors original `SelectedIndex[0]` → `skillIndex`. |

### Script/CardEffectCommons.cs (10)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-17 | `CardEffectCommons.cs:188` (statement starts `:181` `int trashed = …`) | `… ? 0 : await Permanent.TrashSourcesAsync(…)` | `TrashSourcesAsync` (A-12) | `DCGO/…/Permanent.cs:117-128` — `DiscardEvoRoots` trashes the whole `evoRoots`/`linkRoots` lists, keeps no count | none — callers bare-yield | **Ternary-branch consumption**, missed by line-regex. Original `IEnumerator DiscardEvoRoots` `Permanent.cs:106` is void; the `int` is port-only. `trashed` gates `successProcess`/`failureProcess` at `:195-205`. |
| B-18 | `CardEffectCommons.cs:254` | `if (await Permanent.RemoveLinkCardAsync(…))` | `RemoveLinkCardAsync` (A-11) | `ITrashLinkCards` trashed-tracking, written by `trashLinkCards.TrashLinkCards()` `DCGO/…/CardEffectCommons.cs:571` | `DCGO/…/CardEffectCommons.cs:573` `targetLinkCards.Some(sc => trashLinkCards.IsTrashed(sc))`; `:577` `trashLinkCards.TrashedLinkCards` | Original `TrashLinkCardsAndProcessAccordingToResult` `:567`: the per-link `bool` corresponds to reading the `ITrashLinkCards` **object's** `IsTrashed()`/`TrashedLinkCards` after the coroutine. |
| B-19 | `CardEffectCommons.cs:845` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken)…` ("Select 1 card to digivolve.") | `ChooseAsync` — substrate | local `selectedCards` via nested `SelectCardCoroutine(cardSource){ selectedCards.Add(cardSource); }` `DCGO/…/CardEffectCommons.cs:970-975`, fed by `SelectHandEffect`/`SelectCardEffect.Activate()` `:1004`/`:1036` | `DCGO/…/CardEffectCommons.cs:1046` (`cardSources: selectedCards`), `:1082-1084` (`selectedCards[0]`) | Original `DigivolveIntoHandOrTrashCard` `:756`. Value lands in a local list via the per-card callback, not a field. |
| B-20 | `CardEffectCommons.cs:1026` | `ChoiceResult hostResult = await …ChooseAsync(hostRequest, cancellationToken)…` ("Select 1 {selectString} that will trash digivolution cards.") | `ChooseAsync` — substrate | `SelectPermanentEffect.cs:142` `_targetPermanents` → `selectedPermanent = permanent` in `SelectPermanentCoroutine` `DCGO/…/CardEffectCommons/TrashDigivolutionCards.cs:111-113` | `TrashDigivolutionCards.cs:113-119` (uses `selectedPermanent.DigivolutionCards…`) | AS-IS `SelectTrashDigivolutionCards` lives in `DCGO/…/CardEffectCommons/TrashDigivolutionCards.cs:11`; the mirror folded it into `CardEffectCommons.cs`. Pick via `selectPermanentEffect.Activate()` `:109`. |
| B-21 | `CardEffectCommons.cs:1047` | `ChoiceResult sourceResult = await …ChooseAsync(sourceRequest, cancellationToken)…` ("Select digivolution cards to trash.") | `ChooseAsync` — substrate | `SelectCardEffect.cs:150` `_targetCards` → local `selectedCards` via `SelectCardCoroutine` `TrashDigivolutionCards.cs:154-156`, fed by `selectCardEffect.Activate()` `:147` | `TrashDigivolutionCards.cs:161-164` (`new ITrashDigivolutionCards(selectedPermanent, selectedCards, …)`), `:166` | Same original method as B-20. |
| B-22 | `CardEffectCommons.cs:1049` | `int trashed = await Permanent.TrashSpecificSourcesAsync(…)` | `TrashSpecificSourcesAsync` (A-13) | `ITrashDigivolutionCards.TrashDigivolutionCards()` `TrashDigivolutionCards.cs:161-164` | `TrashDigivolutionCards.cs:166` `digivolutionDiscardedCount = selectedCards.Count` | AS-IS derives the count from `selectedCards.Count`, **not** from the trash coroutine (which is void `IEnumerator`). |
| B-23 | `CardEffectCommons.cs:1334` | `ChoiceResult result = await …ChooseAsync(request, cancellationToken)…` ("Discard {max} card(s).") | `ChooseAsync` — substrate | `SelectHandEffect.cs:137` `_targetCards`, fed by `selectHandEffect.Activate()` Mode.Discard `DCGO/…/CardEffectCommons.cs:1444` | `SelectHandEffect.cs:717`, `:729` (`discardHands = _targetCards.Map(...)`); optional `afterSelectCardCoroutine(_targetCards)` | Original `DrawAndDiscardCards` `:1408`; discard happens inside `SelectHandEffect` Mode.Discard. |
| B-24 | `CardEffectCommons.cs:1369` | `ChoiceResult result = await …ChooseAsync(request, cancellationToken)…` ("Specify the order to place the cards at the bottom of the deck.") | `ChooseAsync` — substrate | `SelectCardEffect.cs:150` `_targetCards` → `AfterSelectCardCoroutine1(cardSources)` param `DCGO/…/RevealLibrary.cs:514`, fed by `selectCardEffect.Activate()` `:512` | `DCGO/…/RevealLibrary.cs:516` `AddLibraryBottomCards(cardSources)` | Original `ReturnRevealedCardsToLibraryBottom` `RevealLibrary.cs:469` ordering pick (2+ cards). |
| B-25 | `CardEffectCommons.cs:3053` | `int trashed = await Permanent.TrashSpecificSourcesAsync(…)` | `TrashSpecificSourcesAsync` (A-13) | `ITrashDigivolutionCards.TrashDigivolutionCards()` `DCGO/…/CardEffectCommons.cs:543-545` | `DCGO/…/CardEffectCommons.cs:547` `IsTrashed(sc)`, `:551` `trashDigivolutionCards.TrashedCards` | Original `TrashDigivolutionCardsAndProcessAccordingToResult` `:541` reads the `ITrashDigivolutionCards` object's fields after the coroutine. |
| B-26 | `CardEffectCommons.cs:3255` | `List<CardSource> selected = await SelectCardsFromRevealPoolAsync(…)` | `SelectCardsFromRevealPoolAsync` (A-37) | `SelectHandEffect.cs:137` `_targetCards`, fed by `selectHandEffect.Activate()` Mode.Discard `DCGO/…/CardEffectCommons.cs:1444` | `SelectHandEffect.cs:717`, `:729`; `afterSelectCardCoroutine(_targetCards)` | Advanced overload of original `DrawAndDiscardCards` `:1408`; same `_targetCards` field as B-23. |

### Script/CardEffectCommons/RevealLibrary.cs (6)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-27 | `RevealLibrary.cs:386` | `ChoiceResult optResult = await context.ChoiceProvider.ChooseAsync(optOut)…` ("Will you select cards?") | `ChooseAsync` — substrate | `UserSelectionManager.cs:15` `SelectedBoolValue`, set via `SetBoolSelection` + `WaitForEndSelect` `DCGO/…/RevealLibrary.cs:277-279` | `DCGO/…/RevealLibrary.cs:281` `doAction = …userSelectionManager.SelectedBoolValue` | Original `RevealDeckTopCardsAndSelect` `RevealLibrary.cs:229` opt-out branch (≥2 conditions + `canNoAction`). |
| B-28 | `RevealLibrary.cs:417` | `List<CardSource> selected = await SelectCardsFromRevealPoolAsync(…)` | `SelectCardsFromRevealPoolAsync` (A-37) | `SelectCardEffect.cs:150` `_targetCards`, fed by `selectCardEffect.Activate()` `DCGO/…/RevealLibrary.cs:328` | `DCGO/…/RevealLibrary.cs:331-337` `AfterSelectCardCoroutine(cardSources)`; per-card `selectCardCoroutine` `:315` | Per-condition select pass of the original `RevealDeckTopCardsAndSelect`. |
| B-29 | `RevealLibrary.cs:546` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectCardEffect.cs:150` `_targetCards` | delivered via `_afterSelectCardCoroutine(_targetCards)` `SelectCardEffect.cs:998-1000`; caller reads per B-28 (`RevealLibrary.cs:331-337`) | Helper-internal batch path of A-37; same AS-IS field as B-28. |
| B-30 | `RevealLibrary.cs:584` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectCardEffect.cs:150` `_targetCards` | as B-29 | Helper-internal **incremental one-pick loop** of A-37, reproducing the AS-IS `SelectCardPanel` per-pick re-filter (`byPreSelectedList`); same field. |
| B-31 | `RevealLibrary.cs:680` | `ChoiceResult placeResult = await context.ChoiceProvider.ChooseAsync(placeRequest)…` ("To which area do you place cards?") | `ChooseAsync` — substrate | `UserSelectionManager.cs:15` `SelectedBoolValue`, set via `SetBoolSelection` + `WaitForEndSelect` `DCGO/…/RevealLibrary.cs:636-638` | `DCGO/…/RevealLibrary.cs:640` `bool toTop = …userSelectionManager.SelectedBoolValue` | Original `ReturnRevealedCardsToLibraryTopOrBottom` `RevealLibrary.cs:617` top/bottom binary pick. |
| B-32 | `RevealLibrary.cs:718` | `ChoiceResult orderResult = await context.ChoiceProvider.ChooseAsync(orderRequest)…` ("Specify the order to place the card at the top of the deck…") | `ChooseAsync` — substrate | `SelectCardEffect.cs:150` `_targetCards` → `AfterSelectCardCoroutine1(cardSources)` param `DCGO/…/RevealLibrary.cs:571`, fed by `selectCardEffect.Activate()` `:569` | `DCGO/…/RevealLibrary.cs:573-577` `topCards = cardSources.Clone(); topCards.Reverse(); AddLibraryTopCards(topCards)` | Original `ReturnRevealedCardsToLibraryTop` `RevealLibrary.cs:526` ordering pick (2+ cards). |

### Script/CardEffectCommons/DNADigivolveEffects.cs (2)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-33 | `DNADigivolveEffects.cs:627` | `playedPermanent = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);` | `CreateNewPermanent` (A-09) | `DCGO/…/DNADigivolveEffects.cs:394` `playedPermanent = PlayTempPermanent(selectedCardSource, true)` — **synchronous** `return` at `:35` (local declared `:272`); then void coroutine `:395` `CreateNewPermanent(playedPermanent, frameID)` | `DCGO/…/DNADigivolveEffects.cs:420-422` (`orderedRoots`, `JogressEvoRootsFrameIDs`) | No coroutine value hand-off in the original — the value is a plain synchronous return. `CreateNewPermanent(Permanent,int)` `CardObjectController.cs:479` is void. |
| B-34 | `DNADigivolveEffects.cs:645` | `playedPermanent = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);` | `CreateNewPermanent` (A-09) | `DCGO/…/DNADigivolveEffects.cs:409` `playedPermanent = PlayTempPermanent(selectedCardSource, true)` (sync return `:35`); void coroutine `:412` | `DCGO/…/DNADigivolveEffects.cs:414` (`SelectPermanent(…, playedPermanent, …)`), `:420-422` | Else-branch of B-33: materialize then pick field root. |

### Script/CardEffectCommons/KeyWordEffects/Raid.cs (1)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-35 | `Raid.cs:114` | `Headless.Choices.ChoiceResult result = await card.Context.ChoiceProvider.ChooseAsync(request, cancellationToken)…` | `ChooseAsync` — substrate | `SelectPermanentEffect.cs:1052` `selectionPlayer.QueuePlayerSelection(new PermanentSelection(isTurnPlayer, UnitIndex))` (`SetTargetFrames` RPC), driven by `SelectPermanentEffect.Activate()` (original `Raid.cs:71`) | `SelectPermanentEffect.cs:688` `WaitUntil(_selectPlayer.HasPlayerSelection())` → `:689` `DequeuePlayerSelection<PermanentSelection>()` → resolved into `_targetPermanents` `:694-714`, delivered to the `SelectPermanentCoroutine` callback (original `Raid.cs:73`→`:75` `SwitchDefender`) | Original consumes the value **in a callback**, not via a local read-back. |

### Script/CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs (1)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-36 | `BlastDNADigivolution.cs:258` | `Permanent materialized = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);` | `CreateNewPermanent` (A-09) | `DCGO/…/BlastDNADigivolution.cs:209` `playedPermanent = new Permanent(new List<CardSource>(){selectedCardSource}){IsSuspended=false}` — **synchronous** local (declared `:197`); then void coroutine `:211` | Original does **not** read `playedPermanent`; it re-derives via `selectedCardSource.PermanentOfThisCard().PermanentFrame.FrameID` at `:218` / `:222` | Mirror's return replaces AS-IS sync `new Permanent` + `PermanentOfThisCard()` re-query. |

### Script/MultipleSkills.cs (2)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-37 | `MultipleSkills.cs:274` | `_skillIndex = await ChooseOrderIndexAsync(skillInfos_active, player, canDecline);` | `ChooseOrderIndexAsync` (A-39) | `MultipleSkills.cs:435` `QueuePlayerSelection(new ValueSelection(skillIndex))` (`SetTargetSkill` RPC) | `MultipleSkills.cs:326` `WaitUntil` → `:328` `DequeuePlayerSelection<ValueSelection>()` → `:329` writes field `_skillIndex` (declared `:63`) | The mirror's return value **is** the original's field `_skillIndex` — same name, same role. |
| B-38 | `MultipleSkills.cs:401` | `ChoiceResult result = await _context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `MultipleSkills.cs:435` `QueuePlayerSelection(new ValueSelection(skillIndex))`; `skillIndex` sourced from the `selectHandEffect`/`selectCardPanel` branches `:250`/`:307` | `MultipleSkills.cs:326` → `:328` → `:329` | The order-pick decision; the substrate call collapses the AS-IS panel + RPC + queue round-trip. |

### Script/OptionalSkill.cs (1)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-39 | `OptionalSkill.cs:82` | `ChoiceResult decision = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `OptionalSkill.cs:144` `selectionPlayer.QueuePlayerSelection(new ValueSelection(useOptional))` (`SetUseOptional` RPC) | `OptionalSkill.cs:117` `WaitUntil(player.HasPlayerSelection())` → `:118` `DequeuePlayerSelection<ValueSelection>()` → `:119` `_useOptional = valueSelection.ValueAsBool()` (field) | Yes/No optional-use decision. |

### Script/Select*.cs (11)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-40 | `SelectAppFusionEffect.cs:99` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` (ModeChoice: "Normal Digivolution" / "App Fusion") | `ChooseAsync` — substrate | `SelectCardPanel.cs:474` `SelectedIndex.Add(_handCards.IndexOf(handCard))`, populated by the `OpenSelectCardPanel` coroutine | `SelectAppFusionEffect.cs:76-78`, after `yield return StartCoroutine(…OpenSelectCardPanel(…))`: `if (…SelectedIndex.Count > 0) { int index = …SelectedIndex[0]; }` | Two-option digivolution-method panel; choice read from the shared `SelectedIndex` field. |
| B-41 | `SelectAttackEffect.cs:307` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` (defender permanent / attack player / not-attack) | `ChooseAsync` — substrate | `SelectAttackEffect.cs:566` `SetAttackTarget` (`[PunRPC]` `:553`) → `QueuePlayerSelection(new PermanentSelection(isTurnPlayerList, permanentIndexList))`, fed by `EndSelect_RPC` `:397` / "Not Attack" `:357` with `NopIndex` | `SelectAttackEffect.cs:465-482` `WaitUntil(…HasPlayerSelection())` → `:471` `DequeuePlayerSelection<PermanentSelection>()` → `.IsTurnPlayerList[0]`/`.PermanentIDList[0]` `:478-479`; `_noSelect = targetIndex == NopIndex` `:482`; `_defender` resolved `:504` | Original `Activate()` `IEnumerator` at `:202`. No-select is encoded as a sentinel index, not a separate flag. |
| B-42 | `SelectBurstDigivolutionEffect.cs:103` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` (ModeChoice: "Normal" / "Burst Digivollution") | `ChooseAsync` — substrate | `SelectCardPanel.cs:474` `SelectedIndex.Add(…)` via `OpenSelectCardPanel` | `SelectBurstDigivolutionEffect.cs:74-76` `if (…SelectedIndex.Count > 0) { int index = …SelectedIndex[0]; }`, after `yield return StartCoroutine(…)` `:58` | Same two-option-panel pattern as B-40. |
| B-43 | `SelectCardEffect.cs:458` (statement `:457-458`) | `(List<CardSource> selected, List<int> selectedIndices) =`<br>`    await RunAsIsSelectionAsync(context, rootCards)…` | `RunAsIsSelectionAsync` (A-41) | `SelectCardEffect.cs:1023-1024` two queue pushes (`CardSelection(CardIDs)`, then `CardSelection(Indicies)`) | `SelectCardEffect.cs:660-662` first dequeue → `_targetCards` `:668-671`; second `WaitUntil` + dequeue `:674-676` → `_slectedInexesInList` `:682-684` | **Assignment split across two lines** — missed by line-regex. Results assigned to `_targetCards`/`_slectedInexesInList` at mirror `:459-460`, exactly the original's two fields. |
| B-44 | `SelectCardEffect.cs:741` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectCardEffect.cs:1023-1024` | `SelectCardEffect.cs:660-676` | Batch path inside A-41's helper. |
| B-45 | `SelectCardEffect.cs:780` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectCardEffect.cs:1023-1024` | `SelectCardEffect.cs:660-676` | `byPreSelectedList` incremental path inside A-41's helper. |
| B-46 | `SelectCountEffect.cs:280` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectCountEffect.cs:198` `SetCount` (`[PunRPC]` `:188`) → `selectionPlayer.QueuePlayerSelection(new ValueSelection(selectedCount))` | `SelectCountEffect.cs:164-166` `WaitUntil(…HasPlayerSelection())` → `DequeuePlayerSelection<ValueSelection>()` → `.ValueAsInt()` | `result.SelectedCount` ↔ original `ValueSelection.ValueAsInt()`. |
| B-47 | `SelectDigiXrosClass.cs:568` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` (ModeChoice: area Hand/Field/Trash/…) | `ChooseAsync` — substrate | `SelectDigiXrosClass.cs:1029` (`[PunRPC]` `:1019`) → `selectionPlayer.QueuePlayerSelection(new ValueSelection(targetIndex))` | `SelectDigiXrosClass.cs:545-548` `WaitUntil(…card.Owner.HasPlayerSelection())` → `DequeuePlayerSelection<ValueSelection>()` → `_targetIndex = seletion != null ? seletion.ValueAsInt() : 0;` | Inside `Select()` `IEnumerator` `:368`. Index selects `actions[_targetIndex]`. |
| B-48 | `SelectHandEffect.cs:206` | `(List<CardSource> selected, bool noSelect) = await RunAsIsSelectionAsync(context, handPool)…` | `RunAsIsSelectionAsync` (A-40) | `SelectHandEffect.cs:938` `QueuePlayerSelection(new CardSelection(CardIDs))` | `SelectHandEffect.cs:597-610` `DequeuePlayerSelection<CardSelection>()` → `_targetCards` / `_noSelect` | In the original this consumption **is** the inline `Activate()` queue read. |
| B-49 | `SelectHandEffect.cs:428` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectHandEffect.cs:938` | `SelectHandEffect.cs:597-610` | Batch path inside A-40's helper. |
| B-50 | `SelectHandEffect.cs:469` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectHandEffect.cs:938` | `SelectHandEffect.cs:597-610` | `byPreSelectedList` incremental path inside A-40's helper. |
| B-51 | `SelectJogressEffect.cs:156` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` (ModeChoice: "Normal" / "DNA Digivollution") | `ChooseAsync` — substrate | `SelectCardPanel.cs:474` `SelectedIndex.Add(…)` via `OpenSelectCardPanel` | `SelectJogressEffect.cs:107-109` `if (…SelectedIndex.Count > 0) { int index = …SelectedIndex[0]; }`, after `yield return StartCoroutine(…)` `:91` | Same two-option-panel pattern as B-40 / B-42. |
| B-52 | `SelectPermanentEffect.cs:403` | `(List<Permanent> selected, bool noSelect) = await RunAsIsSelectionAsync(context, pool)…` | `RunAsIsSelectionAsync` (A-42) | `SelectPermanentEffect.cs:1052` `QueuePlayerSelection(new PermanentSelection(…))` | `SelectPermanentEffect.cs:688-695` `DequeuePlayerSelection<PermanentSelection>()` → `_targetPermanents` | Original consumption **is** the inline `Activate()` queue read. |
| B-53 | `SelectPermanentEffect.cs:607` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectPermanentEffect.cs:1052` | `SelectPermanentEffect.cs:688-689` | Batch path inside A-42's helper. |
| B-54 | `SelectPermanentEffect.cs:648` | `ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `SelectPermanentEffect.cs:1052` | `SelectPermanentEffect.cs:688-689` | `byPreSelectedList` incremental path inside A-42's helper. |

> Note: the `Select*.cs` block above holds 15 rows (B-40 … B-54); the section heading count of 11 refers to
> distinct files touched, not rows.

### Script/TurnStateMachine.cs (1)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-55 | `TurnStateMachine.cs:261` | `Headless.Choices.ChoiceResult mulliganSelection = await _context.ChoiceProvider.ChooseAsync(…)` (multi-line) | `ChooseAsync` — substrate | `TurnStateMachine.cs:525` `selectionPlayer.QueuePlayerSelection(new ValueSelection(isRedraw))` (`SetRedraw` RPC) | `TurnStateMachine.cs:446` `WaitUntil(player.HasPlayerSelection())` → `:447` `DequeuePlayerSelection<ValueSelection>()` → `:448` `_isRedraw = valueSeletion.ValueAsBool()` (field) | Mulligan yes/no. |

### Script/UserSelectionManager.cs (1)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-56 | `UserSelectionManager.cs:132` | `ChoiceResult result = await RequireContext().ChoiceProvider.ChooseAsync(request)…` | `ChooseAsync` — substrate | `UserSelectionManager.cs:29` `selectionPlayer.QueuePlayerSelection(new ValueSelection(value))` (`SetIntForPlayer` RPC; bool variant `:53`) | `UserSelectionManager.cs:81` `WaitUntil(_selectPlayer.HasPlayerSelection())` → `:83` `DequeuePlayerSelection<ValueSelection>()` → `:87` `_selectedIntValue = valueSeletion.ValueAsInt()` (field, exposed as `SelectedIntValue`/`SelectedBoolValue`) | Generic int/bool mode selection (`WaitForEndSelect`). |

### CardEffect/BT17/Red/BT17_095.cs (1)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-57 | `BT17_095.cs:500` | `Permanent materialized = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);` | `CreateNewPermanent` (A-09) | `DCGO/…/BT17_095.cs:451` `var playedPermanent = new Permanent(new List<CardSource>(){selectedCardSource}){IsSuspended=false}` — **synchronous** local; then void coroutine `:453-454` | Original does **not** read `playedPermanent`; re-derives via `selectedCardSource.PermanentOfThisCard().PermanentFrame.FrameID` at `:462` | Same shape as B-36. |

### CardEffect/EX11/White/EX11_070.cs (1)

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-58 | `EX11_070.cs:132` | `ChoiceResult mindLinkResult = await card.Context.ChoiceProvider.ChooseAsync(mindLinkClass.BuildRequest());` | `ChooseAsync` — substrate | `SelectPermanentEffect.cs:1052` `QueuePlayerSelection(new PermanentSelection(…))` (`SetTargetFrames` RPC), driven by `SelectPermanentEffect.Activate()` inside `MindLinkClass.MindLink()` `DCGO/…/MindLink.cs:69` | `SelectPermanentEffect.cs:688-689` `WaitUntil` + `DequeuePlayerSelection<PermanentSelection>()`; delivered to the `SelectPermanentCoroutine` callback `MindLink.cs:71`→`:77` | Original `EX11_070.cs:78-83` simply calls `new MindLinkClass(...).MindLink()`; the mirror splits it into `BuildRequest()` → `ChooseAsync` → `MindLink(selectedId)`. |

### CardEffect/TestFixtures/ (4) — port-only, no original

`find DCGO/Assets/Scripts -iname "TfxPlayOption.cs" -o -iname "TfxSelectDigivolve.cs" -o -iname "TfxSelectMode.cs"`
returns nothing. These are test fixtures added by the port; there is no original counterpart to cite.

| id | port file:line | statement | awaited member | original write site | original read site | notes |
|---|---|---|---|---|---|---|
| B-59 | `TfxPlayOption.cs:52` | `ChoiceResult result = await card.Context.ChoiceProvider.ChooseAsync(request, CancellationToken.None)…` | `ChooseAsync` — substrate | — | — | **Port-only, no original.** Selects 1 Option from hand, then drives `CardEffectCommons.PlayOptionCards`; `result` consumed at `:61` to build `selected`. |
| B-60 | `TfxSelectDigivolve.cs:68` | `ChoiceResult targetResult = await context.ChoiceProvider.ChooseAsync(…)` | `ChooseAsync` — substrate | — | — | **Port-only, no original.** Picks the battle-area target Digimon; `targetResult.SelectedIds[0]` consumed at `:77` as `targetId`. |
| B-61 | `TfxSelectDigivolve.cs:88` | `ChoiceResult sourceResult = await context.ChoiceProvider.ChooseAsync(…)` | `ChooseAsync` — substrate | — | — | **Port-only, no original.** Picks the hand source Digimon; `sourceResult.SelectedIds[0]` consumed at `:97` as `sourceId`. |
| B-62 | `TfxSelectMode.cs:81` | `ChoiceResult result = await card.Context.ChoiceProvider.ChooseAsync(…)` | `ChooseAsync` — substrate | — | — | **Port-only, no original.** Mode menu (Draw 1/3/[5]); `result.SelectedIds[0]` parsed at `:91` for the branch index, then runs `available[index].Branch.Activate`. |

---

## Port-only members (no original counterpart)

Stated plainly, per the brief:

- `IChoiceProvider.ChooseAsync` — declared `src/HeadlessDCGO.Engine/Headless/Choices/IChoiceProvider.cs`,
  **outside** `Assets/Scripts`. The original Unity game has no "ChoiceProvider" abstraction; it drives UI
  panels and coroutines directly. Every `ChooseAsync` B-site above is mapped to the concrete original
  decision it stands in for (queue seam or `SelectCardPanel.SelectedIndex`), never to a same-named method.
- `IZoneMover.DrawAsync`, `IZoneMover.AddSecurityFromLibraryAsync` — declared
  `src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs`, outside `Assets/Scripts`. The originals keep the
  corresponding values in **method-locals** inside a single coroutine (B-07, B-08), so no cross-coroutine
  carrier exists.
- `RunAsIsSelectionAsync` ×3 (A-40, A-41, A-42) and `SelectCardsFromRevealPoolAsync` (A-37) — helper names
  invented by the port; the originals perform these selections inline in `Activate()`.
- `HeadlessAttackState` (A-08) — substrate record, `Headless/Runtime/HeadlessAttackState.cs:5`; the original
  keeps attack outcome on the `AttackProcess` singleton's mutable properties.
- The 4 `CardEffect/TestFixtures/` sites (B-59 … B-62) — fixtures with no original file at all.
- Every `bool`/`int` "did it happen / how many" payload on A-01 … A-07, A-10 … A-21, A-38 — the corresponding
  originals are void `IEnumerator` (or, for `AddCardSource`, synchronous `void`) and return nothing.
