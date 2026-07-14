// Source: Assets/Scripts/Script/MultipleSkills.cs
// Decision: PORT
// Category: CardEffect
// Migration: AS-IS mirror (R3-W1b, batch W1) — the re-entrant trigger WINDOW loop, built DORMANT.
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// ============================================================================================================
// (R3-W1b / RD-R3W1b-01) 1:1 mirror of AS-IS MultipleSkills.ActivateMultipleSkills /
// ActivateMultipleSkills_OnePlayer (MultipleSkills.cs:21-423): the collect → turn/non-turn split →
// while(true) re-evaluate → order-select → optional-confirm → resolve → RuleProcess → TriggeredSkillProcess
// cut-in recursion window loop, in SkillInfo currency. This REPLACES the RD-R3-01 STOP stub.
//
// DORMANT: no live caller reaches this loop. It is entered only via AutoProcessing.TriggeredSkillProcess ->
// availableMultipleSkills.ActivateMultipleSkills, and TriggeredSkillProcess's drain body is gated on a
// NON-EMPTY StackedSkillInfos; the three live cut-in callers (CardController.cs:2623/2887/3258) run over an
// EMPTY cut-in stack in every currently-exercised scenario (0 ported cards return Before/AfterPayCost cut-in
// skills), so the loop body is never reached. The live window today is still WindowResolver (untouched).
//
// SUBSTRATE TRANSLATION (task-approved, per design §1.4/§4):
//  - IEnumerator -> async Task; ContinuousController.StartCoroutine(X)/yield return -> await X.
//  - MonoBehaviourPunCallbacks / photonView.RPC("SetTargetSkill", ...) / SetTargetSkill PunRPC /
//    player.QueuePlayerSelection/HasPlayerSelection/DequeuePlayerSelection: the whole local-player UI +
//    network round-trip that computes and syncs `skillIndex` collapses to ONE call to the SkillInfo choice
//    port (ADAPTATION A2 — ISkillWindowChoicePort.ChooseOrderAsync); it returns the picked index (or null =
//    decline). The AS-IS default (`valueSelection != null ? ValueAsInt() : 0`) is subsumed: the port returns
//    the chosen index directly and null maps to -1 (the AS-IS skip sentinel the Blast/normal branches set).
//  - player.securityObject.securityBreakGlass.* (oldIsSecurityGlassBlue and its SetActive/ShowBlueMatarial
//    guards, AS-IS :169/:195-198/:348-351/:390-392), GManager.commandText.*, player.FieldPermanentObjects,
//    selectHandEffect.SetUpCustomMessage/SetNotShow*/SetIsLocal, IsAI branch: all UI — stripped.
//  - AutomaticOrder.GetSkillIndexAutomaticOrder (AS-IS :304) + ContinuousController.autoEffectOrder: a
//    client-side auto-order setting with NO mirror -> the order pick always routes to the port (substrate note).
//  - CardSource.Owner is a HeadlessPlayerId; gameContext.TurnPlayer/NonTurnPlayer are mirror Players -> the
//    turn/non-turn split compares Owner == player.PlayerId (ADAPTATION). card.PermanentOfThisCard() ->
//    ICardEffect.ResolvePermanentOfThisCard (established adaptation (2)). attackProcess.SecurityDigimon is an
//    instance id -> compare card.InstanceId. turnStateMachine.endGame has no mirror -> terminal via
//    Context.RuleQueryService.IsTerminal().
// The Blast-branch (SelectHandEffect custom mode) vs normal-branch (selectCardPanel) DECISION LOGIC is mirrored
// 1:1; only the UI selection inside each branch is routed through the port.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
using System.Threading;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;
// Disambiguate the AS-IS mirror SkillInfo from the same-import substrate `Headless.Effects.SkillInfo` record.
using SkillInfo = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.SkillInfo;

public sealed class MultipleSkills
{
    private readonly EngineContext _context;

    // (design §2 / A1) the per-instance suspend state (stacked list, in-flight pick, externally-suspended flag,
    // recorded-answer replay). The AS-IS `StackedSkillInfos` field IS this continuation's list (below).
    private readonly SkillWindowContinuation _continuation = new();
    private readonly ISkillWindowChoicePort _choicePort;

    public MultipleSkills(EngineContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        // (ADAPTATION A1/A2, DORMANT) the SkillInfo-currency choice port over this instance's continuation. Not
        // driven live yet — the cutover batch (C) records answers on the continuation from the agent's action.
        _choicePort = new AgentSkillWindowChoicePort(context.ChoiceController, _continuation);
    }

    // AS-IS MultipleSkills.cs:10.
    public bool IsUsing { get; private set; } = false;

    AutoProcessing _autoProcessing = null;

    // AS-IS MultipleSkills.cs:12.
    public List<SkillInfo> SkillInfos_used { get; private set; } = new List<SkillInfo>();

    // AS-IS MultipleSkills.cs:13 — the stacked skills the window re-evaluates each pass. Backed by the
    // continuation (design §2) so the suspend state genuinely holds it; the AS-IS field name/behaviour is intact.
    public List<SkillInfo> StackedSkillInfos
    {
        get => _continuation.StackedSkillInfos;
        set => _continuation.StackedSkillInfos = value;
    }

    // AS-IS MultipleSkills.cs:14-15.
    public bool IsOnlyHandEffectStacked => StackedSkillInfos.Every(skillInfo =>
        skillInfo.CardEffect != null && skillInfo.CardEffect.EffectSourceCard != null && Commons.IsExistOnHand(skillInfo.CardEffect.EffectSourceCard) && skillInfo.CardEffect.EffectDiscription.Contains("[Hand]"));

    // AS-IS MultipleSkills.cs:17.
    bool IsOnlyOptionalEffectStacked => StackedSkillInfos.Every(skillInfo => skillInfo.CardEffect != null && skillInfo.CardEffect.IsSkippable(null));

    // AS-IS MultipleSkills.cs:18-20.
    bool IsEachStackedEffectHasDistinctSourceCard => StackedSkillInfos.Filter(skillInfo1 => skillInfo1.CardEffect != null && skillInfo1.CardEffect.EffectSourceCard != null)
        .Every(skillInfo => StackedSkillInfos.Filter(skillInfo1 => skillInfo1.CardEffect != null && skillInfo1.CardEffect.EffectSourceCard != null)
            .Count(otherSkillInfo => otherSkillInfo != skillInfo && skillInfo.CardEffect.EffectSourceCard == otherSkillInfo.CardEffect.EffectSourceCard) == 0);

    // AS-IS MultipleSkills.cs:21-61.
    public async Task ActivateMultipleSkills(List<SkillInfo> skillInfos, AutoProcessing autoProcessing, bool CheckNewTriggredSkill_mainStack, Func<List<SkillInfo>, SkillInfo, bool> skipCondition)
    {
        _skillIndex = 0;
        IsUsing = true;

        SkillInfos_used = new List<SkillInfo>();
        StackedSkillInfos = new List<SkillInfo>();

        _autoProcessing = autoProcessing;

        List<SkillInfo> TurnPlayerSkillInfos = new List<SkillInfo>();
        List<SkillInfo> NonTurnPlayerSkillInfos = new List<SkillInfo>();

        foreach (SkillInfo skillInfo in skillInfos)
        {
            if (skillInfo != null)
            {
                ICardEffect cardEffect = skillInfo.CardEffect;

                if (cardEffect.EffectSourceCard != null)
                {
                    // ADAPTATION: AS-IS compares CardSource.Owner (a Player) to gameContext.TurnPlayer; the mirror
                    // Owner is a HeadlessPlayerId and TurnPlayer is a mirror Player -> compare against PlayerId.
                    if (GManager.instance.turnStateMachine.gameContext.TurnPlayer is { } turnPlayer && cardEffect.EffectSourceCard.Owner == turnPlayer.PlayerId)
                    {
                        TurnPlayerSkillInfos.Add(skillInfo);
                    }

                    else if (GManager.instance.turnStateMachine.gameContext.NonTurnPlayer is { } nonTurnPlayer && cardEffect.EffectSourceCard.Owner == nonTurnPlayer.PlayerId)
                    {
                        NonTurnPlayerSkillInfos.Add(skillInfo);
                    }
                }
            }
        }

        await ActivateMultipleSkills_OnePlayer(TurnPlayerSkillInfos, GManager.instance.turnStateMachine.gameContext.TurnPlayer, CheckNewTriggredSkill_mainStack, skipCondition);
        await ActivateMultipleSkills_OnePlayer(NonTurnPlayerSkillInfos, GManager.instance.turnStateMachine.gameContext.NonTurnPlayer, CheckNewTriggredSkill_mainStack, skipCondition);

        SkillInfos_used = new List<SkillInfo>();

        IsUsing = false;
    }

    int _skillIndex;

    // AS-IS MultipleSkills.cs:65 — a cut-in effect = a non-main-stack drain on the CUT-IN AutoProcessing instance.
    bool IsCutinEffect(bool CheckNewTriggredSkill_mainStack) => !CheckNewTriggredSkill_mainStack && _autoProcessing != GManager.instance.autoProcessing;

    // AS-IS MultipleSkills.cs:67-423.
    async Task ActivateMultipleSkills_OnePlayer(List<SkillInfo> skillInfos, Player player, bool CheckNewTriggredSkill_mainStack, Func<List<SkillInfo>, SkillInfo, bool> skipCondition)
    {
        StackedSkillInfos = new List<SkillInfo>();

        foreach (SkillInfo skillInfo in skillInfos)
        {
            StackedSkillInfos.Add(skillInfo);
        }

        while (true)
        {
            List<SkillInfo> skillInfos_active = new List<SkillInfo>();

            foreach (SkillInfo skillInfo in StackedSkillInfos)
            {
                #region set the flag whether it is Digimon's effect or Tamer's effect
                if (skillInfo.CardEffect != null)
                {
                    if (skillInfo.CardEffect.EffectSourceCard != null)
                    {
                        CardSource card = skillInfo.CardEffect.EffectSourceCard;

                        if (card != null)
                        {
                            if (!skillInfo.CardEffect.IsDigimonEffect)
                            {
                                if (skillInfo.CardEffect.IsInheritedEffect || skillInfo.CardEffect.IsLinkedEffect)
                                {
                                    skillInfo.CardEffect.SetIsDigimonEffect(true);
                                }
                                else
                                {
                                    // ADAPTATION(2): AS-IS `card.PermanentOfThisCard()` -> ResolvePermanentOfThisCard.
                                    Permanent permanentOfThisCard = ICardEffect.ResolvePermanentOfThisCard(card);

                                    if (permanentOfThisCard != null)
                                    {
                                        skillInfo.CardEffect.SetIsDigimonEffect(permanentOfThisCard.IsDigimon);
                                        skillInfo.CardEffect.SetIsTamerEffect(permanentOfThisCard.IsTamer);
                                    }

                                    else
                                    {
                                        skillInfo.CardEffect.SetIsTamerEffect(card.IsTamer);
                                    }

                                    // ADAPTATION(gap1 SECURITYDIGIMON): AS-IS `card == attackProcess.SecurityDigimon`
                                    // compares CardSources; the mirror SecurityDigimon is the card INSTANCE id.
                                    if (GManager.instance.attackProcess.SecurityDigimon is { } securityDigimon && card.InstanceId == securityDigimon)
                                    {
                                        skillInfo.CardEffect.SetIsDigimonEffect(true);
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Check if the effect can be activated
                if (!skillInfo.CardEffect.CanActivate(skillInfo.Hashtable))
                {
                    continue;
                }

                if (skipCondition != null)
                {
                    if (skipCondition(_autoProcessing.skillInfos_used, skillInfo))
                    {
                        continue;
                    }
                }

                if (skillInfo.CardEffect.ChainActivations > 0)
                {
                    if (GManager.instance.autoProcessing.IsCutInEffectUsedMaxCount(skillInfo.CardEffect))
                    {
                        continue;
                    }
                }

                if (IsCutinEffect(CheckNewTriggredSkill_mainStack))
                {
                    if (GManager.instance.autoProcessing.IsCutInEffectHasUsed(skillInfo.CardEffect))
                    {
                        continue;
                    }
                }

                skillInfos_active.Add(skillInfo);
                #endregion
            }

            skillInfos_active = skillInfos_active.Filter(skillInfo => skillInfo != null && skillInfo.CardEffect != null
                && skillInfo.CardEffect.CanActivate(skillInfo.Hashtable) && skillInfo.CardEffect.EffectSourceCard != null);

            if (skillInfos_active.Count > 0)
            {
                // AS-IS :169 `oldIsSecurityGlassBlue` (player.securityObject.securityBreakGlass.IsBlueGlass && ...)
                // only gates UI (SetActive / ShowBlueMatarial at :195-198/:348-351/:390-392) — stripped.

                #region If the effect list is one, process normally.
                if (skillInfos_active.Count == 1)
                {
                    _skillIndex = 0;

                    await Activate(true);
                }
                #endregion

                #region If there are multiple effect lists, select which one to process first
                else
                {
                    // Blast Digivolution vs normal DECISION LOGIC (AS-IS :187 vs :264), 1:1. The UI selection in
                    // each branch (SelectHandEffect custom mode / selectCardPanel) is routed through the port.
                    bool canDecline;

                    if (IsOnlyHandEffectStacked && IsOnlyOptionalEffectStacked && IsEachStackedEffectHasDistinctSourceCard)
                    {
                        // AS-IS :187-262 Blast branch: SelectHandEffect custom mode, canNoSelect: true (:208).
                        canDecline = true;
                    }

                    else
                    {
                        // AS-IS :264-324 normal branch: selectCardPanel, _CanNoSelect = all skippable (:284).
                        // (AutomaticOrder / autoEffectOrder has no mirror — always route to the port; substrate note.)
                        canDecline = skillInfos_active.All(skillInfo => skillInfo.CardEffect.IsSkippable(skillInfo.Hashtable));
                    }

                    // AS-IS :326-329: WaitUntil(HasPlayerSelection) + DequeuePlayerSelection<ValueSelection>() ->
                    // the port returns the picked index (or null = decline, the AS-IS -1 sentinel).
                    HeadlessPlayerId controller = player is { } p ? p.PlayerId : default;
                    int? pick = await _choicePort.ChooseOrderAsync(skillInfos_active, controller, canDecline, CancellationToken.None);
                    _skillIndex = pick ?? -1;

                    await Activate(!(IsOnlyHandEffectStacked && IsOnlyOptionalEffectStacked && IsEachStackedEffectHasDistinctSourceCard));
                }
                #endregion

                #region Executing the effect
                // AS-IS MultipleSkills.cs:340-394 (local coroutine `Activate`, hoisted — called above).
                async Task Activate(bool isCheckOptional)
                {
                    if (_skillIndex < 0 || skillInfos_active.Count < _skillIndex)
                    {
                        StackedSkillInfos = new List<SkillInfo>();
                        return;
                    }

                    // AS-IS :348-351 securityBreakGlass SetActive = UI (stripped).

                    ICardEffect cardEffect = skillInfos_active[_skillIndex].CardEffect;
                    Hashtable hashtable = skillInfos_active[_skillIndex].Hashtable;

                    StackedSkillInfos.Remove(skillInfos_active[_skillIndex]);

                    skillInfos_active[_skillIndex].CardEffect.SetOnProcessCallbuck(() =>
                        {
                            SkillInfos_used.Add(skillInfos_active[_skillIndex]);
                            cardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(cardEffect);
                        });

                    if (cardEffect is ActivateICardEffect)
                    {
                        if (cardEffect.CanActivate(hashtable))
                        {
                            // For interrupt processing
                            if (IsCutinEffect(CheckNewTriggredSkill_mainStack))
                            {
                                // AS-IS :371 `// GManager.instance.autoProcessing.AddCutinEffect(cardEffect);` — the
                                // add is instead passed as the useEffectCallback below (AS-IS :377).
                                await ((ActivateICardEffect)cardEffect)
                                    .Activate_Optional_Effect_Execute(
                                        hashtable,
                                        isCheckOptional,
                                        useEffectCallback: GManager.instance.autoProcessing.AddCutinEffect);
                            }

                            else
                            {
                                await GManager.instance.autoProcessing.ActivateEffectProcess(
                                    cardEffect,
                                    hashtable,
                                    isCheckOptional);
                            }
                        }
                    }

                    // AS-IS :390-392 securityBreakGlass ShowBlueMatarial = UI (stripped).
                }
                #endregion

                //Rule processing (AS-IS :398)
                await GManager.instance.autoProcessing.RuleProcess();

                // AS-IS :400-403 `if (turnStateMachine.endGame) yield break;` — ADAPTATION: the mirror
                // TurnStateMachine has no `endGame`; terminal state reads through RuleQueryService.
                if (GManager.instance.Context.RuleQueryService.IsTerminal())
                {
                    return;
                }

                #region If there are any newly triggered effects, resolve those first. (AS-IS :405-415)
                if (!CheckNewTriggredSkill_mainStack)
                {
                    await _autoProcessing.TriggeredSkillProcess(CheckNewTriggredSkill_mainStack, skipCondition);
                }

                else
                {
                    await GManager.instance.autoProcessing.TriggeredSkillProcess(CheckNewTriggredSkill_mainStack, null);
                }
                #endregion
            }

            else
            {
                break;
            }
        }
    }
}
