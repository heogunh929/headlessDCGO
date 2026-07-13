// Source: DCGO/Assets/Scripts/Script/ICardEffect.cs
// (EFFECT-MODEL REBUILD / FOUNDATION) 1:1 mirror of the original abstract `ICardEffect` — every ported card
// effect class (CardEffectCommons/CardEffectFactory.*) ultimately derives from this. Same method order and
// region layout as AS-IS so upstream DCGO2 diffs apply mechanically; only Unity/coroutine plumbing is replaced.
//
// Namespace: placed in `...Script.CardEffectCommons` (NOT `...Script`, where the AS-IS file itself lives) so it
// sits alongside the already-ported foundation types it depends on verbatim: CardSource, Permanent, GManager,
// CardEffectCommons (Hashtable builders), CheckEffectDisabledClass (this goal), CalculateOrder (already at
// CardEffectCommons/ModifierHelpers.cs — NOT redefined here) and EffectDuration (already at
// Headless.Effects.EffectDuration — NOT redefined here; both AS-IS enums already exist on the mirror with a
// 1:1 value set, so this file only *references* them, per the FOUNDATION brief). The (much larger) mirror
// `EffectTiming` enum already lives at CardEffectCommons/EffectTiming.cs and is likewise referenced, not
// redefined.
//
// ADAPTATIONS (translation-rule call-outs, see also docs/audit/rebuild_p1_missing.md):
//   (1) CardSource.cEntity_EffectController / CheckEffectDisabledClass.isDisabled / GManager.instance /
//       CardEffectCommons.*Hashtable builders / EffectList are referenced VERBATIM even where the mirror member
//       does not exist yet (per the FOUNDATION brief: reference, do not stub-replace) — see MISSING.md.
//   (2) AS-IS `CardSource.PermanentOfThisCard()` returns a `Permanent` (or null); the mirror `CardSource`
//       method of the same name returns a `PermanentView` (a read-only Stack projection with `.TopInstanceId` /
//       `.IsEmpty`, not `.TopCard/.IsDigimon/.LinkedCards`). `ResolvePermanentOfThisCard` below is the single
//       adaptation point: it reads the PermanentView and, when non-empty, constructs the REAL mirror `Permanent`
//       (CardEffectCommons/Permanent.cs, ctor `(EngineContext, HeadlessEntityId, HeadlessPlayerId)`) over the
//       same top-card id, so the rest of this file's ported logic reads the AS-IS `Permanent` surface
//       (`.TopCard`, `.IsDigimon`, `.LinkedCards`) unmodified. AS-IS `new Permanent(new List<CardSource>{card})`
//       (a transient single-card permanent, e.g. IsOnDeletion:810) ports to
//       `new Permanent(card.Context, card.InstanceId, card.Owner)` — the mirror ctor's closest analog (see
//       IsOnDeletion below).
//   (3) DESIGN ITEM CARDSOURCE-EQUALITY: AS-IS relies on stable per-card object identity for `==`/`!=`
//       comparisons between `CardSource`/`Permanent` instances (CanActivate, IsSameEffect). The mirror
//       `CardSource`/`Permanent` are lightweight views constructed fresh on every access (e.g. `Permanent.TopCard`
//       returns `new(...)` each call) and neither overrides `Equals`/`GetHashCode`, so `==`/`!=` is REFERENCE
//       equality here — two views of the same live card will compare unequal. Ported verbatim (no invented
//       equality override — that is a cross-cutting foundation decision out of this file's scope); flagged in
//       MISSING.md.
//   (4) UnityAction/UnityAction<T> -> System.Action/System.Action<T>; IEnumerator gate methods stay synchronous
//       bool/property (CanTrigger/CanActivate/CanUse/IsSameEffect/IsOnPlay/IsWhenDigivolving/IsOnDeletion/
//       IsOnAttack); `ActivateICardEffect.Activate` becomes `Task Activate(Hashtable)`; the
//       ActivateICardEffectExtensionClass coroutines become `async Task`, UI stripped (see that class's header).
//   (5) Debug.Log / PlayLog.OnAddLog -> stripped per the FOUNDATION brief.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>AS-IS <c>ICardEffect</c> (DCGO ICardEffect.cs:8) — the base class every card effect derives from.</summary>
public abstract class ICardEffect
{
    #region Set up effect

    // AS-IS ICardEffect.cs:12-41.
    public void SetUpICardEffect(string effectName, Func<Hashtable, bool> canUseCondition, CardSource card)
    {
        SetEffectSourceCard(card);
        SetEffectSourcePermanent(null);
        SetMaxCountPerTurn(-1);
        SetEffectName(effectName);
        SetEffectTargets(null);
        SetEffectDiscription("");
        SetHashString("");
        SetOnProcessCallbuck(null);
        SetRootCardEffect(null);

        SetCanUseCondition(canUseCondition);
        SetCanActivateCondition(null);

        SetIsOptional(false);
        SetIsSkippableFunction(null);
        SetUseOptional(false);
        SetIsDeclarative(false);
        SetIsInheritedEffect(false);
        SetIsLinkedEffect(false);
        SetIsSecurityEffect(false);
        SetIsCounterEffect(false);
        SetIsDigimonEffect(false);
        SetIsTamerEffect(false);
        SetIsOptionEffect(false);
        SetChainActivationCount(-1);
        SetIsBackgroundProcess(false);
        SetNotShowUI(false);
    }

    #endregion

    #region The source card of this effect

    // AS-IS ICardEffect.cs:47-72.
    CardSource _effectSourceCard = null;

    public CardSource EffectSourceCard
    {
        get
        {
            if (EffectSourcePermanent != null)
            {
                // AS-IS: `EffectSourcePermanent.TopCard != null` — the mirror `Permanent.TopCard` is a
                // freshly-constructed CardSource (never null itself); the null-guard is preserved structurally.
                if (EffectSourcePermanent.TopCard != null)
                {
                    return EffectSourcePermanent.TopCard;
                }
            }

            return _effectSourceCard;
        }

        private set { _effectSourceCard = value; }
    }

    public void SetEffectSourceCard(CardSource effectSourceCard)
    {
        EffectSourceCard = effectSourceCard;
    }

    #endregion

    #region The source permanent of this effect

    // AS-IS ICardEffect.cs:74-89.
    private Permanent _effectSourcePermanent = null;

    public Permanent EffectSourcePermanent
    {
        get { return _effectSourcePermanent; }
        private set { _effectSourcePermanent = value; }
    }

    public void SetEffectSourcePermanent(Permanent effectSourcePermanent)
    {
        EffectSourcePermanent = effectSourcePermanent;
    }

    #endregion

    #region Maximum number of times this effect can be used in a turn

    // AS-IS ICardEffect.cs:91-112.
    int _maxCountPerTurn = 114514;

    public int MaxCountPerTurn
    {
        get { return _maxCountPerTurn; }
        private set
        {
            if (value > 0)
            {
                _maxCountPerTurn = value;
            }
        }
    }

    public void SetMaxCountPerTurn(int maxCountPerTurn)
    {
        MaxCountPerTurn = maxCountPerTurn;
    }

    #endregion

    #region Effect name

    // AS-IS ICardEffect.cs:114-139.
    string _effectName = "";

    public string EffectName
    {
        get { return _effectName; }
        private set
        {
            if (string.IsNullOrEmpty(value))
            {
                _effectName = "";
            }
            else
            {
                _effectName = value;
            }
        }
    }

    internal void SetEffectName(string effectName)
    {
        EffectName = effectName;
    }

    #endregion

    #region Effect Target, used to display a detail of what card triggered this / will be targetted by the effect if performed

    // AS-IS ICardEffect.cs:141-159.
    Func<Hashtable, List<Permanent>> _effectTargets = null;

    public Func<Hashtable, List<Permanent>> EffectTargets
    {
        get { return _effectTargets; }
        private set
        {
            _effectTargets = value;
        }
    }

    internal void SetEffectTargets(Func<Hashtable, List<Permanent>> effectTargets)
    {
        EffectTargets = effectTargets;
    }

    #endregion

    #region Effect discription

    // AS-IS ICardEffect.cs:161-186.
    string _effectDiscription = "";

    public string EffectDiscription
    {
        get { return _effectDiscription; }
        private set
        {
            if (string.IsNullOrEmpty(value))
            {
                _effectDiscription = "";
            }
            else
            {
                _effectDiscription = value;
            }
        }
    }

    internal void SetEffectDiscription(string effectDiscription)
    {
        EffectDiscription = effectDiscription;
    }

    #endregion

    #region Hash value for identification of same effects

    // AS-IS ICardEffect.cs:188-213.
    string _hashString = "";

    public string HashString
    {
        get { return _hashString; }
        private set
        {
            if (string.IsNullOrEmpty(value))
            {
                _hashString = "";
            }
            else
            {
                _hashString = value;
            }
        }
    }

    public void SetHashString(string hashString)
    {
        HashString = hashString;
    }

    #endregion

    #region Callbacks when this effect is executed

    // AS-IS ICardEffect.cs:215-230. UnityAction -> System.Action.
    Action _onProcessCallbuck = null;

    public Action OnProcessCallbuck
    {
        get { return _onProcessCallbuck; }
        private set { _onProcessCallbuck = value; }
    }

    public void SetOnProcessCallbuck(Action onProcessCallbuck)
    {
        OnProcessCallbuck = onProcessCallbuck;
    }

    #endregion

    #region Effect giving this effect

    // AS-IS ICardEffect.cs:232-247.
    ICardEffect _rootCardEffect = null;

    public ICardEffect RootCardEffect
    {
        get { return _rootCardEffect; }
        private set { _rootCardEffect = value; }
    }

    public void SetRootCardEffect(ICardEffect rootCardEffect)
    {
        RootCardEffect = rootCardEffect;
    }

    #endregion

    #region Determine whether this effect can be used

    #region Condition for which this triggering effect triggers, this static effect applies or this declarative effect can be declared

    // AS-IS ICardEffect.cs:253-266.
    Func<Hashtable, bool> _canUseCondition = null;

    public Func<Hashtable, bool> CanUseCondition
    {
        get { return _canUseCondition; }
        private set { _canUseCondition = value; }
    }

    public void SetCanUseCondition(Func<Hashtable, bool> canUseCondition)
    {
        CanUseCondition = canUseCondition;
    }

    #endregion

    #region Condition for which this triggering effect activates

    // AS-IS ICardEffect.cs:270-283.
    Func<Hashtable, bool> _canActivateCondition = null;

    public Func<Hashtable, bool> CanActivateCondition
    {
        get { return _canActivateCondition; }
        private set { _canActivateCondition = value; }
    }

    public void SetCanActivateCondition(Func<Hashtable, bool> canActivateCondition)
    {
        CanActivateCondition = canActivateCondition;
    }

    #endregion

    #region Override IsOptional with a function

    // AS-IS ICardEffect.cs:287-299.
    Func<Hashtable, bool> _isOptionalFunction = null;
    public bool IsSkippableCondition(Hashtable hashtable)
    {
        if (_isOptionalFunction != null) return _isOptionalFunction(hashtable);
        return IsOptional;
    }

    public void SetIsSkippableFunction(Func<Hashtable, bool> isOptionalOverride)
    {
        _isOptionalFunction = isOptionalOverride;
    }

    #endregion

    #region If effect is skippable for effects that remove the extra click with isOptional false

    // AS-IS ICardEffect.cs:303-315.
    bool _isSkippable = false;

    public bool IsSkippable(Hashtable hashtable)
    {
        return _isSkippable || IsSkippableCondition(hashtable);
    }

    public void SetIsSkippable(bool isSkippable)
    {
        _isSkippable = isSkippable;
    }

    #endregion

    #region Whether this triggering effect triggers

    // AS-IS ICardEffect.cs:319-358.
    public bool CanTrigger(Hashtable hashtable)
    {
        #region Effects not available before the start of the game

        // AS-IS: GManager.instance / .turnStateMachine / .turnStateMachine.gameContext / .DoneStartGame —
        // MISSING.md: GManager.instance.turnStateMachine.gameContext (present on mirror TurnStateMachine, but
        // the AS-IS null-guard chain is preserved verbatim below).
        if (GManager.instance != null)
        {
            if (GManager.instance.turnStateMachine != null)
            {
                if (GManager.instance.turnStateMachine.gameContext != null)
                {
                    if (!GManager.instance.turnStateMachine.DoneStartGame)
                    {
                        return false;
                    }
                }
            }
        }

        #endregion

        #region Effect availability determined by the maximum number of times it can be used in a turn

        // MISSING.md: CardSource.cEntity_EffectController (this goal ports the controller type + a `CardSource`
        // accessor addition — see CEntity_EffectController.cs / CardSource.cs edit).
        if (EffectSourceCard.cEntity_EffectController.isOverMaxCountPerTurn(this, MaxCountPerTurn))
        {
            return false;
        }

        #endregion

        #region Determination of availability for each effect

        if (CanUseCondition == null || !CanUseCondition(hashtable))
        {
            return false;
        }

        #endregion

        return true;
    }

    #endregion

    #region Whether this triggering effect activates

    // AS-IS ICardEffect.cs:364-457.
    public bool CanActivate(Hashtable hashtable)
    {
        #region Effect availability determined by the maximum number of times it can be used in a turn

        if (EffectSourceCard.cEntity_EffectController.isOverMaxCountPerTurn(this, MaxCountPerTurn))
        {
            return false;
        }

        #endregion

        #region Determination of availability for each effect

        if (CanActivateCondition != null && !CanActivateCondition(hashtable))
        {
            return false;
        }

        #endregion

        #region Determination of availability for Inheritated/Linked Effect

        // ADAPTATION (2): AS-IS reads `EffectSourceCard.PermanentOfThisCard()` (a `Permanent`) directly; the
        // mirror accessor returns a `PermanentView`. `ResolvePermanentOfThisCard` bridges to the real mirror
        // `Permanent` (see file header) so `.TopCard`/`.IsDigimon`/`.LinkedCards` below read the AS-IS surface.
        if (this is ActivateICardEffect)
        {
            if (EffectSourceCard != null)
            {
                Permanent permanentOfThisCard = ResolvePermanentOfThisCard(EffectSourceCard);

                if (permanentOfThisCard != null && !IsOnDeletion)
                {
                    if (IsInheritedEffect || IsLinkedEffect)
                    {
                        // DESIGN ITEM CARDSOURCE-EQUALITY (3): `==` here is reference equality on freshly
                        // constructed CardSource views, not AS-IS per-card identity.
                        if (EffectSourceCard == permanentOfThisCard.TopCard)
                            return false;

                        if (EffectSourceCard.IsFlipped)
                            return false;

                        if (!permanentOfThisCard.IsDigimon)
                            return false;

                        if (IsLinkedEffect && !permanentOfThisCard.LinkedCards.Contains(EffectSourceCard))
                            return false;
                    }
                    else
                    {
                        if (EffectSourceCard != permanentOfThisCard.TopCard)
                        {
                            return false;
                        }
                    }
                }
            }
        }

        #endregion

        #region Determination of availability due to be disabled

        if (IsDisabled)
        {
            return false;
        }

        #endregion
        //TODO: Look into this for the on deletion General issue
        #region Determination whether the permanent is same as when triggered

        if (IsInheritedEffect || IsLinkedEffect)
        {
            if (this is ActivateICardEffect)
            {
                Permanent PermanentWhenTriggered = ((ActivateICardEffect)this).PermanentWhenTriggered;

                if (PermanentWhenTriggered != null)
                {
                    if (EffectSourceCard != null)
                    {
                        Permanent currentPermanent = ResolvePermanentOfThisCard(EffectSourceCard);

                        if (currentPermanent != null)
                        {
                            // DESIGN ITEM CARDSOURCE-EQUALITY (3).
                            if (currentPermanent != PermanentWhenTriggered)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        return true;
    }

    // ADAPTATION (2): the bridge point from `CardSource.PermanentOfThisCard()` (PermanentView) to the
    // real mirror `Permanent`. Returns null when the card is not on a battle-area permanent (AS-IS null).
    // `internal` (not `private`): every other FOUNDATION file with the same AS-IS
    // `card.PermanentOfThisCard()` idiom (CEntity_EffectController.GetCardEffects) reuses this one bridge
    // rather than duplicating it — see PERMANENT-PERMANENTVIEW-DUALITY in docs/audit/rebuild_p1_missing.md.
    internal static Permanent ResolvePermanentOfThisCard(CardSource card)
    {
        PermanentView view = card.PermanentOfThisCard();
        if (view == null || view.IsEmpty)
        {
            return null;
        }

        return new Permanent(card.Context, view.TopInstanceId, card.Owner);
    }

    #endregion

    #region Whether this static effect applies , or this declarative effect can be declared

    // AS-IS ICardEffect.cs:463-473.
    public bool CanUse(Hashtable hashtable)
    {
        if (!CanTrigger(hashtable) || !CanActivate(hashtable))
        {
            return false;
        }

        return true;
    }

    #endregion

    #endregion

    #region Changable bool properties

    #region Whether this effect is an optional effect

    // AS-IS ICardEffect.cs:481-494.
    bool _isOptional = false;

    public bool IsOptional
    {
        get { return _isOptional; }
        private set { _isOptional = value; }
    }

    public void SetIsOptional(bool isOptional)
    {
        IsOptional = isOptional;
    }

    #endregion

    #region Whether to use this optional effect

    // AS-IS ICardEffect.cs:498-511.
    bool _useOptional = false;

    public bool UseOptional
    {
        get { return _useOptional; }
        private set { _useOptional = value; }
    }

    public void SetUseOptional(bool useOptional)
    {
        UseOptional = useOptional;
    }

    #endregion

    #region Whether this effect is a declarative effect

    // AS-IS ICardEffect.cs:515-528.
    bool _isDeclarative = false;

    public bool IsDeclarative
    {
        get { return _isDeclarative; }
        private set { _isDeclarative = value; }
    }

    public void SetIsDeclarative(bool isDeclarative)
    {
        IsDeclarative = isDeclarative;
    }

    #endregion

    #region Whether this effect is an Inherited Effect

    // AS-IS ICardEffect.cs:532-545.
    bool _isInheritedEffect = false;

    public bool IsInheritedEffect
    {
        get { return _isInheritedEffect; }
        private set { _isInheritedEffect = value; }
    }

    public void SetIsInheritedEffect(bool isInheritatedEffect)
    {
        IsInheritedEffect = isInheritatedEffect;
    }

    #endregion

    #region Whether this effect is an Linked Effect

    // AS-IS ICardEffect.cs:549-562.
    bool _isLinkededEffect = false;

    public bool IsLinkedEffect
    {
        get { return _isLinkededEffect; }
        private set { _isLinkededEffect = value; }
    }

    public void SetIsLinkedEffect(bool isLinkededEffect)
    {
        IsLinkedEffect = isLinkededEffect;
    }

    #endregion

    #region Whether this effect is an Security Effect

    // AS-IS ICardEffect.cs:566-579.
    bool _isSecurityEffect = false;

    public bool IsSecurityEffect
    {
        get { return _isSecurityEffect; }
        private set { _isSecurityEffect = value; }
    }

    public void SetIsSecurityEffect(bool isSecurityEffect)
    {
        IsSecurityEffect = isSecurityEffect;
    }

    #endregion

    #region Whether this effect is an Counter Effect

    // AS-IS ICardEffect.cs:583-596.
    bool _isCounterEffect = false;

    public bool IsCounterEffect
    {
        get { return _isCounterEffect; }
        private set { _isCounterEffect = value; }
    }

    public void SetIsCounterEffect(bool isCounterEffect)
    {
        IsCounterEffect = isCounterEffect;
    }

    #endregion

    #region Whether this effect is Digimon's effect

    // AS-IS ICardEffect.cs:600-613.
    bool _isDigimonEffect = false;

    public bool IsDigimonEffect
    {
        get { return _isDigimonEffect; }
        private set { _isDigimonEffect = value; }
    }

    public void SetIsDigimonEffect(bool isDigimonEffect)
    {
        IsDigimonEffect = isDigimonEffect;
    }

    #endregion

    #region Whether this effect is Tamer's effect

    // AS-IS ICardEffect.cs:617-630.
    bool _isTamerEffect = false;

    public bool IsTamerEffect
    {
        get { return _isTamerEffect; }
        private set { _isTamerEffect = value; }
    }

    public void SetIsTamerEffect(bool isTamerEffect)
    {
        IsTamerEffect = isTamerEffect;
    }

    #endregion

    #region Whether this is specifically an Option Card effect, overriding anything settign it as a Digimon or Tamer effect

    // AS-IS ICardEffect.cs:634-647.
    bool _isOptionEffect = false;

    public bool IsOptionEffect
    {
        get { return _isOptionEffect; }
        private set { _isOptionEffect = value; }
    }

    public void SetIsOptionEffect(bool isOptionEffect)
    {
        IsOptionEffect = isOptionEffect;
    }

    #endregion

    #region How many times this effect can activate per chain

    // AS-IS ICardEffect.cs:651-664.
    int _chainActivations = -1;

    public int ChainActivations
    {
        get { return _chainActivations; }
        private set { _chainActivations = value; }
    }

    public void SetChainActivationCount(int chainActivations)
    {
        ChainActivations = chainActivations;
    }

    #endregion

    #region Whether this effect is carried out in the background

    // AS-IS ICardEffect.cs:668-681.
    bool _isBackgroundProcess = false;

    public bool IsBackgroundProcess
    {
        get { return _isBackgroundProcess; }
        private set { _isBackgroundProcess = value; }
    }

    public void SetIsBackgroundProcess(bool isBackgroundProcess)
    {
        IsBackgroundProcess = isBackgroundProcess;
    }

    #endregion

    #region Whether this effect should be displayed in the UI

    // AS-IS ICardEffect.cs:685-698.
    bool _isNotShowUI = false;

    public bool IsNotShowUI
    {
        get { return _isNotShowUI; }
        private set { _isNotShowUI = value; }
    }

    public void SetNotShowUI(bool isNotShowUI)
    {
        IsNotShowUI = isNotShowUI;
    }

    #endregion

    #region When this effect was activated for comparison

    // AS-IS ICardEffect.cs:702-721.
    DateTime _activatedTime = DateTime.MinValue;

    public DateTime ActivatedTime
    {
        get { return _activatedTime; }
        private set { _activatedTime = value; }
    }

    public void SetActivatedTime(params DateTime[] activationTimes)
    {
        if (activationTimes.Length > 0)
        {
            DateTime latest = activationTimes[0];
            foreach (DateTime time in activationTimes)
            {
                if (time > latest) latest = time;
            }
            _activatedTime = latest;
        }
    }

    #endregion

    #endregion

    #region Read only bool properties

    #region Whether this effect is disabled

    // AS-IS ICardEffect.cs:731-737. This goal ports CheckEffectDisabledClass (see CheckEffectDisabledClass.cs).
    public bool IsDisabled
    {
        get
        {
            return CheckEffectDisabledClass.isDisabled(this);
        }
    }

    #endregion

    #region Whether this effect is [On Play] effect

    // AS-IS ICardEffect.cs:743-766. Debug.Log stripped (rule: Debug.Log/PlayLog -> strip).
    // MISSING.md: CardEffectCommons.OnPlayCheckHashtableOfCard(CardSource) (DCGO HashtableSetting.cs:213).
    public bool IsOnPlay
    {
        get
        {
            if (EffectSourceCard != null)
            {
                if (!string.IsNullOrEmpty(EffectDiscription))
                {
                    // AS-IS ICardEffect.cs:751: Debug.Log($"Effect Description: ...") — UI/log (stripped).
                    if (EffectDiscription.StartsWith("[On Play]"))
                    {
                        Hashtable hashtable = CardEffectCommons.OnPlayCheckHashtableOfCard(EffectSourceCard);
                        // AS-IS ICardEffect.cs:755: Debug.Log($"Can Trigger: ...") — UI/log (stripped).
                        if (CanTrigger(hashtable))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    #endregion

    #region Whether this effect is [When Digivolving] effect

    // AS-IS ICardEffect.cs:772-794.
    // MISSING.md: CardEffectCommons.WhenDigivolvingCheckHashtableOfCard(CardSource) (DCGO HashtableSetting.cs:232).
    public bool IsWhenDigivolving
    {
        get
        {
            if (EffectSourceCard != null)
            {
                if (!string.IsNullOrEmpty(EffectDiscription))
                {
                    if (EffectDiscription.StartsWith("[When Digivolving]"))
                    {
                        Hashtable hashtable = CardEffectCommons.WhenDigivolvingCheckHashtableOfCard(EffectSourceCard);

                        if (CanTrigger(hashtable))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    #endregion

    #region Whether this effect is [On Deletion] effect

    // AS-IS ICardEffect.cs:800-824.
    // MISSING.md: CardEffectCommons.OnDeletionHashtable(List<Permanent>, ICardEffect, IBattle, bool)
    // (DCGO HashtableSetting.cs:85); IBattle has no mirror type yet.
    public bool IsOnDeletion
    {
        get
        {
            if (EffectSourceCard != null)
            {
                if (!string.IsNullOrEmpty(EffectDiscription))
                {
                    if (EffectDiscription.StartsWith("[On Deletion]"))
                    {
                        // ADAPTATION (2): AS-IS `EffectSourceCard.PermanentOfThisCard() ?? new Permanent(new
                        // List<CardSource>() { EffectSourceCard })` (ICardEffect.cs:810) — the mirror
                        // `PermanentOfThisCard()` returns a `PermanentView`; bridge via `ResolvePermanentOfThisCard`
                        // and fall back to the mirror ctor's transient single-card analog
                        // `new Permanent(card.Context, card.InstanceId, card.Owner)` when off-field.
                        Permanent effectPermanent = ResolvePermanentOfThisCard(EffectSourceCard)
                            ?? new Permanent(EffectSourceCard.Context, EffectSourceCard.InstanceId, EffectSourceCard.Owner);

                        Hashtable hashtable = CardEffectCommons.OnDeletionHashtable(new List<Permanent>() { effectPermanent }, null, null, false);

                        if (CanTrigger(hashtable))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    #endregion

    #region Whether this effect is [On Attack] effect

    // AS-IS ICardEffect.cs:830-852.
    // MISSING.md: CardEffectCommons.OnAttackCheckHashtableOfCard(CardSource, ICardEffect) (DCGO HashtableSetting.cs:299).
    public bool IsOnAttack
    {
        get
        {
            if (EffectSourceCard != null)
            {
                if (!string.IsNullOrEmpty(EffectDiscription))
                {
                    if (EffectDiscription.StartsWith("[When Attacking]"))
                    {
                        Hashtable hashtable = CardEffectCommons.OnAttackCheckHashtableOfCard(EffectSourceCard, null);

                        if (CanTrigger(hashtable))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    #endregion

    #endregion

    #region Whether the target effect is the same as this effect

    // AS-IS ICardEffect.cs:860-933.
    public bool IsSameEffect(ICardEffect cardEffect)
    {
        if (cardEffect != null)
        {
            if (cardEffect == this)
            {
                return true;
            }

            if (cardEffect.EffectSourceCard != null)
            {
                if (this.EffectSourceCard != null)
                {
                    // DESIGN ITEM CARDSOURCE-EQUALITY (3): `==` on CardSource is reference equality here.
                    if (cardEffect.EffectSourceCard == this.EffectSourceCard)
                    {
                        if (HasSameHashString() && HasSameRootCardEffect())
                        {
                            return true;
                        }

                        bool HasSameHashString()
                        {
                            if (string.IsNullOrEmpty(cardEffect.HashString))
                            {
                                if (string.IsNullOrEmpty(this.HashString))
                                {
                                    return true;
                                }
                            }

                            if (!string.IsNullOrEmpty(cardEffect.HashString))
                            {
                                if (!string.IsNullOrEmpty(this.HashString))
                                {
                                    if (cardEffect.HashString == this.HashString)
                                    {
                                        return true;
                                    }
                                }
                            }

                            return false;
                        }

                        bool HasSameRootCardEffect()
                        {
                            if (cardEffect.RootCardEffect == null)
                            {
                                if (this.RootCardEffect == null)
                                {
                                    return true;
                                }
                            }

                            if (cardEffect.RootCardEffect != null)
                            {
                                if (this.RootCardEffect != null)
                                {
                                    if (cardEffect.RootCardEffect == this.RootCardEffect)
                                    {
                                        return true;
                                    }
                                }
                            }

                            return false;
                        }
                    }
                }
            }
        }

        return false;
    }

    #endregion
}

// AS-IS ICardEffect.cs:938-965 (`CalculateOrder` / `EffectDuration` enum regions): both enums already exist on
// the mirror with a 1:1 AS-IS value set — CalculateOrder at CardEffectCommons/ModifierHelpers.cs:37, EffectDuration
// at HeadlessDCGO.Engine.Headless.Effects.EffectDuration — so they are referenced, not redefined here (per the
// FOUNDATION brief's "if already exists, reference it" rule, extended from EffectTiming to these two).

// AS-IS ICardEffect.cs:967-1032 (`EffectTiming` enum, 65 values): already ported at
// CardEffectCommons/EffectTiming.cs with every AS-IS member name preserved (string-equal to
// TriggerTimings/EffectTimings.ToTriggerName) — referenced, not redefined here.

#region card effect that processes

/// <summary>AS-IS <c>ActivateICardEffect</c> (ICardEffect.cs:1038-1044). <c>IEnumerator Activate(Hashtable)</c>
/// -> <c>Task Activate(Hashtable)</c> (async gate methods stay sync; only the activation body is async).</summary>
public interface ActivateICardEffect
{
    Task Activate(Hashtable hash);

    public Permanent PermanentWhenTriggered { get; set; }
    public CardSource TopCardWhenTriggered { get; set; }
}

/// <summary>AS-IS <c>ActivateICardEffectExtensionClass</c> (ICardEffect.cs:1046-1289). The original coroutines
/// are ~90% Unity UI (ShowActivateCardEffectDiscription, outline/highlight toggling, WaitForSeconds,
/// FieldPermanentCard/HandCard/TrashHandCard visuals) driven by `ContinuousController.instance.StartCoroutine`.
/// Per the FOUNDATION brief only the game-logic skeleton is ported as `async Task`:
///   - the Optional -> Effect -> Execute call ORDER (Activate_Optional_Effect_Execute / Activate_Effect_Execute);
///   - OnProcessCallbuck invoke + clear (Activate_Execute);
///   - AddUse/RemoveUse (the per-turn use-count registration this goal wires to CEntity_EffectController);
///   - the final StackSkillInfos(AfterEffectsActivate) + RuleProcess calls (Activate_Effect_Execute tail).
/// Every stripped UI statement is left as a `// AS-IS <line>: UI (stripped)` comment IN POSITION so the method
/// body's shape still lines up with the original for diffing. `ContinuousController.instance.StartCoroutine(X)`
/// -> `await X` throughout (the coroutine-runner Unity dependency the async/await translation replaces).
/// `UnityAction`/`UnityAction&lt;T&gt;` -> `System.Action`/`System.Action&lt;T&gt;`.
/// MISSING.md: every `GManager.instance.GetComponent&lt;X&gt;()` call below (OptionalSkill/Effects) — referenced
/// verbatim per the FOUNDATION brief (GManager members not yet on the mirror), not stub-replaced.
/// MISSING.md: `GManager.instance.autoProcessing` (AttackProcess/AutoProcessing GManager fields, GManager.cs:84/99).</summary>
public static class ActivateICardEffectExtensionClass
{
    #region Optional

    // AS-IS ICardEffect.cs:1050-1056.
    public static async Task Activate_Optional(this ActivateICardEffect activateICardEffect, Hashtable hash)
    {
        if (((ICardEffect)activateICardEffect).IsOptional)
        {
            // MISSING.md: GManager.instance.GetComponent<OptionalSkill>().SelectOptional(ICardEffect, Hashtable)
            // (DCGO ICardEffect.cs:1054) — the player's yes/no prompt for an optional effect; game logic, not
            // pure UI, so referenced verbatim rather than stripped.
            await GManager.instance.GetComponent<OptionalSkill>().SelectOptional((ICardEffect)activateICardEffect, hash);
        }
    }

    #endregion

    #region Effect

    // AS-IS ICardEffect.cs:1062-1110. This method is entirely presentation (description text, per-zone
    // "play the skill animation" dispatch, a fixed WaitForSeconds) — no state mutation — so it is stripped in
    // full per the FOUNDATION brief, leaving the method as a documented no-op placeholder in position.
    public static async Task Activate_Effect(this ActivateICardEffect activateICardEffect)
    {
        // AS-IS ICardEffect.cs:1064-1109: UI (stripped) — ShowActivateCardEffectDiscription +
        // ActivateFieldPokemonSkillEffect / ActivateHandCardSkillEffect / ActivateTrashCardSkillEffect /
        // ActivateExecutingCardSkillEffect dispatch by zone, + WaitForSeconds(0.4f). No game-state mutation.
        await Task.CompletedTask;
    }

    #endregion

    #region Processing

    // AS-IS ICardEffect.cs:1116-1126.
    public static async Task Activate_Execute(this ActivateICardEffect activateICardEffect, Hashtable hash)
    {
        if (((ICardEffect)activateICardEffect).UseOptional || !((ICardEffect)activateICardEffect).IsOptional)
        {
            ((ICardEffect)activateICardEffect).OnProcessCallbuck?.Invoke();
            ((ICardEffect)activateICardEffect).SetOnProcessCallbuck(null);

            //Handling Effect
            await activateICardEffect.Activate(hash);
        }
    }

    #endregion

    #region Optional→ Effect → Processing

    // AS-IS ICardEffect.cs:1132-1237.
    public static async Task Activate_Optional_Effect_Execute(this ActivateICardEffect activateICardEffect, Hashtable hash, bool isCheckOptional = true, Action<ICardEffect> useEffectCallback = null)
    {
        CardSource card = ((ICardEffect)activateICardEffect).EffectSourceCard;

        // AS-IS ICardEffect.cs:1135-1152: UI (stripped) — resolves `fieldPermanentCard`/`handCard` display
        // handles purely to drive outline/highlight visuals below (also stripped).

        bool oldIsExecuting = GManager.instance.turnStateMachine.isExecuting;

        GManager.instance.turnStateMachine.isExecuting = true;

        // AS-IS ICardEffect.cs:1158-1185: UI (stripped) — fieldPermanentCard.OnSelectEffect/Outline_Select/
        // SetOrangeOutline; handCard.Outline_Select/SetOrangeOutline (owner-only "[Hand]" effect highlight).
        // AS-IS ICardEffect.cs:1186/1189: Debug.Log (stripped).

        if (activateICardEffect is ICardEffect)
        {
            //Optional effect activation selection
            if (((ICardEffect)activateICardEffect).IsOptional)
            {
                if (isCheckOptional)
                {
                    await Activate_Optional(activateICardEffect, hash);
                }
                else
                {
                    ((ICardEffect)activateICardEffect).SetUseOptional(true);
                }
            }

            //Optional effect activation selection → cost → processing
            await Activate_Effect_Execute(activateICardEffect, hash, useEffectCallback);
        }

        // AS-IS ICardEffect.cs:1207-1211: UI (stripped) — `player.TrashHandCard.gameObject.SetActive(false)` /
        // `.IsExecuting = false` for every player (Photon trash-hand-card display reset).
        // AS-IS ICardEffect.cs:1213: UI (stripped) — `yield return new WaitForSeconds(Time.deltaTime * 2)`.
        // AS-IS ICardEffect.cs:1215-1231: UI (stripped) — OffUsingSkillEffect/OffSkillName/RemoveSelectEffect on
        // the source's field permanent card, and on every field permanent card of the card's owner.
        // AS-IS ICardEffect.cs:1228-1230: UI (stripped) — handCard.Outline_Select off.

        if (card != null)
        {
            GManager.instance.turnStateMachine.isExecuting = oldIsExecuting;
        }
    }

    #endregion

    #region remove a usage of an X Per Turn

    // AS-IS ICardEffect.cs:1242-1246.
    public static void RemoveUse(this ActivateICardEffect activateICardEffect)
    {
        ((ICardEffect)activateICardEffect).EffectSourceCard.cEntity_EffectController.RemoveUseEffectThisTurn((ICardEffect)activateICardEffect);
    }

    #endregion

    #region add a usage of an X Per Turn

    // AS-IS ICardEffect.cs:1249-1253.
    public static void AddUse(this ActivateICardEffect activateICardEffect)
    {
        ((ICardEffect)activateICardEffect).EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn((ICardEffect)activateICardEffect);
    }

    #endregion

    #region Effect → Processing

    // AS-IS ICardEffect.cs:1257-1286.
    public static async Task Activate_Effect_Execute(this ActivateICardEffect activateICardEffect, Hashtable hash, Action<ICardEffect> useEffectCallback)
    {
        //cost → effect processing
        if (((ICardEffect)activateICardEffect).UseOptional || !((ICardEffect)activateICardEffect).IsOptional)
        {
            useEffectCallback?.Invoke((ICardEffect)activateICardEffect);

            // AS-IS ICardEffect.cs:1264: Debug.Log (stripped).

            #region Add log

            // AS-IS ICardEffect.cs:1266-1275: PlayLog.OnAddLog (stripped).

            #endregion

            //effect
            await Activate_Effect(activateICardEffect);

            await Activate_Execute(activateICardEffect, hash);
        }

        // MISSING.md: GManager.instance.autoProcessing.StackSkillInfos(ICardEffect, EffectTiming) /
        // .RuleProcess() (DCGO ICardEffect.cs:1283/1285) — referenced verbatim per the FOUNDATION brief.
        await GManager.instance.autoProcessing.StackSkillInfos(null, EffectTiming.AfterEffectsActivate);

        await GManager.instance.autoProcessing.RuleProcess();
    }

    #endregion
}

#endregion
