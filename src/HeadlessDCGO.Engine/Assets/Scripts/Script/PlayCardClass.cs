// Source: DCGO/Assets/Scripts/Script/CardController.cs:118-933 (`class PlayCardClass`, nested inside the
// `#region Play cards` region of the ~5988-line Unity `CardController` MonoBehaviour). 1:1 port of JUST that
// class — the top-level command object `CardEffectCommons`/the card-effect factory layer constructs to
// actually play a batch of cards (`new PlayCardClass(cardSources, hashtable, payCost, targetPermanent,
// isTapped, root, activateETB).PlayCard()`), covering digivolution-cost selection, DigiXros/Assembly/Burst/
// AppFusion pre-play selection, the BeforePayCost/AfterPayCost cut-in windows, cost paying, and finally
// handing the filtered card lists off to the (unported, out-of-scope) sibling `PlayPermanentClass`/
// `UseOptionClass`. The surrounding `CardController` class (and its many other nested play/permanent/bounce
// classes) is OUT OF SCOPE for this port — this is a FOUNDATION/P6-missing-type pass (unblock
// declaration-level compilation of the already-ported factory/Hashtable layer that references
// `PlayCardClass`), not a full CardController migration.
//
// Namespace: `...Script.CardEffectCommons` — the same namespace as the already-ported foundation types this
// class is built from (`CardSource`, `Permanent`, `Player`, `ICardEffect`, `CardEffectCommons` static helpers,
// `SkillInfo`, `GManager`, `CardEffectCommons.IgnoreRequirement`). `SelectCardEffect.Root`, `AutoProcessing`,
// `SelectCountEffect`, `SelectAssemblyClass`, and the `IEnumerableExtension` (`.Filter`/`.Clone`/`.CloneArray`)
// helpers already exist at `...Script` (one directory up) — `using`d below.
//
// ADAPTATIONS (mechanical, per the FOUNDATION brief — same rules as ICardEffect.cs/OptionResolutionClass.cs):
//   (1) `using UnityEngine;`/`using Photon;` stripped (this file never had its own usings — they lived at the
//       top of the 5988-line AS-IS file; irrelevant Unity/Photon types this class itself never names).
//   (2) `IEnumerator` -> `Task` for every coroutine in this class: `PlayCard()`, the two nested local
//       coroutines `SelectCost()`/`SelectCountCoroutine(int)`, and `OffMemoryPredictionLine()`.
//       `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X` throughout (established
//       rule, ICardEffect.cs/BlastDNADigivolution.cs). A lone `yield return null;` -> `await
//       Task.CompletedTask;`. The two FIRE-AND-FORGET (non-yielded) `ContinuousController.instance.
//       StartCoroutine(OffMemoryPredictionLine());` calls (:801/:861) drop the same wrapper without adding an
//       `await` (AS-IS never awaited them either) -> `_ = OffMemoryPredictionLine();`.
//   (3) `card.PermanentOfThisCard()` / `cardSource.PermanentOfThisCard()` -> `ICardEffect.
//       ResolvePermanentOfThisCard(card)` / `(cardSource)` (the mirror `CardSource.PermanentOfThisCard()`
//       returns a `PermanentView`, not a `Permanent` — same bridge ICardEffect.cs itself uses; per this
//       goal's brief).
//   (4) `PlayLog.OnAddLog?.Invoke(...)` (:785) stripped (Debug.Log/PlayLog = UI, per this goal's brief).
//   (5) `yield return new WaitForSeconds(0.5f);` (:929, inside `OffMemoryPredictionLine`) stripped — a Unity
//       `YieldInstruction` has no `Task` equivalent; the established rebuild convention for a bare
//       WaitForSeconds yield (ICardEffect.cs `Activate_Effect`, BlastDNADigivolution.cs) is
//       `await Task.CompletedTask;` with the elided statement called out in a comment. The
//       `GManager.instance.memoryObject.OffMemoryPredictionLine()` call the delay guarded is KEPT VERBATIM
//       (masked-missing — not a simplification, `GManager.memoryObject` just is not on the mirror yet).
//
// MASKED-VERBATIM (referenced exactly as AS-IS, NOT on the mirror yet — see
// docs/audit/rebuild_p6_types_missing.md, per the "reference, do not stub-replace" FOUNDATION rule):
// `ContinuousController` (the coroutine-runner singleton itself — every `ContinuousController.instance.*`
// access), `GManager.instance.GetComponent<T>()` (generic component lookup; `GManager` mirror only exposes
// `turnStateMachine`/`autoProcessing`/`attackProcess`/`Context`), `Effects`/`SelectDigiXrosClass`/
// `SelectDNACondition` (GManager components), `GManager.instance.memoryObject`/`.autoProcessing_CutIn`/
// `.selectBurstDigivolutionEffect`/`.selectAppFusionEffect`/`.IsAI` (GManager fields not yet ported),
// `CardObjectController` (static zone-move helper), `HandCard`/`FieldPermanentCard` (Unity display
// components — `card.ShowingHandCard`, `player.FieldPermanentObjects`, `card.Owner.brainStormObject`,
// `permanent.ShowingPermanentCard`), `PlayPermanentClass`/`UseOptionClass` (this class's sibling nested
// CardController classes — out of this port's 4-type scope).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

// AS-IS CardController.cs:118-933.
public class PlayCardClass
{
    // AS-IS :120-134.
    public PlayCardClass(List<CardSource> cardSources, Hashtable hashtable, bool payCost, Permanent targetPermanent, bool isTapped, SelectCardEffect.Root root,
    bool activateETB)
    {
        if (cardSources != null)
        {
            CardSources = cardSources.Filter(cardSource => cardSource != null).Clone();
        }

        _hashtable = hashtable;
        PayCost = payCost;
        _targetPermanent = targetPermanent;
        _isTapped = isTapped;
        Root = root;
        _activateETB = activateETB;
    }

    // AS-IS :136-142.
    public void SetJogress(int[] jogressEvoRootsFrameIDs)
    {
        if (jogressEvoRootsFrameIDs != null)
        {
            _jogressEvoRootsFrameIDs = jogressEvoRootsFrameIDs.CloneArray();
        }
    }

    // AS-IS :144-150.
    public void SetBurst(int BurstTamerFrameID, CardSource card)
    {
        if (0 <= BurstTamerFrameID && BurstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1)
        {
            _burstTamerFrameID = BurstTamerFrameID;
        }
    }

    // AS-IS :152-158.
    public void SetAppFusion(int[] AppFusionFrameID)
    {
        if (AppFusionFrameID != null)
        {
            _appFusionFrameIDs = AppFusionFrameID.CloneArray(); ;
        }
    }

    // AS-IS :160-163.
    public void SetShowEffect()
    {
        _showEffect = true;
    }

    // AS-IS :165-169.
    public void SetIgnoreLevel()
    {
        _ignoreLevel = true;
        SetIgnoreRequirements(CardEffectCommons.IgnoreRequirement.Level);
    }

    // AS-IS :171-174.
    public void SetIgnoreRequirements(CardEffectCommons.IgnoreRequirement ignore)
    {
        _ignoreRequirement = ignore;
    }

    // AS-IS :176-179.
    private bool GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement ignore)
    {
        return _ignoreRequirement.Equals(ignore) || _ignoreRequirement.Equals(CardEffectCommons.IgnoreRequirement.All);
    }

    // AS-IS :181-184.
    public void SetFixedCost(int FixedCost)
    {
        _fixedCost = FixedCost;
    }

    // AS-IS :186-189.
    public void SetReducedCost(int ReducedCost)
    {
        _reducedCost = ReducedCost;
    }

    // AS-IS :191-194.
    public void SetIsBreedingArea()
    {
        _isBreedingArea = true;
    }

    // AS-IS :196-211.
    public List<CardSource> CardSources { get; private set; } = new List<CardSource>();
    Hashtable _hashtable = null;
    public bool PayCost { get; private set; }
    Permanent _targetPermanent = null;
    bool _isTapped = false;
    public SelectCardEffect.Root Root { get; private set; } = SelectCardEffect.Root.None;
    bool _activateETB = true;
    bool _showEffect = false;
    bool _ignoreLevel = false;
    CardEffectCommons.IgnoreRequirement _ignoreRequirement = CardEffectCommons.IgnoreRequirement.None;
    int _fixedCost = -1;
    int _reducedCost = 0;
    int[] _jogressEvoRootsFrameIDs = null;
    int _burstTamerFrameID = -1;
    int[] _appFusionFrameIDs = null;
    bool _isBreedingArea = false;

    // AS-IS :213.
    public bool isJogress => _jogressEvoRootsFrameIDs != null && _jogressEvoRootsFrameIDs.Length == 2;

    // AS-IS :215-237.
    bool IsBurst(CardSource card)
    {
        Permanent burstTamer = BurstTamer(card);

        if (burstTamer != null)
        {
            if (burstTamer.TopCard != null)
            {
                if (card.burstDigivolutionCondition != null)
                {
                    if (card.burstDigivolutionCondition.tamerCondition != null)
                    {
                        if (card.burstDigivolutionCondition.tamerCondition(burstTamer))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :239-249.
    Permanent BurstTamer(CardSource card)
    {
        if (0 <= _burstTamerFrameID && _burstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1)
        {
            Permanent tamer = card.Owner.fieldCardFrames[_burstTamerFrameID].GetFramePermanent();

            return tamer;
        }

        return null;
    }

    // AS-IS :251-275.
    bool IsAppFusion(CardSource card)
    {
        CardSource linkCard = LinkedCard(card);

        if (linkCard != null)
        {
            if (card.appFusionCondition != null)
            {
                if (card.appFusionCondition.digimonCondition != null)
                {
                    Permanent digimon = card.Owner.fieldCardFrames[_appFusionFrameIDs[0]].GetFramePermanent();

                    if (card.appFusionCondition.linkedCondition != null)
                    {
                        if (card.appFusionCondition.linkedCondition(digimon, linkCard))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :277-294.
    public CardSource LinkedCard(CardSource card)
    {
        if (_appFusionFrameIDs != null && _appFusionFrameIDs.Length == 2)
        {
            if (0 <= _appFusionFrameIDs[0] && _appFusionFrameIDs[0] <= card.Owner.fieldCardFrames.Count - 1)
            {
                Permanent targetPermanent = card.Owner.fieldCardFrames[_appFusionFrameIDs[0]].GetFramePermanent();

                if (targetPermanent.LinkedCards.Count > _appFusionFrameIDs[1])
                {
                    CardSource link = targetPermanent.LinkedCards[_appFusionFrameIDs[1]];
                    return link;
                }
            }
        }

        return null;
    }

    // AS-IS :296-1042. `IEnumerator PlayCard()` -> `async Task PlayCard()` (see file header, adaptation (2)).
    public async Task PlayCard()
    {
        bool burstDigivolved = false;
        bool appFusion = false;
        bool isEvolution = false;

        List<CardSource> playedCards_fixed = new List<CardSource>();

        foreach (CardSource card in CardSources)
        {
            GManager.instance.GetComponent<SelectDigiXrosClass>().ResetSelectDigiXrosClass();
            GManager.instance.GetComponent<SelectAssemblyClass>().ResetSelectAssemblyClass();
            GManager.instance.GetComponent<SelectDNACondition>().ResetSelectDNAConditionClass();

            if (card == null)
            {
                continue;
            }

            #region Set Root

            ICardEffect CardEffect = null;

            CardEffect = CardEffectCommons.GetCardEffectFromHashtable(this._hashtable);

            if (CardEffectCommons.IsExistOnTrash(card))
            {
                Root = SelectCardEffect.Root.Trash;
            }
            else if (card.Owner.HandCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Hand;
            }
            else if (card.Owner.LibraryCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Library;
            }
            else if (card.Owner.GetFieldPermanents().Count((permanent) => permanent.DigivolutionCards.Contains(card)) >= 1)
            {
                Root = SelectCardEffect.Root.DigivolutionCards;
            }
            else if (card.Owner.GetFieldPermanents().Count((permanent) => permanent.LinkedCards.Contains(card)) >= 1)
            {
                Root = SelectCardEffect.Root.LinkedCards;
            }
            else if (card.Owner.SecurityCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Security;
            }
            else if (CardEffectCommons.IsExistOnExecutingArea(card))
            {
                Root = SelectCardEffect.Root.Execution;
            }

            #endregion

            #region Set target(s)

            List<Permanent> targetPermanents = new List<Permanent>();

            if (card.IsPermanent)
            {
                if (!isJogress)
                {
                    if (CardEffectCommons.IsOwnerPermanent(_targetPermanent, card))
                    {
                        targetPermanents.Add(_targetPermanent);
                    }
                }
                else
                {
                    for (int i = 0; i < _jogressEvoRootsFrameIDs.Length; i++)
                    {
                        int JogressFrameID = _jogressEvoRootsFrameIDs[i];

                        if (0 <= JogressFrameID && JogressFrameID <= card.Owner.fieldCardFrames.Count - 1)
                        {
                            Permanent targetPermanent = card.Owner.fieldCardFrames[JogressFrameID].GetFramePermanent();
                            targetPermanents.Add(targetPermanent);
                        }
                    }

                    foreach (Permanent permanent in targetPermanents)
                    {
                        permanent.ShowingPermanentCard.SetPermanentIndexText(targetPermanents);
                    }
                }
            }

            #endregion

            #region Determine if Evolution

            if (targetPermanents.Count >= 1)
            {
                if (!isJogress)
                {
                    if (IsBurst(card))
                    {
                        if (card.CanBurstDigivolutionFromTargetPermanent(targetPermanents[0], PayCost))
                        {
                            isEvolution = true;
                        }
                    }
                    else if (IsAppFusion(card))
                    {
                        if (card.CanAppFusionFromTargetPermanent(targetPermanents[0], PayCost))
                        {
                            isEvolution = true;
                        }
                    }
                    else
                    {
                        if (card.CanEvolve(targetPermanents[0], true) || GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement.Level) || _ignoreLevel)
                        {
                            isEvolution = true;
                        }
                    }
                }
                else
                {
                    if (targetPermanents.Count == 2)
                    {
                        isEvolution = true;
                    }
                }
            }

            #endregion

            List<CardSource> oldTrashCards = new List<CardSource>();

            foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForNonTurnPlayer)
            {
                foreach (Permanent permanent in player.GetFieldPermanents())
                {
                    permanent.oldIsTapped_playCard = permanent.IsSuspended;
                }
            }

            foreach (CardSource cardSource in card.Owner.TrashCards)
            {
                oldTrashCards.Add(cardSource);
            }

            // effect of removing digivolution/linked cards
            // AS-IS :442/:444 `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`
            // (adaptation (3) — mirror CardSource.PermanentOfThisCard() returns a PermanentView).
            if (card.IsPermanent && !isEvolution && ICardEffect.ResolvePermanentOfThisCard(card) != null && (Root == SelectCardEffect.Root.DigivolutionCards || Root == SelectCardEffect.Root.LinkedCards))
            {
                await GManager.instance.GetComponent<Effects>().RemoveDigivolveRootEffect(card, ICardEffect.ResolvePermanentOfThisCard(card));
            }

            #region select digivolution cost

            int baseCost = -1;

            bool costSelected = false;

            await SelectCost();

            // AS-IS :455. `IEnumerator SelectCost()` -> `async Task SelectCost()` (adaptation (2)).
            async Task SelectCost()
            {
                if (!isJogress)
                {
                    if (PayCost)
                    {
                        if (_fixedCost < 0)
                        {
                            Permanent targetPermanent = null;

                            if (targetPermanents.Count >= 1)
                            {
                                targetPermanent = targetPermanents[0];
                            }

                            if (targetPermanent != null)
                            {
                                List<int> CostList = new List<int>();

                                bool isBurst = IsBurst(card);
                                bool isAppFusion = IsAppFusion(card);

                                if (isBurst || isAppFusion)
                                {
                                    if (isBurst)
                                        CostList.Add(card.burstDigivolutionCondition.cost);

                                    if (isAppFusion)
                                        CostList.Add(card.appFusionCondition.cost);
                                }
                                else
                                {
                                    foreach (int cost in card.CostList(targetPermanent, ignoreLevel: GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement.Level), checkAvailability: false))
                                    {
                                        int evoCost = cost;

                                        if (_reducedCost > 0)
                                            evoCost -= _reducedCost;

                                        CostList.Add(evoCost);
                                    }
                                }

                                CostList = CostList.Distinct().ToList();

                                if (CostList.Count >= 1)
                                {
                                    if (CostList.Count == 1)
                                    {
                                        baseCost = CostList[0];
                                    }
                                    else
                                    {
                                        costSelected = true;

                                        bool MoveToExecuteCardEffect = true;

                                        if (card.Owner.HandCards.Contains(card) && card.ShowingHandCard != null)
                                        {
                                            if (card.ShowingHandCard.gameObject.activeSelf)
                                            {
                                                MoveToExecuteCardEffect = false;
                                            }
                                        }

                                        if (!card.Owner.isYou && GManager.instance.IsAI)
                                        {
                                            MoveToExecuteCardEffect = false;

                                            costSelected = false;
                                        }

                                        if (card.Owner.isYou && ContinuousController.instance.autoMinDigivolutionCost)
                                        {
                                            MoveToExecuteCardEffect = false;

                                            costSelected = false;
                                        }

                                        if (MoveToExecuteCardEffect)
                                        {
                                            await GManager.instance.GetComponent<Effects>().MoveToExecuteCardEffect(card);
                                        }

                                        SelectCountEffect selectCountEffect = GManager.instance.GetComponent<SelectCountEffect>();

                                        if (selectCountEffect != null)
                                        {
                                            selectCountEffect.SetUp(
                                                SelectPlayer: card.Owner,
                                                targetPermanent: null,
                                                MaxCount: 1,
                                                CanNoSelect: false,
                                                Message: "Which digivolution cost do you pay?",
                                                Message_Enemy: "The opponent is choosing which digivolution cost to pay.",
                                                SelectCountCoroutine: SelectCountCoroutine);

                                            selectCountEffect.SetCandidates(CostList);
                                            selectCountEffect.SetPreferMin(true);
                                            selectCountEffect.SetIsDigivolutionCost(true);

                                            await selectCountEffect.Activate();

                                            // AS-IS :558. `IEnumerator SelectCountCoroutine(int count)` ->
                                            // `async Task SelectCountCoroutine(int count)` (adaptation (2));
                                            // lone `yield return null;` -> `await Task.CompletedTask;`.
                                            async Task SelectCountCoroutine(int count)
                                            {
                                                baseCost = count;
                                                await Task.CompletedTask;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                baseCost = card.BasePlayCostFromEntity;
                            }
                        }
                    }
                }
            }

            #endregion

            #region select DNA condition

            int baseDNA = 0;

            if (isJogress)
            {
                baseDNA = GManager.instance.GetComponent<SelectDNACondition>()._selectedCount;
            }

            #endregion

            #region HashTable Setting

            Hashtable hashtable = CardEffectCommons.WouldEnterFieldHashtable(
                payCost: PayCost,
                card: card,
                root: Root,
                isEvolution: isEvolution,
                playCardClass: this,
                cardEffect: CardEffect,
                isJogress: isJogress,
                targetPermanents: targetPermanents
            );

            #endregion

            // AS-IS :606 `cardSource.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(cardSource)` (adaptation (3)).
            List<SkillInfo> skillInfos_BeforePayCost = AutoProcessing.GetSkillInfos(hashtable, EffectTiming.BeforePayCost)
            .Concat(AutoProcessing.GetSkillInfosOfCards(hashtable, EffectTiming.BeforePayCost, new List<CardSource>() { card }
                .Filter(cardSource => !CardEffectCommons.IsExistOnHand(cardSource) && !CardEffectCommons.IsExistOnTrash(cardSource) && !CardEffectCommons.IsExistInSecurity(cardSource) && ICardEffect.ResolvePermanentOfThisCard(cardSource) == null)))
            .ToList();

            await AutoProcessing.ActivateBackgroundEffects(hashtable, EffectTiming.BeforePayCost);

            #region IsShowEffect()

            bool IsShowEffect()
            {
                if (skillInfos_BeforePayCost.Count >= 2)
                {
                    return true;
                }
                else if (skillInfos_BeforePayCost.Count == 1)
                {
                    if (skillInfos_BeforePayCost[0].CardEffect.CanActivate(skillInfos_BeforePayCost[0].Hashtable))
                    {
                        return true;
                    }
                }
                else if (card.HasDigiXros && !isEvolution)
                {
                    return true;
                }
                else if (IsBurst(card))
                {
                    return true;
                }
                else if (IsAppFusion(card))
                {
                    return true;
                }

                return false;
            }

            #endregion

            #region effect

            if (PayCost || IsShowEffect())
            {
                if (!costSelected)
                {
                    if (card.IsOption || IsShowEffect())
                    {
                        bool noHandCard = true;

                        if (card.Owner.HandCards.Contains(card))
                        {
                            if (card.ShowingHandCard != null)
                            {
                                if (card.ShowingHandCard.gameObject.activeSelf)
                                {
                                    if (card.ShowingHandCard.gameObject.transform.GetChild(0).gameObject.activeSelf)
                                    {
                                        noHandCard = false;
                                    }
                                }
                            }
                        }

                        if (noHandCard)
                        {
                            await GManager.instance.GetComponent<Effects>().MoveToExecuteCardEffect(card);
                        }
                    }
                    else
                    {
                        if (CardEffect == null)
                        {
                            await GManager.instance.GetComponent<Effects>().ShrinkUpUseHandCard(GManager.instance.GetComponent<Effects>().ShowUseHandCard);
                        }
                    }
                }
            }

            #endregion

            #region show expected cost

            if (PayCost)
            {
                if (!isJogress)
                {
                    int cost = card.GetPayingCostWithBaseCost(baseCost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);

                    GManager.instance.memoryObject.ShowMemoryPredictionLine(card.Owner.ExpectedMemory(cost));
                }
                else
                {
                    if (card.jogressCondition.Count > 0)
                    {
                        int cost = card.GetPayingCostWithBaseCost(card.jogressCondition[baseDNA].cost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                        GManager.instance.memoryObject.ShowMemoryPredictionLine(card.Owner.ExpectedMemory(cost));
                    }
                }
            }

            #endregion

            #region process cut in effects before paying cost

            if (skillInfos_BeforePayCost.Count >= 1)
            {
                foreach (SkillInfo skillInfo in skillInfos_BeforePayCost)
                {
                    GManager.instance.autoProcessing_CutIn.PutStackedSkill(skillInfo);
                }

                if (IsShowEffect())
                {
                    foreach (Permanent targetPermanent in targetPermanents)
                    {
                        if (targetPermanent != null)
                        {
                            targetPermanent.ShowWillEvolutionEffect();
                        }
                    }
                }

                await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(false, AutoProcessing.HasExecutedSameEffect);

                foreach (Permanent targetPermanent in targetPermanents)
                {
                    if (targetPermanent != null)
                    {
                        targetPermanent.HideWillEvolutionEffect();
                    }
                }
            }

            #endregion

            if (CardSources.Count == 1) //Do Digixros in this loop if playing 1 card as they will be needed to calculate cost, else will be done just before play
            {

                #region select DigiXros

                if (card.HasDigiXros && !isEvolution)
                {
                    GManager.instance.GetComponent<SelectDigiXrosClass>().SetExcludedCards(CardSources);
                    await GManager.instance.GetComponent<SelectDigiXrosClass>().Select(card);
                }

                #endregion

                #region select Assembly

                if (card.HasAssembly && !isEvolution)
                {
                    GManager.instance.GetComponent<SelectAssemblyClass>().SetExcludedCards(CardSources);
                    await GManager.instance.GetComponent<SelectAssemblyClass>().Select(card);
                }

                #endregion

            }

            #region Bounce Tamer of Burst digivolution

            if (IsBurst(card))
            {
                await GManager.instance.selectBurstDigivolutionEffect.BounceTamer(BurstTamer(card));

                if (!GManager.instance.selectBurstDigivolutionEffect.TamerBounced)
                {
                    _burstTamerFrameID = -1;

                    await SelectCost();
                }
                else
                {
                    burstDigivolved = true;
                }
            }

            #endregion

            #region Add Link Card of App Fusion

            if (IsAppFusion(card))
            {
                await GManager.instance.selectAppFusionEffect.AddToSources(LinkedCard(card));

                if (!GManager.instance.selectAppFusionEffect.LinkAdded)
                {
                    _appFusionFrameIDs = new int[0];

                    await SelectCost();
                }
                else
                {
                    appFusion = true;
                }

            }

            #endregion

            #region fix cost to pay

            int Cost = 0;

            if (PayCost)
            {
                if (!isJogress)
                {
                    Cost = card.GetPayingCostWithBaseCost(baseCost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                    Cost = card.GetPayingCostWithBaseCost(baseCost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                }
                else
                {
                    if (card.jogressCondition.Count > 0)
                    {
                        Cost = card.GetPayingCostWithBaseCost(card.jogressCondition[baseDNA].cost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                    }
                }

                GManager.instance.memoryObject.ShowMemoryPredictionLine(card.Owner.ExpectedMemory(Cost));
            }

            #endregion

            #region end play cards

            bool endPlayCard = false;
            bool playFailed = false;

            if (PayCost)
            {
                if (Cost > card.Owner.MaxMemoryCost)
                {
                    endPlayCard = true;
                    playFailed = true;
                }
            }

            if (isEvolution)
            {
                if (targetPermanents != null)
                {
                    if (targetPermanents.Count >= 1)
                    {
                        foreach (Permanent permanent in targetPermanents)
                        {
                            if (permanent != null)
                            {
                                if (permanent.TopCard == null)
                                {
                                    endPlayCard = true;
                                }
                            }
                        }

                        if (!endPlayCard)
                        {
                            if (!isJogress && !IsBurst(card) && !IsAppFusion(card))
                            {
                                if (!GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement.Level) && !card.CanPlayCardTargetFrame(targetPermanents[0].PermanentFrame, PayCost, CardEffect, root: Root, fixedCost: -1))
                                {
                                    endPlayCard = true;
                                    playFailed = true;
                                }
                            }
                            else if (isJogress)
                            {
                                if (!card.CanJogressFromTargetPermanents(targetPermanents, PayCost))
                                {
                                    endPlayCard = true;
                                    playFailed = true;
                                }
                            }
                            else if (IsBurst(card))
                            {
                                if (!card.CanBurstDigivolutionFromTargetPermanent(targetPermanents[0], PayCost))
                                {
                                    endPlayCard = true;
                                    playFailed = true;
                                }
                            }
                            else if (IsAppFusion(card))
                            {
                                if (!card.CanAppFusionFromTargetPermanent(targetPermanents[0], PayCost))
                                {
                                    endPlayCard = true;
                                    playFailed = true;
                                }
                            }
                        }
                    }
                }
            }

            if (endPlayCard)
            {
                // AS-IS :785 PlayLog = UI (stripped, adaptation (4)).

                GManager.instance.GetComponent<SelectDigiXrosClass>().ResetSelectDigiXrosClass();
                GManager.instance.GetComponent<SelectDNACondition>().ResetSelectDNAConditionClass();

                GManager.instance.GetComponent<SelectAssemblyClass>().ResetSelectAssemblyClass();

                await GManager.instance.GetComponent<Effects>().FailedPlayCardEffect(card);

                if (card.Owner.HandCards.Contains(card))
                {
                    await CardObjectController.RemoveFromAllArea(card);

                    await CardObjectController.AddHandCards(new List<CardSource>() { card }, false, null);
                }

                // AS-IS :801 non-yielded (fire-and-forget) `StartCoroutine` -> drop the wrapper without an
                // `await` (adaptation (2)).
                _ = OffMemoryPredictionLine();

                foreach (HandCard handCard in card.Owner.brainStormObject.BrainStormHandCards)
                {
                    if (handCard.gameObject.activeSelf && handCard.cardSource == card)
                    {
                        await card.Owner.brainStormObject.CloseBrainstrorm(card);
                    }
                }

                foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
                {
                    foreach (FieldPermanentCard fieldPermanentCard in player.FieldPermanentObjects)
                    {
                        if (fieldPermanentCard != null)
                        {
                            fieldPermanentCard.OffPermanentIndexText();
                        }
                    }
                }

                if (playFailed)
                {
                    foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
                    {
                        foreach (Permanent permanent in player.GetFieldPermanents())
                        {
                            permanent.IsSuspended = permanent.oldIsTapped_playCard;
                        }
                    }

                    foreach (CardSource cardSource in oldTrashCards)
                    {
                        if (!CardEffectCommons.IsExistOnTrash(cardSource))
                        {
                            await CardObjectController.AddTrashCard(cardSource);
                        }
                    }
                }
            }

            #endregion

            card.Owner.UntilCalculateFixedCostEffect = new List<Func<EffectTiming, ICardEffect>>();

            if (endPlayCard)
            {
                continue;
            }

            #region pay cost

            if (PayCost)
            {
                // memory lose
                if (Cost <= card.Owner.MaxMemoryCost)
                {
                    await card.Owner.AddMemory(-1 * Cost, null);
                }

                // AS-IS :861 non-yielded (fire-and-forget) `StartCoroutine` -> drop the wrapper (adaptation (2)).
                _ = OffMemoryPredictionLine();
            }

            #endregion

            #region cut in effect after paying cost

            await GManager.instance.autoProcessing_CutIn.StackSkillInfos(hashtable, EffectTiming.AfterPayCost);

            // cur in effect process
            await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(
                false,
                AutoProcessing.HasExecutedSameEffect);

            #endregion

            // add to played cards
            playedCards_fixed.Add(card);
        }

        #region filter cards

        bool isDualCardAsOption(CardSource cardSource) => cardSource.IsDigimon && cardSource.IsOption && !isEvolution;
        List<CardSource> permanentCards = playedCards_fixed.Filter(cardSource => cardSource.IsPermanent && !isDualCardAsOption(cardSource));
        List<CardSource> optionCards = playedCards_fixed.Filter(cardSource => !cardSource.IsPermanent || isDualCardAsOption(cardSource));

        #region play permanent

        PlayPermanentClass playPermanent = new PlayPermanentClass(permanentCards, _hashtable, _targetPermanent, _isTapped, Root, _activateETB);

        if (isJogress)
        {
            playPermanent.SetJogress(_jogressEvoRootsFrameIDs);
        }

        if (burstDigivolved)
        {
            playPermanent.SetBurstDigivolved();
        }

        if (appFusion)
            playPermanent.SetAppFusion(_appFusionFrameIDs);

        if (_isBreedingArea)
        {
            playPermanent.SetIsBreedingArea();
        }

        await playPermanent.PlayPermanent();

        #endregion

        #region use option

        UseOptionClass useOption = new UseOptionClass(optionCards, _hashtable, Root)
        {
            _showEffect = _showEffect
        };

        await useOption.UseOption();

        #endregion

        #endregion
    }

    // AS-IS :1044-1049. `IEnumerator OffMemoryPredictionLine()` -> `async Task OffMemoryPredictionLine()`
    // (adaptation (2)). The `yield return new WaitForSeconds(0.5f);` delay is UI-only (adaptation (5)) —
    // stripped to `await Task.CompletedTask;`; the actual `memoryObject.OffMemoryPredictionLine()` call is
    // kept verbatim (masked-missing, NOT stripped).
    async Task OffMemoryPredictionLine()
    {
        await Task.CompletedTask;

        GManager.instance.memoryObject.OffMemoryPredictionLine();
    }
}
