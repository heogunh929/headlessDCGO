# 강모델 후속개발 큐 (자동생성)

> `porting/scripts/ir_queue.py` 산출. 저장소 전체 Source IR 기준. 빈도(차단 카드 수)순.

- 코루틴 의도 차단원인: **136종**
- 다중-yield(합성 필요) 카드: **2602장**
- STOP 코드별 카드 수: {'STOP_COMPLEX_TIMING': 2815, 'STOP_MULTI_STEP_OPTIONAL': 2345, 'STOP_RULE_AMBIGUOUS': 964, 'STOP_MISSING_PRIMITIVE': 336}

## 코루틴 의도 차단원인 (빈도순)

| 차단원인(intent) | 차단카드 | 헤드리스 팩토리 | 발화등급 | 작업유형 |
|---|---|---|---|---|
| `selectPermanentEffect.Activate` | 442 | GainMemoryActivatedEffect | fireable | activation(interactive) |
| `CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect` | 212 | RevealDeckTopCardsAndSelect | activated | activation(interactive) |
| `new DrawClass(card.Owner, 1, activateClass).Draw` | 161 | DrawCardsEffect | activated | trigger-wrap|activation |
| `CardEffectCommons.DigivolveIntoHandOrTrashCard` | 102 | — | — | needs-analysis |
| `CardEffectCommons.AddThisCardToHand` | 59 | AddThisCardToHandEffect | activated | trigger-wrap|activation |
| `new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateCl` | 49 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `selectCardEffect.Activate` | 49 | GainMemoryActivatedEffect | fireable | activation(interactive) |
| `new IRecovery(card.Owner, 1, activateClass).Recovery` | 36 | RecoveryTriggerEffect | fireable | maybe-fireable(heuristic) |
| `new IDestroySecurity(` | 36 | — | — | needs-analysis |
| `CardEffectCommons.ShowReducedCost` | 36 | — | — | needs-analysis |
| `CardEffectCommons.DNADigivolvePermanentsIntoHandOrTrashCard` | 27 | — | — | needs-analysis |
| `new IUnsuspendPermanents(new List<Permanent>() { card.PermanentOfThisCard() }, a` | 18 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `selectAttackEffect.Activate` | 17 | GainMemoryActivatedEffect | fireable | activation(interactive) |
| `new IDestroySecurity(` | 17 | — | — | needs-analysis |
| `CardEffectCommons.RevealDeckTopCardsAndProcessForAll` | 16 | — | — | activation(interactive) |
| `CardObjectController.AddSecurityCard` | 16 | — | — | needs-analysis |
| `StartCoroutine` | 14 | — | — | needs-analysis |
| `CardEffectCommons.PlayPermanentCards` | 14 | — | — | needs-analysis |
| `CardEffectCommons.ChangeDigimonSAttack` | 13 | — | — | needs-analysis |
| `new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffect` | 13 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `SharedActivateCoroutine` | 13 | — | — | needs-analysis |
| `CardEffectCommons.DeletePeremanentAndProcessAccordingToResult` | 11 | — | — | needs-analysis |
| `new DrawClass(card.Owner, 2, activateClass).Draw` | 10 | DrawCardsEffect | activated | trigger-wrap|activation |
| `new IAddTrashCardsFromLibraryTop(2, card.Owner, activateClass).AddTrashCardsFrom` | 9 | — | — | needs-analysis |
| `CardEffectCommons.PlayDiaboromonToken` | 9 | — | — | needs-analysis |
| `GManager.instance.attackProcess.SwitchDefender` | 8 | — | — | needs-analysis |
| `new MindLinkClass(` | 8 | MindLinkSelfEffect | fireable | activation(interactive) |
| `CardEffectCommons.ChangeDigimonSAttackPlayerEffect` | 7 | — | — | needs-analysis |
| `CardEffectCommons.SelectTrashDigivolutionCards` | 7 | — | — | activation(interactive) |
| `new IAddTrashCardsFromLibraryTop(3, card.Owner, activateClass).AddTrashCardsFrom` | 6 | — | — | needs-analysis |
| `CardEffectCommons.PlaceDelayOptionCards` | 6 | — | — | needs-analysis |
| `CardEffectCommons.GainRush` | 5 | — | — | needs-analysis |
| `new DrawClass(player, 1, activateClass).Draw` | 5 | DrawCardsEffect | activated | trigger-wrap|activation |
| `new DrawClass(` | 5 | DrawCardsEffect | activated | trigger-wrap|activation |
| `CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom` | 4 | — | — | needs-analysis |
| `new SuspendPermanentsClass(new List<Permanent>() { card.PermanentOfThisCard() },` | 4 | — | — | needs-analysis |
| `GManager.instance.GetComponent<Effects>().CreateBuffEffect` | 4 | — | — | needs-analysis |
| `CardObjectController.MovePermanent` | 4 | — | — | needs-analysis |
| `new IUnsuspendPermanents(` | 4 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `CardEffectCommons.PlayFamiliarToken` | 4 | — | — | needs-analysis |
| `CardEffectCommons.GainCanNotBeDeletedByBattle` | 3 | — | — | needs-analysis |
| `CardEffectCommons.GainCanNotAttackPlayerEffect` | 3 | — | — | needs-analysis |
| `new SuspendPermanentsClass(` | 3 | — | — | needs-analysis |
| `new SuspendPermanentsClass(new List<Permanent>() { card.PermanentOfThisCard() },` | 3 | — | — | needs-analysis |
| `new IUnsuspendPermanents(` | 3 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `new IDestroySecurity(` | 3 | — | — | needs-analysis |
| `mainActivateClass.Activate` | 3 | GainMemoryActivatedEffect | fireable | maybe-fireable(heuristic) |
| `addTrashCard.AddTrashCardsFromLibraryTop` | 3 | — | — | needs-analysis |
| `new IDegeneration(permanent, 1, activateClass).Degeneration` | 3 | — | — | needs-analysis |
| `CardEffectCommons.ChangeDigimonDPPlayerEffect` | 3 | — | — | needs-analysis |
| `CardEffectCommons.GainJamming` | 3 | — | — | needs-analysis |
| `new IDestroySecurity(` | 3 | — | — | needs-analysis |
| `new IUnsuspendPermanents(` | 2 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `new SuspendPermanentsClass(suspendTargetPermanents, CardEffectCommons.CardEffect` | 2 | — | — | needs-analysis |
| `new HandBounceClaass(boounceTargetPermanents, CardEffectCommons.CardEffectHashta` | 2 | SelectAndBounceEffect | fireable | maybe-fireable(heuristic) |
| `card.PermanentOfThisCard().AddDigivolutionCardsBottom` | 2 | — | — | needs-analysis |
| `CardEffectCommons.GainBlocker` | 2 | — | — | needs-analysis |
| `new SuspendPermanentsClass(` | 2 | — | — | needs-analysis |
| `new IDestroySecurity(` | 2 | — | — | needs-analysis |
| `CardEffectCommons.PlayPipeFox` | 2 | — | — | needs-analysis |
| `new IAddTrashCardsFromLibraryTop(1, card.Owner, activateClass).AddTrashCardsFrom` | 2 | — | — | needs-analysis |
| `new IRecovery(card.Owner, 2, activateClass).Recovery` | 2 | RecoveryTriggerEffect | fireable | maybe-fireable(heuristic) |
| `new IDestroySecurity(` | 2 | — | — | needs-analysis |
| `new MindLinkClass(` | 2 | MindLinkSelfEffect | fireable | maybe-fireable(heuristic) |
| `new IDestroySecurity(` | 2 | — | — | needs-analysis |
| `new IDestroySecurity(` | 2 | — | — | needs-analysis |
| `CardEffectCommons.ChangeSecurityDigimonCardDPPlayerEffect` | 2 | — | — | needs-analysis |
| `GManager.instance.GetComponent<Effects>().CreateDebuffEffect` | 2 | — | — | needs-analysis |
| `CardEffectCommons.GainCanNotUnsuspendPlayerEffect` | 1 | — | — | needs-analysis |
| `new DrawClass(card.Owner, card.Owner.SecurityCards.Count / 2, activateClass).Dra` | 1 | DrawCardsEffect | activated | trigger-wrap|activation |
| `new DrawClass(card.Owner, drawCount, activateClass).Draw` | 1 | DrawCardsEffect | activated | trigger-wrap|activation |
| `new SuspendPermanentsClass(` | 1 | — | — | needs-analysis |
| `new DrawClass(` | 1 | DrawCardsEffect | activated | trigger-wrap|activation |
| `CardEffectCommons.GainBlockerPlayerEffect` | 1 | — | — | needs-analysis |
| `new IAddTrashCardsFromLibraryTop(trashCount, card.Owner.Enemy, activateClass).Ad` | 1 | — | — | needs-analysis |
| `CardEffectCommons.PlayKoHagurumonToken` | 1 | — | — | needs-analysis |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `GManager.instance.attackProcess.EndAttack` | 1 | — | — | needs-analysis |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `new IUnsuspendPermanents(new List<Permanent>() { card.PermanentOfThisCard() },` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `new DestroyPermanentsClass(` | 1 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `new IUnsuspendPermanents(new List<Permanent>() { card.PermanentOfThisCard() },` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `selectEnemyEffect.Activate` | 1 | GainMemoryActivatedEffect | fireable | activation(interactive) |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `CardEffectCommons.GainCanNotBeDeletedByEffect` | 1 | — | — | needs-analysis |
| `new IAddTrashCardsFromLibraryTop(OpponentColorCount(), card.Owner, activateClass` | 1 | — | — | needs-analysis |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `new IPutSecurityPermanent(` | 1 | — | — | needs-analysis |
| `new IPutSecurityPermanent(` | 1 | — | — | needs-analysis |
| `new SuspendPermanentsClass(tappedPermanents, hashtable).Tap` | 1 | — | — | needs-analysis |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `CardEffectCommons.FortitudeProcess` | 1 | — | — | needs-analysis |
| `new SuspendPermanentsClass(new List<Permanent>() { card.PermanentOfThisCard() },` | 1 | — | — | needs-analysis |
| `new DeckBottomBounceClass(bounceTargetPermanents, CardEffectCommons.CardEffectHa` | 1 | — | — | needs-analysis |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `new DestroyPermanentsClass(DestroyTargetPermanents, CardEffectCommons.CardEffect` | 1 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `new SuspendPermanentsClass(new List<Permanent> { selectedPermanent }, hashtable)` | 1 | — | — | needs-analysis |
| `new IUnsuspendPermanents(` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `new DestroyPermanentsClass(` | 1 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `new SuspendPermanentsClass(new List<Permanent>() { thisPermament }, hashtable).T` | 1 | — | — | needs-analysis |
| `new DrawClass(` | 1 | DrawCardsEffect | activated | trigger-wrap|activation |
| `selectHandEffect.Activate` | 1 | GainMemoryActivatedEffect | fireable | activation(interactive) |
| `new IUnsuspendPermanents(unsuspendPermanetns, activateClass).Unsuspend` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `CardEffectCommons.GainCanNotBeDeletedPlayerEffect` | 1 | — | — | needs-analysis |
| `CardObjectController.AddHandCards` | 1 | — | — | needs-analysis |
| `new HandBounceClaass(bouncePermanents, hashtable).Bounce` | 1 | SelectAndBounceEffect | fireable | maybe-fireable(heuristic) |
| `new DestroyPermanentsClass(` | 1 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `new DrawClass(card.Owner, count, activateClass).Draw` | 1 | DrawCardsEffect | activated | trigger-wrap|activation |
| `CardEffectCommons.GainRetaliation` | 1 | — | — | needs-analysis |
| `new HatchDigiEggClass(player: card.Owner, hashtable: CardEffectCommons.CardEffec` | 1 | — | — | needs-analysis |
| `new IUnsuspendPermanents(unsuspendTargetPermanents, activateClass).Unsuspend` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `new DestroyPermanentsClass(new List<Permanent>() { card.PermanentOfThisCard() },` | 1 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `new IRecovery(` | 1 | RecoveryTriggerEffect | fireable | maybe-fireable(heuristic) |
| `new IUnsuspendPermanents(` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `new IRecovery(card.Owner, addSecurityCount, activateClass).Recovery` | 1 | RecoveryTriggerEffect | fireable | maybe-fireable(heuristic) |
| `new DestroyPermanentsClass(destroyedPermanetns, hashtable).Destroy` | 1 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `CardEffectCommons.GainPierce` | 1 | — | — | needs-analysis |
| `CardEffectCommons.PlayFujitsumonToken` | 1 | — | — | needs-analysis |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `new HandBounceClaass(bounceTargetPermanents, CardEffectCommons.CardEffectHashtab` | 1 | SelectAndBounceEffect | fireable | maybe-fireable(heuristic) |
| `CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash` | 1 | — | — | needs-analysis |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `CardEffectCommons.PlayUkaNoMitama` | 1 | — | — | needs-analysis |
| `new IUnsuspendPermanents(` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `new IDestroySecurity(` | 1 | — | — | needs-analysis |
| `new IUnsuspendPermanents(` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `CardEffectCommons.BlitzProcess` | 1 | — | — | needs-analysis |
| `new IPutSecurityPermanent(card.PermanentOfThisCard(), CardEffectCommons.CardEffe` | 1 | — | — | needs-analysis |
| `new DrawClass(card.Owner,1,activateClass).Draw` | 1 | DrawCardsEffect | activated | trigger-wrap|activation |
| `new IUnsuspendPermanents(new List<Permanent> { card.PermanentOfThisCard() }, act` | 1 | SelectAndUnsuspendEffect | fireable | maybe-fireable(heuristic) |
| `CardEffectCommons.DrawAndDiscardCards` | 1 | — | — | needs-analysis |
| `CardEffectCommons.PlaySelfDeleteFamiliarToken` | 1 | — | — | needs-analysis |
| `new DestroyPermanentsClass(` | 1 | SelectAndDestroyEffect | fireable | maybe-fireable(heuristic) |
| `new IAddTrashCardsFromLibraryTop(4, card.Owner, activateClass).AddTrashCardsFrom` | 1 | — | — | needs-analysis |
| `selectReturnEffect.Activate` | 1 | GainMemoryActivatedEffect | fireable | activation(interactive) |
