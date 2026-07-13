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
// `SelectCountEffect`, `SelectDigiXrosClass`, `SelectDNACondition`, and the `IEnumerableExtension`
// (`.Filter`/`.Clone`/`.CloneArray`) helpers already exist at `...Script` (one directory up) — `using`d below.
//
// ADAPTATIONS (mechanical, per the FOUNDATION brief — same rules as ICardEffect.cs/OptionResolutionClass.cs):
//   (1) `using UnityEngine;`/`using Photon;` stripped (this file never had its own usings — they lived at the
//       top of the 5988-line AS-IS file; irrelevant Unity/Photon types this class itself never names).
//   (2) `IEnumerator` -> `Task` for every coroutine in this class: `PlayCard()` and the two nested local
//       coroutines `SelectCost()`/`SelectCountCoroutine(int)`.
//       `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X` throughout (established
//       rule, ICardEffect.cs/BlastDNADigivolution.cs). A lone `yield return null;` -> `await
//       Task.CompletedTask;`.
//   (3) `card.PermanentOfThisCard()` / `cardSource.PermanentOfThisCard()` -> `ICardEffect.
//       ResolvePermanentOfThisCard(card)` / `(cardSource)` (the mirror `CardSource.PermanentOfThisCard()`
//       returns a `PermanentView`, not a `Permanent` — same bridge ICardEffect.cs itself uses).
//   (4) UI-ONLY statements stripped with their AS-IS anchors cited inline (PlayLog :785; the Effects component
//       calls — RemoveDigivolveRootEffect :444 / MoveToExecuteCardEffect :529·:673 / ShrinkUpUseHandCard :679 /
//       FailedPlayCardEffect :791 — all pure DOTween/ShowUseHandCard display animations per Effects.cs; the
//       memoryObject.Show/OffMemoryPredictionLine gauge overlay :594-600·:826·:861-863·:1044-1049 incl. the
//       whole `OffMemoryPredictionLine()` helper; SetPermanentIndexText :390 / OffPermanentIndexText :807-813;
//       Show/HideWillEvolutionEffect :745-757; the brainStormObject hand-display loop :803-809; the
//       ShowingHandCard visibility probes + isYou/IsAI/autoMinDigivolutionCost CLIENT-presentation branches
//       inside SelectCost :506-530 and the `noHandCard` probe :649-668 — the mirror ChoiceProvider is the
//       decider those branches steered the Unity client toward).
//   (5) `card.Owner.<member>` (AS-IS Player instance members on the bare mirror `HeadlessPlayerId`): methods
//       ride the `PlayerIdAsIsExtensions` bridges (AddMemory), properties ride the established
//       `new Player(card.Context, card.Owner).<member>` route (HandCards/LibraryCards/SecurityCards/
//       TrashCards/GetFieldPermanents/MaxMemoryCost — the BT2_023 idiom; a bare id cannot carry an extension
//       PROPERTY).
//   (6) AS-IS `CardSource` accessor PROPERTIES whose mirror home (CardSource.cs) is another P6 remediation
//       cluster's file ride the `CardSourceAsIsPlayAccessors` extension bridge at the bottom of THIS file
//       (relocation design item RD-P6C1-9): REAL 1:1 accessors `jogressCondition`->`JogressConditionOf()`,
//       `burstDigivolutionCondition`->`BurstDigivolutionConditionOf()`, `digiXrosCondition`->
//       `DigiXrosConditionOf()`, `HasDigiXros`->`HasDigiXros()`, `IsPermanent`->`IsPermanent()`,
//       `BasePlayCostFromEntity`->`BasePlayCostFromEntity()`; `appFusionCondition` maps to the EXISTING mirror
//       `AppFusionConditionOf()`.
//   (7) STOP bridges (NO mirror subsystem — explicit throw, never a silent stub; design items RD-P6C1-1..8,
//       docs/audit/rebuild_p6_cluster1_notes.md):
//       RD-P6C1-1 field-frame model (Player.fieldCardFrames/FieldCardFrame.GetFramePermanent/
//                 Permanent.PermanentFrame — MIG5-FRAME-MODEL): SetBurst/BurstTamer/IsAppFusion/LinkedCard/
//                 jogress target resolution/CanPlayCardTargetFrame sites;
//       RD-P6C1-2 play/digivolution cost+requirement engine (CardSource.CostList/GetPayingCostWithBaseCost/
//                 CanEvolve/CanPlayCardTargetFrame/CanJogressFromTargetPermanents/
//                 CanBurstDigivolutionFromTargetPermanent/CanAppFusionFromTargetPermanent — the MIG5 PLAY-COST
//                 gap): STOP extensions keep the call-site text verbatim;
//       RD-P6C1-3 cut-in drain (AutoProcessing.TriggeredSkillProcess interior — no MultipleSkills mirror);
//       RD-P6C1-4 sibling classes PlayPermanentClass/UseOptionClass (the final hand-off);
//       RD-P6C1-5 Assembly/DigiXros interactive pre-play selection (mirror SelectAssemblyClass is the STATIC
//                 feasibility half — its component Reset/SetExcludedCards/Select calls are stripped/STOPped);
//       RD-P6C1-6 SelectBurstDigivolutionEffect/SelectAppFusionEffect components;
//       RD-P6C1-8 CardObjectController zone-move statics (== cluster-2's RD-P6C2-1).
//   (8) `card.Owner.UntilCalculateFixedCostEffect = new List<...>()` (:851) -> `EffectDurationExpiry.
//       ExpireFixedCostCalc(card.Context.EffectRegistry)` — the mirror carrier of that AS-IS per-player bucket
//       is the EffectDuration.UntilCalculateFixedCost binding set (same clear PlayCardAction.cs:169 performs
//       at the same AS-IS anchor).

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

    // AS-IS :144-150. The AS-IS guard tail `&& BurstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1`
    // needs the field-frame model — STOP RD-P6C1-1 (a negative id = the AS-IS not-set fallthrough, kept).
    public void SetBurst(int BurstTamerFrameID, CardSource card)
    {
        if (0 <= BurstTamerFrameID)
        {
            // AS-IS: if (0 <= BurstTamerFrameID && BurstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1)
            //            _burstTamerFrameID = BurstTamerFrameID;
            throw new NotSupportedException(
                "STOP: SetBurst needs the field-frame model (AS-IS Player.fieldCardFrames) — no mirror " +
                "frame/slot model exists (design item RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md).");
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

    // AS-IS :215-237. `card.burstDigivolutionCondition` -> `card.BurstDigivolutionConditionOf()`
    // (adaptation (6); re-read per access, exactly like the AS-IS property re-scan).
    bool IsBurst(CardSource card)
    {
        Permanent burstTamer = BurstTamer(card);

        if (burstTamer != null)
        {
            if (burstTamer.TopCard != null)
            {
                if (card.BurstDigivolutionConditionOf() != null)
                {
                    if (card.BurstDigivolutionConditionOf().tamerCondition != null)
                    {
                        if (card.BurstDigivolutionConditionOf().tamerCondition(burstTamer))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :239-249. The frame lookup (`card.Owner.fieldCardFrames[_burstTamerFrameID].GetFramePermanent()`)
    // needs the frame model — STOP RD-P6C1-1. `_burstTamerFrameID < 0` (never SetBurst) = the AS-IS null
    // fallthrough, kept — the only reachable path until RD-P6C1-1 lands (SetBurst itself STOPs).
    Permanent BurstTamer(CardSource card)
    {
        _ = card;

        if (0 <= _burstTamerFrameID)
        {
            // AS-IS: if (0 <= _burstTamerFrameID && _burstTamerFrameID <= card.Owner.fieldCardFrames.Count - 1)
            //        { Permanent tamer = card.Owner.fieldCardFrames[_burstTamerFrameID].GetFramePermanent(); return tamer; }
            throw new NotSupportedException(
                "STOP: BurstTamer needs the field-frame model (AS-IS Player.fieldCardFrames[i].GetFramePermanent) " +
                "— design item RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md.");
        }

        return null;
    }

    // AS-IS :251-275. `card.appFusionCondition` -> the EXISTING mirror `card.AppFusionConditionOf()`
    // (adaptation (6)); the frame lookup for the host digimon is STOP RD-P6C1-1. `linkCard == null`
    // (never SetAppFusion / LinkedCard fallthrough) = the AS-IS false path, kept.
    bool IsAppFusion(CardSource card)
    {
        CardSource linkCard = LinkedCard(card);

        if (linkCard != null)
        {
            if (card.AppFusionConditionOf() != null)
            {
                if (card.AppFusionConditionOf().digimonCondition != null)
                {
                    // AS-IS :259-267: Permanent digimon = card.Owner.fieldCardFrames[_appFusionFrameIDs[0]].GetFramePermanent();
                    //                 if (card.appFusionCondition.linkedCondition != null)
                    //                     if (card.appFusionCondition.linkedCondition(digimon, linkCard)) return true;
                    throw new NotSupportedException(
                        "STOP: IsAppFusion needs the field-frame model (AS-IS Player.fieldCardFrames) — " +
                        "design item RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md.");
                }
            }
        }

        return false;
    }

    // AS-IS :277-294. The frame lookup needs the frame model — STOP RD-P6C1-1; `_appFusionFrameIDs` unset =
    // the AS-IS null fallthrough, kept (the only reachable path until RD-P6C1-1 lands).
    public CardSource LinkedCard(CardSource card)
    {
        _ = card;

        if (_appFusionFrameIDs != null && _appFusionFrameIDs.Length == 2)
        {
            // AS-IS :281-291: if (0 <= _appFusionFrameIDs[0] && _appFusionFrameIDs[0] <= card.Owner.fieldCardFrames.Count - 1)
            //                 { Permanent targetPermanent = card.Owner.fieldCardFrames[_appFusionFrameIDs[0]].GetFramePermanent();
            //                   if (targetPermanent.LinkedCards.Count > _appFusionFrameIDs[1])
            //                   { CardSource link = targetPermanent.LinkedCards[_appFusionFrameIDs[1]]; return link; } }
            throw new NotSupportedException(
                "STOP: LinkedCard needs the field-frame model (AS-IS Player.fieldCardFrames) — design item " +
                "RD-P6C1-1, docs/audit/rebuild_p6_cluster1_notes.md.");
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
            // AS-IS :307 `GManager.instance.GetComponent<SelectAssemblyClass>().ResetSelectAssemblyClass();` —
            // the mirror SelectAssemblyClass is the STATIC feasibility half (material matching lives in the
            // parameterized play action), so there is no component state to reset (adaptation (7), RD-P6C1-5).
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
            else if (new Player(card.Context, card.Owner).HandCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Hand;
            }
            else if (new Player(card.Context, card.Owner).LibraryCards.Contains(card))
            {
                Root = SelectCardEffect.Root.Library;
            }
            else if (new Player(card.Context, card.Owner).GetFieldPermanents().Count((permanent) => permanent.DigivolutionCards.Contains(card)) >= 1)
            {
                Root = SelectCardEffect.Root.DigivolutionCards;
            }
            else if (new Player(card.Context, card.Owner).GetFieldPermanents().Count((permanent) => permanent.LinkedCards.Contains(card)) >= 1)
            {
                Root = SelectCardEffect.Root.LinkedCards;
            }
            else if (new Player(card.Context, card.Owner).SecurityCards.Contains(card))
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

            if (card.IsPermanent())
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
                    // AS-IS :377-392: resolve the two jogress evolution roots from
                    // `card.Owner.fieldCardFrames[JogressFrameID].GetFramePermanent()` (+ the
                    // SetPermanentIndexText display loop = UI) — the frame model has no mirror: STOP RD-P6C1-1.
                    throw new NotSupportedException(
                        "STOP: jogress target resolution needs the field-frame model (AS-IS " +
                        "Player.fieldCardFrames[JogressFrameID].GetFramePermanent) — design item RD-P6C1-1, " +
                        "docs/audit/rebuild_p6_cluster1_notes.md.");
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

            foreach (CardSource cardSource in new Player(card.Context, card.Owner).TrashCards)
            {
                oldTrashCards.Add(cardSource);
            }

            // effect of removing digivolution/linked cards
            // AS-IS :441-445: `if (card.IsPermanent && !isEvolution && card.PermanentOfThisCard() != null &&
            // (Root == DigivolutionCards || Root == LinkedCards)) yield return ... GManager.instance.
            // GetComponent<Effects>().RemoveDigivolveRootEffect(card, card.PermanentOfThisCard());` —
            // Effects.RemoveDigivolveRootEffect (Effects.cs:2162-2265) is a pure ShowUseHandCard/DOTween
            // display animation (no game-state change; the actual digivolution-card removal happens in the
            // play flow itself) = UI, stripped (adaptation (4)).

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
                                        CostList.Add(card.BurstDigivolutionConditionOf().cost);

                                    if (isAppFusion)
                                        CostList.Add(card.AppFusionConditionOf().cost);
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

                                        // AS-IS :506-530: the `MoveToExecuteCardEffect` bool + ShowingHandCard
                                        // visibility probe + `!card.Owner.isYou && GManager.instance.IsAI` +
                                        // `card.Owner.isYou && ContinuousController.instance.
                                        // autoMinDigivolutionCost` branches (which could reset costSelected on
                                        // the AI/auto-min CLIENT) + the Effects.MoveToExecuteCardEffect
                                        // animation await — Unity-client presentation steering only; the
                                        // mirror ChoiceProvider is the decider (adaptation (4)).

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
                                baseCost = card.BasePlayCostFromEntity();
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
                else if (card.HasDigiXros() && !isEvolution)
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
                        // AS-IS :649-673: the `noHandCard` ShowingHandCard visibility probe + the
                        // Effects.MoveToExecuteCardEffect display move = UI, stripped (adaptation (4)).
                    }
                    else
                    {
                        if (CardEffect == null)
                        {
                            // AS-IS :679: Effects.ShrinkUpUseHandCard(Effects.ShowUseHandCard) = UI, stripped
                            // (adaptation (4)).
                        }
                    }
                }
            }

            #endregion

            #region show expected cost

            // AS-IS :688-704: computes the expected paying cost ONLY to feed
            // `GManager.instance.memoryObject.ShowMemoryPredictionLine(card.Owner.ExpectedMemory(cost))` — the
            // memory-gauge prediction overlay = UI; the whole region (including its side-effect-free
            // GetPayingCostWithBaseCost probe) is stripped (adaptation (4)). The AUTHORITATIVE cost fix happens
            // below in `#region fix cost to pay`.

            #endregion

            #region process cut in effects before paying cost

            if (skillInfos_BeforePayCost.Count >= 1)
            {
                foreach (SkillInfo skillInfo in skillInfos_BeforePayCost)
                {
                    GManager.instance.autoProcessing_CutIn.PutStackedSkill(skillInfo);
                }

                // AS-IS :745-757 `if (IsShowEffect()) targetPermanent.ShowWillEvolutionEffect();` loop —
                // WillEvolutionObject display = UI, stripped (adaptation (4)).

                await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(false, AutoProcessing.HasExecutedSameEffect);

                // AS-IS :763-769 `targetPermanent.HideWillEvolutionEffect();` loop = UI, stripped
                // (adaptation (4)).
            }

            #endregion

            if (CardSources.Count == 1) //Do Digixros in this loop if playing 1 card as they will be needed to calculate cost, else will be done just before play
            {

                #region select DigiXros

                if (card.HasDigiXros() && !isEvolution)
                {
                    GManager.instance.GetComponent<SelectDigiXrosClass>().SetExcludedCards(CardSources);
                    await GManager.instance.GetComponent<SelectDigiXrosClass>().Select(card);
                }

                #endregion

                #region select Assembly

                if (card.HasAssembly && !isEvolution)
                {
                    // AS-IS :755-756: `GManager.instance.GetComponent<SelectAssemblyClass>().SetExcludedCards(
                    // CardSources);` + `yield return ... .Select(card);` — the AS-IS interactive Assembly
                    // material pre-selection component; the mirror SelectAssemblyClass is the STATIC
                    // feasibility half (materials ride the parameterized play action), so the component flow
                    // has no mirror: STOP RD-P6C1-5.
                    throw new NotSupportedException(
                        "STOP: Assembly pre-play material selection (AS-IS SelectAssemblyClass.Select) has no " +
                        "mirror component flow — design item RD-P6C1-5, docs/audit/rebuild_p6_cluster1_notes.md.");
                }

                #endregion

            }

            #region Bounce Tamer of Burst digivolution

            if (IsBurst(card))
            {
                // AS-IS :770-786: `yield return ... GManager.instance.selectBurstDigivolutionEffect.BounceTamer(
                // BurstTamer(card));` then the `!TamerBounced` retry (`_burstTamerFrameID = -1; SelectCost();`)
                // else `burstDigivolved = true;` — SelectBurstDigivolutionEffect (a 345-line component: the
                // tamer bounce is GAME STATE) has no mirror: STOP RD-P6C1-6. Unreachable today — IsBurst()
                // needs a burst frame id and SetBurst/BurstTamer STOP first (RD-P6C1-1).
                throw new NotSupportedException(
                    "STOP: Burst digivolution tamer bounce (AS-IS GManager.selectBurstDigivolutionEffect) has " +
                    "no mirror — design item RD-P6C1-6, docs/audit/rebuild_p6_cluster1_notes.md.");
            }

            #endregion

            #region Add Link Card of App Fusion

            if (IsAppFusion(card))
            {
                // AS-IS :792-808: `yield return ... GManager.instance.selectAppFusionEffect.AddToSources(
                // LinkedCard(card));` then the `!LinkAdded` retry (`_appFusionFrameIDs = new int[0];
                // SelectCost();`) else `appFusion = true;` — SelectAppFusionEffect (241-line component: the
                // link-card re-source is GAME STATE) has no mirror: STOP RD-P6C1-6. Unreachable today —
                // IsAppFusion() STOPs on the frame model first (RD-P6C1-1).
                throw new NotSupportedException(
                    "STOP: App-Fusion link-card sourcing (AS-IS GManager.selectAppFusionEffect) has no mirror " +
                    "— design item RD-P6C1-6, docs/audit/rebuild_p6_cluster1_notes.md.");
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
                    if (card.JogressConditionOf().Count > 0)
                    {
                        Cost = card.GetPayingCostWithBaseCost(card.JogressConditionOf()[baseDNA].cost, Root, targetPermanents, checkAvailability: false, FixedCost: _fixedCost);
                    }
                }

                // AS-IS :826 memoryObject.ShowMemoryPredictionLine(card.Owner.ExpectedMemory(Cost)) = UI,
                // stripped (adaptation (4)).
            }

            #endregion

            #region end play cards

            bool endPlayCard = false;
            bool playFailed = false;

            if (PayCost)
            {
                if (Cost > new Player(card.Context, card.Owner).MaxMemoryCost)
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
                                if (!GetIgnoreRequirement(CardEffectCommons.IgnoreRequirement.Level))
                                {
                                    // AS-IS :813: `if (!GetIgnoreRequirement(Level) && !card.
                                    // CanPlayCardTargetFrame(targetPermanents[0].PermanentFrame, PayCost,
                                    // CardEffect, root: Root, fixedCost: -1)) { endPlayCard = true; playFailed
                                    // = true; }` — needs Permanent.PermanentFrame (frame model, RD-P6C1-1) AND
                                    // the play-cost/requirement engine (RD-P6C1-2): STOP (the short-circuit on
                                    // GetIgnoreRequirement(Level) is preserved).
                                    throw new NotSupportedException(
                                        "STOP: CanPlayCardTargetFrame needs the frame model + the play-cost/" +
                                        "requirement engine — design items RD-P6C1-1/RD-P6C1-2, " +
                                        "docs/audit/rebuild_p6_cluster1_notes.md.");
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

                // AS-IS :790 SelectAssemblyClass component reset — no mirror component state (see the loop-top
                // note; adaptation (7), RD-P6C1-5).

                // AS-IS :791: Effects.FailedPlayCardEffect(card) — a DOTween shake on the brainstorm hand-card
                // display (Effects.cs:2267-2306) = UI, stripped (adaptation (4)).

                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
                {
                    // AS-IS :795-797: `yield return ... CardObjectController.RemoveFromAllArea(card);` +
                    // `yield return ... CardObjectController.AddHandCards(new List<CardSource>() { card },
                    // false, null);` — the failed-play hand restore; the AS-IS static zone-move helper class
                    // has no mirror: STOP RD-P6C1-8 (== cluster-2 design item RD-P6C2-1).
                    throw new NotSupportedException(
                        "STOP: failed-play hand restore needs CardObjectController.RemoveFromAllArea/" +
                        "AddHandCards — no mirror zone-move statics (design item RD-P6C1-8, " +
                        "docs/audit/rebuild_p6_cluster1_notes.md).");
                }

                // AS-IS :801 fire-and-forget OffMemoryPredictionLine() = UI, stripped (adaptation (4)).

                // AS-IS :803-809: the brainStormObject.BrainStormHandCards loop + CloseBrainstrorm — the
                // brainstorm hand display = UI, stripped (adaptation (4)).

                // AS-IS :811-821: the player.FieldPermanentObjects / fieldPermanentCard.OffPermanentIndexText()
                // loop — the jogress index-badge display = UI, stripped (adaptation (4)).

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
                            // AS-IS :843: `yield return ... CardObjectController.AddTrashCard(cardSource);` —
                            // the failed-play trash restore; STOP RD-P6C1-8.
                            throw new NotSupportedException(
                                "STOP: failed-play trash restore needs CardObjectController.AddTrashCard — no " +
                                "mirror zone-move statics (design item RD-P6C1-8, " +
                                "docs/audit/rebuild_p6_cluster1_notes.md).");
                        }
                    }
                }
            }

            #endregion

            // AS-IS :851 `card.Owner.UntilCalculateFixedCostEffect = new List<Func<EffectTiming, ICardEffect>>();`
            // — adaptation (8): the mirror carrier of that per-player bucket is the
            // EffectDuration.UntilCalculateFixedCost binding set (same clear PlayCardAction.cs:169 performs).
            Headless.Effects.EffectDurationExpiry.ExpireFixedCostCalc(card.Context.EffectRegistry);

            if (endPlayCard)
            {
                continue;
            }

            #region pay cost

            if (PayCost)
            {
                // memory lose
                if (Cost <= new Player(card.Context, card.Owner).MaxMemoryCost)
                {
                    await card.Owner.AddMemory(-1 * Cost, null);
                }

                // AS-IS :861 fire-and-forget OffMemoryPredictionLine() = UI, stripped (adaptation (4)).
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
        List<CardSource> permanentCards = playedCards_fixed.Filter(cardSource => cardSource.IsPermanent() && !isDualCardAsOption(cardSource));
        List<CardSource> optionCards = playedCards_fixed.Filter(cardSource => !cardSource.IsPermanent() || isDualCardAsOption(cardSource));

        // (the split lists + burst/appFusion/breeding flags are consumed by the AS-IS hand-off behind the STOP)
        _ = permanentCards;
        _ = optionCards;
        _ = burstDigivolved;
        _ = appFusion;
        _ = _isTapped;
        _ = _activateETB;
        _ = _showEffect;

        // AS-IS :868-960 `#region play permanent` + `#region use option` — the final hand-off:
        //     PlayPermanentClass playPermanent = new PlayPermanentClass(permanentCards, _hashtable, _targetPermanent, _isTapped, Root, _activateETB);
        //     if (isJogress) playPermanent.SetJogress(_jogressEvoRootsFrameIDs);
        //     if (burstDigivolved) playPermanent.SetBurstDigivolved();
        //     if (appFusion) playPermanent.SetAppFusion(_appFusionFrameIDs);
        //     if (_isBreedingArea) playPermanent.SetIsBreedingArea();
        //     yield return ContinuousController.instance.StartCoroutine(playPermanent.PlayPermanent());
        //     UseOptionClass useOption = new UseOptionClass(optionCards, _hashtable, Root) { _showEffect = _showEffect };
        //     yield return ContinuousController.instance.StartCoroutine(useOption.UseOption());
        // — the sibling nested CardController classes `PlayPermanentClass`/`UseOptionClass` are UNPORTED
        // (explicitly out of this port's 4-type scope; the verified headless play executors live in
        // PlayCardAction/PlayCardsBridge but do NOT match this seam — the cost was already paid above, so
        // re-entering the bridge would double-pay): STOP RD-P6C1-4.
        throw new NotSupportedException(
            "STOP: PlayCardClass.PlayCard reached the PlayPermanentClass/UseOptionClass hand-off — the sibling " +
            "AS-IS classes are unported (design item RD-P6C1-4, docs/audit/rebuild_p6_cluster1_notes.md).");

        #endregion
    }

    // AS-IS :1044-1049 `IEnumerator OffMemoryPredictionLine()` — a WaitForSeconds-delayed
    // `GManager.instance.memoryObject.OffMemoryPredictionLine()` (the memory-gauge prediction overlay) = UI,
    // stripped WITH its two fire-and-forget call sites (:801/:861) (adaptation (4)).
}

/// <summary>(P6C1) AS-IS <c>CardSource</c> members the AS-IS-verbatim play pipeline reads, bridged as
/// extensions because their AS-IS home (<c>CardSource.cs</c>) belongs to another P6 remediation cluster —
/// relocate them into the mirror <c>CardSource</c> when that file is free (design item RD-P6C1-9,
/// docs/audit/rebuild_p6_cluster1_notes.md). Two kinds:
/// <list type="bullet">
/// <item>REAL 1:1 accessors (the AS-IS property bodies verbatim over the live <c>EffectList(None)</c> scan —
/// the same shape the existing mirror <c>AppFusionConditionOf</c>/<c>AssemblyConditionOf</c> established):
/// <see cref="JogressConditionOf"/>, <see cref="BurstDigivolutionConditionOf"/>, <see cref="DigiXrosConditionOf"/>,
/// <see cref="HasDigiXros"/>, <see cref="IsPermanent"/>, <see cref="BasePlayCostFromEntity"/>.</item>
/// <item>STOP bridges for the play/digivolution cost+requirement engine (the MIG5 PLAY-COST gap — AS-IS
/// <c>EvoCosts</c>/<c>GetChangedCostItselef</c>/<c>GetChangedPayingCost</c>/requirement scans are a whole
/// unported subsystem): <see cref="CanEvolve"/>, <see cref="CostList"/>, <see cref="GetPayingCostWithBaseCost"/>,
/// <see cref="CanJogressFromTargetPermanents"/>, <see cref="CanBurstDigivolutionFromTargetPermanent"/>,
/// <see cref="CanAppFusionFromTargetPermanent"/> — design item RD-P6C1-2; they keep the AS-IS call-site text
/// verbatim and throw, never guess.</item>
/// </list></summary>
public static class CardSourceAsIsPlayAccessors
{
    /// <summary>(P6C1) AS-IS <c>CardSource.jogressCondition</c> (CardSource.cs:2707-2722) — verbatim: every
    /// usable <c>IAddJogressConditionEffect</c>'s non-null condition from this card's live effect list.</summary>
    public static List<JogressCondition> JogressConditionOf(this CardSource card)
    {
        List<JogressCondition> addJogressConditionEffect =
        card.EffectList(EffectTiming.None)
        .Filter(cardEffect => cardEffect is IAddJogressConditionEffect
            && cardEffect.CanUse(null)
            && ((IAddJogressConditionEffect)cardEffect).GetJogressCondition(card) != null)
        .Select(cardEffect => ((IAddJogressConditionEffect)cardEffect).GetJogressCondition(card))
        .ToList();

        return addJogressConditionEffect;
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.burstDigivolutionCondition</c> (CardSource.cs:2987-3009) — verbatim:
    /// the first usable <c>IAddBurstDigivolutionConditionEffect</c>'s non-null condition.</summary>
    public static BurstDigivolutionCondition BurstDigivolutionConditionOf(this CardSource card)
    {
        foreach (ICardEffect cardEffect in card.EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddBurstDigivolutionConditionEffect)
            {
                if (cardEffect.CanUse(null))
                {
                    BurstDigivolutionCondition burstDigivolutionCondition = ((IAddBurstDigivolutionConditionEffect)cardEffect).GetBurstDigivolutionCondition(card);

                    if (burstDigivolutionCondition != null)
                    {
                        return burstDigivolutionCondition;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.digiXrosCondition</c> (CardSource.cs:2959-2981) — verbatim: the
    /// first usable <c>IAddDigiXrosConditionEffect</c>'s non-null condition.</summary>
    public static DigiXrosCondition DigiXrosConditionOf(this CardSource card)
    {
        foreach (ICardEffect cardEffect in card.EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddDigiXrosConditionEffect)
            {
                if (cardEffect.CanUse(null))
                {
                    DigiXrosCondition digiXrosCondition = ((IAddDigiXrosConditionEffect)cardEffect).GetDigiXrosCondition(card);

                    if (digiXrosCondition != null)
                    {
                        return digiXrosCondition;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.HasDigiXros</c> (CardSource.cs:2569) — verbatim
    /// (<c>digiXrosCondition != null</c>).</summary>
    public static bool HasDigiXros(this CardSource card) => card.DigiXrosConditionOf() != null;

    /// <summary>(P6C1) AS-IS <c>CardSource.IsPermanent</c> (CardSource.cs:3488 → CEntity_Base.cs:238):
    /// Digimon OR Tamer OR Digi-Egg (static printed card kind).</summary>
    public static bool IsPermanent(this CardSource card) => card.IsDigimon || card.IsTamer || card.IsDigiEgg;

    /// <summary>(P6C1) AS-IS <c>CardSource.BasePlayCostFromEntity</c> (CardSource.cs:757-763 —
    /// <c>_cEntity_Base.PlayCost</c>, the raw printed play cost): the mirror carrier of exactly that value is
    /// <c>CardSource.GetCostItself</c> (<c>Definition?.PlayCost ?? 0</c>).</summary>
    public static int BasePlayCostFromEntity(this CardSource card) => card.GetCostItself;

    /// <summary>(P6C1) AS-IS <c>CardSource.CanEvolve(targetPermanent, checkAvailability, ignore)</c>
    /// (CardSource.cs:1263) — the digivolution requirement+cost availability check. STOP: the mirror has no
    /// AS-IS cost/requirement engine (design item RD-P6C1-2; the headless digivolve legality lives in
    /// DigivolveAction/DigivolutionCostHelpers, a different seam).</summary>
    public static bool CanEvolve(this CardSource card, Permanent targetPermanent, bool checkAvailability, CardEffectCommons.IgnoreRequirement ignore = CardEffectCommons.IgnoreRequirement.None)
    {
        _ = card;
        _ = targetPermanent;
        _ = checkAvailability;
        _ = ignore;
        throw new NotSupportedException(
            "STOP: CardSource.CanEvolve (AS-IS CardSource.cs:1263) — the AS-IS digivolution requirement/cost " +
            "engine has no mirror (design item RD-P6C1-2, docs/audit/rebuild_p6_cluster1_notes.md).");
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.CostList(targetPermanent, ignoreLevel, checkAvailability)</c>
    /// (CardSource.cs:617-628 — the <c>EvoCosts</c> projection). STOP: RD-P6C1-2.</summary>
    public static List<int> CostList(this CardSource card, Permanent targetPermanent, bool ignoreLevel, bool checkAvailability)
    {
        _ = card;
        _ = targetPermanent;
        _ = ignoreLevel;
        _ = checkAvailability;
        throw new NotSupportedException(
            "STOP: CardSource.CostList (AS-IS CardSource.cs:617) — the AS-IS EvoCosts/requirement engine has " +
            "no mirror (design item RD-P6C1-2, docs/audit/rebuild_p6_cluster1_notes.md).");
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.GetPayingCostWithBaseCost(baseCost, root, targetPermanents,
    /// checkAvailability, FixedCost)</c> (CardSource.cs:664-751 — DigiXros/Assembly reductions +
    /// <c>GetChangedCostItselef</c> + <c>GetChangedPayingCost</c> modifier scans + the 0 floor). STOP:
    /// RD-P6C1-2 (the MIG5 PLAY-COST gap).</summary>
    public static int GetPayingCostWithBaseCost(this CardSource card, int baseCost, SelectCardEffect.Root root, List<Permanent> targetPermanents, bool checkAvailability = false, int FixedCost = -1)
    {
        _ = card;
        _ = baseCost;
        _ = root;
        _ = targetPermanents;
        _ = checkAvailability;
        _ = FixedCost;
        throw new NotSupportedException(
            "STOP: CardSource.GetPayingCostWithBaseCost (AS-IS CardSource.cs:664) — the AS-IS play-cost " +
            "modifier engine has no mirror (design item RD-P6C1-2, docs/audit/rebuild_p6_cluster1_notes.md).");
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.CanJogressFromTargetPermanents(targetPermanents, PayCost)</c>
    /// (CardSource.cs:2846). STOP: RD-P6C1-2.</summary>
    public static bool CanJogressFromTargetPermanents(this CardSource card, List<Permanent> targetPermanents, bool PayCost)
    {
        _ = card;
        _ = targetPermanents;
        _ = PayCost;
        throw new NotSupportedException(
            "STOP: CardSource.CanJogressFromTargetPermanents (AS-IS CardSource.cs:2846) — the AS-IS jogress " +
            "requirement/cost check has no mirror (design item RD-P6C1-2, docs/audit/rebuild_p6_cluster1_notes.md).");
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.CanBurstDigivolutionFromTargetPermanent(targetPermanent, PayCost)</c>
    /// (CardSource.cs:3211). STOP: RD-P6C1-2.</summary>
    public static bool CanBurstDigivolutionFromTargetPermanent(this CardSource card, Permanent targetPermanent, bool PayCost)
    {
        _ = card;
        _ = targetPermanent;
        _ = PayCost;
        throw new NotSupportedException(
            "STOP: CardSource.CanBurstDigivolutionFromTargetPermanent (AS-IS CardSource.cs:3211) — the AS-IS " +
            "burst-digivolution requirement/cost check has no mirror (design item RD-P6C1-2, " +
            "docs/audit/rebuild_p6_cluster1_notes.md).");
    }

    /// <summary>(P6C1) AS-IS <c>CardSource.CanAppFusionFromTargetPermanent(targetPermanent, PayCost, root)</c>
    /// (CardSource.cs:3378). STOP: RD-P6C1-2.</summary>
    public static bool CanAppFusionFromTargetPermanent(this CardSource card, Permanent targetPermanent, bool PayCost, SelectCardEffect.Root root = SelectCardEffect.Root.Hand)
    {
        _ = card;
        _ = targetPermanent;
        _ = PayCost;
        _ = root;
        throw new NotSupportedException(
            "STOP: CardSource.CanAppFusionFromTargetPermanent (AS-IS CardSource.cs:3378) — the AS-IS " +
            "app-fusion requirement/cost check has no mirror (design item RD-P6C1-2, " +
            "docs/audit/rebuild_p6_cluster1_notes.md).");
    }
}
