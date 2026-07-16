namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;  // (P4 ACTIVATED timing-builders) Hashtable — the AS-IS gate/coroutine delegate param type.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // (P4 ACTIVATED timing-builders) ActivateClass kind-class (return type / new).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// Headless mirror of the original <c>CardEffectFactory</c>. Method names match the original so ported
/// card bodies read 1:1. Each returns an <see cref="ICardEffect"/> the registrar lowers to a binding.
/// </summary>
public static partial class CardEffectFactory
{
    /// <summary>(B-5 uniform migration) Wrap a composable <see cref="IEffectBody"/> as a uniform
    /// <see cref="ActivatedEffect"/> so the activated-effect resolver applies the SHARED once-per-turn cap
    /// (<paramref name="maxCountPerTurn"/>) + optional yes/no gate (<paramref name="isOptional"/>) that the
    /// per-shape resolver cases could not express. For a plain activated skill (Option / [Main] / Security)
    /// <paramref name="timing"/> is <see cref="EffectTiming.None"/> and <paramref name="canUse"/> is null —
    /// the timing block the card registers the effect under carries the AS-IS timing; a broadcast trigger
    /// passes its own timing/gate. 1:1 mirror of the AS-IS <c>ActivateClass</c>
    /// (SetUpActivateClass(canActivate, coroutine, maxCountPerTurn, isOptional, description)).</summary>
    internal static ActivatedEffect AsUniformActivated(
        CardSource card,
        IEffectBody body,
        string description,
        bool isOptional = false,
        int? maxCountPerTurn = null,
        EffectTiming timing = EffectTiming.None,
        Func<CardEffectResolveContext, bool>? canUse = null,
        Func<bool>? canActivate = null) =>
        new ActivatedEffect(card, timing, canUse, canActivate, body, maxCountPerTurn, isOptional, description);

    // (P4 slice) ChangeSelfSAttackStaticEffect moved to CardEffectFactory/ChangeSAttack.cs (AS-IS 1:1)

    // (EFFECT-MODEL REBUILD / P4 slice: continuous DP) ChangeSelfDPStaticEffect (both int and Func<int> forms)
    // moved to the AS-IS-1:1 generic version in Script/CardEffectFactory/ChangeDP.cs (returns ChangeDPClass).

    // (P4 slice) ChangeDigivolutionCostStaticEffect moved to CardEffectFactory/ChangeDigivolutionCost.cs (AS-IS 1:1)

    // (P4 slice) CanNotDigivolveStaticSelfEffect + CanNotDigivolveStaticEffect moved to CardEffectFactory/CanNotDigivolve.cs (AS-IS 1:1)

    // (P4 slice) AddDigivolutionRequirementStaticEffect + AddSelfDigivolutionRequirementStaticEffect moved to CardEffectFactory/AddDigivolutionRequirement.cs (AS-IS 1:1)

    /// <summary>(PRIM-W5) <c>DrawCardsEffect</c> — the declarative form of the AS-IS
    /// <c>new DrawClass(owner, count, ...).Draw()</c> coroutine: the owner draws <paramref name="count"/>
    /// cards. Use this in place of the original draw coroutine.</summary>
    public static IActivatedCardEffect DrawCardsEffect(CardSource card, int count) =>
        new DrawEffect(card, count, $"Draw {count} card(s).");

    /// <summary>(G4) <c>DrawThenDiscardEffect</c> — atomic "draw <paramref name="drawAmount"/>, then discard
    /// <paramref name="trashAmount"/> from your hand" (AS-IS draw-then-discard coroutine). Wraps
    /// <see cref="CardEffectCommons.DrawAndDiscardCards"/> (draws+flushes before the discard candidate pool is
    /// built, so drawn cards are discardable). <paramref name="discardOptional"/> = "you may discard";
    /// <paramref name="discardUpTo"/> = "discard up to N" (min 1) vs exactly N. Resolved via the activation flow.</summary>
    public static IActivatedCardEffect DrawThenDiscardEffect(
        CardSource card, int drawAmount, int trashAmount, string description,
        Func<CardSource, bool>? canTrash = null, bool discardOptional = false, bool discardUpTo = false) =>
        new ActivatedDrawThenDiscardEffect(card, drawAmount, trashAmount, canTrash, discardOptional, discardUpTo, description);

    // (P4 KeyWord slice) PierceSelfEffect moved to KeyWordEffects/Pierce.cs (AS-IS 1:1)

    // (P4 KeyWord slice) BlockerSelfStaticEffect moved to KeyWordEffects/Blocker.cs (AS-IS 1:1)
    // (P4 KeyWord slice) JammingSelfStaticEffect moved to KeyWordEffects/Jamming.cs (AS-IS 1:1)
    // (P4 KeyWord slice) RebootSelfStaticEffect moved to KeyWordEffects/Reboot.cs (AS-IS 1:1)

    // (P4 KeyWord slice) AllianceSelfEffect moved to KeyWordEffects/Alliance.cs (AS-IS 1:1)

    // (P4 KeyWord slice) OverclockSelfEffect moved to KeyWordEffects/Overclock.cs (AS-IS 1:1)

    // (P4 KeyWord slice) VortexSelfEffect moved to KeyWordEffects/Vortex.cs (AS-IS 1:1)

    // (P4 KeyWord slice) RushSelfStaticEffect moved to KeyWordEffects/Rush.cs (AS-IS 1:1)

    // (P4 KeyWord slice) RetaliationSelfEffect moved to KeyWordEffects/Retaliation.cs (AS-IS 1:1)

    // (P4 KeyWord slice) RaidSelfEffect moved to KeyWordEffects/Raid.cs (AS-IS 1:1)

    // (P4 KeyWord slice) BarrierSelfEffect moved to KeyWordEffects/Barrier.cs (AS-IS 1:1)

    // (P4 KeyWord slice) CollisionSelfStaticEffect moved to KeyWordEffects/Collision.cs (AS-IS 1:1)

    // (P4 KeyWord slice) FortitudeSelfEffect moved to KeyWordEffects/Fortitude.cs (AS-IS 1:1)

    // (P4 KeyWord slice) EvadeSelfEffect moved to KeyWordEffects/Evade.cs (AS-IS 1:1)

    // (P4 KeyWord slice) SaveEffect moved to KeyWordEffects/Save.cs (AS-IS 1:1)

    // --- (PRIM-W3) keyword self-static grants -----------------------------------------------------------
    // (P4 KeyWord slice) BlitzSelfEffect moved to KeyWordEffects/Blitz.cs (AS-IS 1:1)

    // (P4 KeyWord slice) DecodeSelfEffect moved to KeyWordEffects/Decode.cs (AS-IS 1:1)

    // (P4 KeyWord slice) ProgressSelfStaticEffect moved to KeyWordEffects/Progress.cs (AS-IS 1:1)

    // (P4 KeyWord slice) PartitionSelfEffect moved to KeyWordEffects/Partition.cs (AS-IS 1:1)

    // (P4 KeyWord slice) IcecladSelfStaticEffect moved to KeyWordEffects/Iceclad.cs (AS-IS 1:1)

    // (P4 KeyWord slice) DecoySelfEffect moved to KeyWordEffects/Decoy.cs (AS-IS 1:1)

    // (P4 KeyWord slice) FragmentSelfEffect moved to KeyWordEffects/Fragment.cs (AS-IS 1:1)

    // (P4 KeyWord slice) ExecuteSelfEffect moved to KeyWordEffects/Execute.cs (AS-IS 1:1)

    // (P4 KeyWord slice) ScapegoatSelfEffect moved to KeyWordEffects/Scapegoat.cs (AS-IS 1:1)

    /// <summary>(C9) AS-IS linked-effect gate (Permanent.cs:1532 / ICardEffect.cs:403): an effect flagged
    /// <c>isLinkedEffect</c> is ACTIVE only while its source card is a LINK card of a battle-area permanent
    /// (<c>cardSource.IsLinked</c>), evaluated LIVE on every read — breaking the link stops the effect (the
    /// original has no removal event, only the two live guards). Wrapping the stored condition gives that
    /// gate to every consumer with no per-gate changes.</summary>
    internal static Func<bool>? LinkedGate(CardSource card, bool isLinkedEffect, Func<bool>? condition) =>
        !isLinkedEffect
            ? condition
            : () => card.IsLinked && (condition?.Invoke() ?? true);

    /// <summary>(FR-P2) Adapts a ported card's <c>Func&lt;Permanent,bool&gt; permanentCondition</c> into the
    /// player-scope predicate (evaluated against each candidate 1:1). Null → no predicate (whole scope).</summary>
    internal static Func<CardSource, bool>? ScopePred(Func<Permanent, bool>? permanentCondition) =>
        permanentCondition is null ? null : cs => permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner));

    // (P4 KeyWord slice) RushStaticEffect moved to KeyWordEffects/Rush.cs (AS-IS 1:1)
    // (P4 KeyWord slice) RebootStaticEffect moved to KeyWordEffects/Reboot.cs (AS-IS 1:1)

    // (P4 slice) CanNotAttackStaticEffect + CanNotAttackSelfStaticEffect moved to CardEffectFactory/CanNotAttack.cs (AS-IS 1:1)

    /// <summary>(joint-migration) Canonical CannotAttack — <paramref name="predicate"/> is the AS-IS joint
    /// <c>CanNotAttack(attacker, defender)</c> (defender may be null when the gate has no specific defender).</summary>
    public static ICardEffect CanNotAttackJointStaticEffect(Func<CardSource, CardSource?, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new JointRestrictionEffect(card, RestrictionHelpers.CannotAttackKey, predicate, condition);

    // (R3-F1 fold) AS-IS 1:1 port of DCGO CardEffectFactory.cs:63 Gain1MemoryTamerOpponentDigimonEffect — was the
    // mirror-invented TriggeredGainMemoryEffect; now the uniform ActivateClass (ActivateICardEffect, visible to the
    // AS-IS window collection). Substrate: IEnumerator->async Task, StartCoroutine->await; AS-IS `card.Owner`
    // (Player) -> mirror HeadlessPlayerId (memory extensions) / `new Player(context, owner)` for `.Enemy`.
    public static ICardEffect Gain1MemoryTamerOpponentDigimonEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());

        string EffectDiscription()
        {
            return "[Start of Your Main Phase] If your opponent has a Digimon, gain 1 memory.";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass))
            {
                if (CardEffectCommons.IsOwnerTurn(card))
                {
                    return true;
                }
            }

            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass))
            {
                if ((new Player(card.Context, card.Owner).Enemy?.GetBattleAreaDigimons().Count ?? 0) >= 1)
                {
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            await card.Owner.AddMemory(1, activateClass);
        }

        return activateClass;
    }

    /// <summary>(PRIM-W2 #9) AS-IS <c>Gain2MemoryOptionDelayEffect(card)</c> — a [Main] &lt;Delay&gt; option: TRASH
    /// this card's own battle-area permanent to activate, and ONLY IF trashed, gain 2 memory. 1:1 via
    /// <see cref="TrashSelfThenGainMemoryDelayEffect"/> (was wrongly an unconditional start-of-turn gain).</summary>
    public static ICardEffect Gain2MemoryOptionDelayEffect(CardSource card) =>
        new TrashSelfThenGainMemoryDelayEffect(card, amount: 2);

    // (P4 slice) CanNotBeBlockedStaticSelfEffect moved to CardEffectFactory/CanNotBeBlocked.cs (AS-IS 1:1)

    // (P4 slice) CantUnsuspendStaticEffect moved to CardEffectFactory/CanNotUnsuspend.cs (AS-IS 1:1)

    // (P4 slice) CanNotBeDestroyedBySkillStaticEffect moved to CardEffectFactory/CanNotBeDeletedByEffect.cs (AS-IS 1:1)

    // (P4 slice) ChangeSAttackStaticEffect moved to CardEffectFactory/ChangeSAttack.cs (AS-IS 1:1)

    // (P6 stage A retirement) mirror-invented `ReturnToLibraryBottomDigivolutionCardsClass` DELETED — 0
    // consumers remained (grep: no card, no Tfx, no test); the AS-IS-named factory lives in the partial layer.

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:599
    // ReplaceBottomSecurityWithFaceUpOptionMainEffect. Replaces the old mirror-invented
    // ReplaceBottomSecurityWithFaceUpEffect version. StartCoroutine->await (coroutine body lambda returns Task).
    public static ICardEffect ReplaceBottomSecurityWithFaceUpOptionMainEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Replace your bottom security card with this face-up card", CanUseCondition, card);
        activateClass.SetUpActivateClass(null, _ => ReplaceBottomSecurityWithFaceUpOptionEffect(card, activateClass), -1, false, EffectDescription());

        string EffectDescription()
        {
            return "[Main] Add your bottom security card to the hand. Then, place this card face up as the bottom security card.";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
        }

        return activateClass;
    }

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:645
    // ReplaceBottomSecurityWithFaceUpOptionEffect. Zero consumers on the mirror corpus (no real card / no Tfx
    // fixture references ReplaceBottomSecurityWithFaceUpOption{Main}Effect) — kept as the AS-IS-named entry
    // point (matching the file's method-name-fidelity contract) rather than deleted, so a future card port of
    // this exact text has somewhere to land. STOP (design item RD-P6C3-B1): the AS-IS body needs
    // ContinuousController.instance (StartCoroutine host) + CardObjectController.AddHandCards/AddSecurityCard
    // (the RD-P6C1-8/RD-P6C2-1 zone-move-statics gap, still unmodeled) + GManager.GetComponent<Effects>()
    // (the AS-IS UI/VFX-only Effects component, cluster-1 §4 precedent — never ported headless-side). The
    // equivalent live behavior (BT9_043's ReturnTopSecurityToHandThenUnsuspendSelfBody / IReduceSecurity /
    // IAddSecurity sink primitives) already exists for the currently-witnessed cards; this legacy path is not
    // on any live resolution route today.
    public static Task ReplaceBottomSecurityWithFaceUpOptionEffect(CardSource card, ActivateClass activateClass)
    {
        throw new NotSupportedException(
            "ReplaceBottomSecurityWithFaceUpOptionEffect (AS-IS CardEffectFactory.cs:645) needs " +
            "ContinuousController.instance / CardObjectController.AddHandCards+AddSecurityCard / " +
            "GManager.GetComponent<Effects>().CreateRecoveryEffect — unported substrate, design item RD-P6C3-B1.");
    }

    // (P4 KeyWord slice) TrainingEffect moved to KeyWordEffects/Training.cs (AS-IS 1:1)

    // (P4 KeyWord slice) MaterialSaveEffect moved to KeyWordEffects/MaterialSave.cs (AS-IS 1:1)

    // (P4 slice) ChangeSelfLinkMaxStaticEffect moved to CardEffectFactory/ChangeLinkMax.cs (AS-IS 1:1)

    // (P4 KeyWord slice) GrantedReduceLinkCostClass moved to KeyWordEffects/Link.cs (AS-IS 1:1)

    /// <summary>(PRIM-W3) <c>MindLink</c> — grants the MindLink keyword (Tamer↔Digimon link). Grant is live via
    /// HasKeyword; the tamer-as-Digimon behavior consumer migrates separately (preemptive seal).</summary>
    public static ICardEffect MindLinkSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.MindLink, isInheritedEffect, condition);

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:722 UseRequirements. Replaces the old
    // mirror-invented ContinuousSelfRestrictionEffect version. Verbatim (no substrate translation needed here).
    public static IgnoreColorConditionClass UseRequirements(CardSource card, Func<CardSource, bool> cardCondition)
    {
        IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
        ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
        ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);
        return ignoreColorConditionClass;

        bool PermanentCondition(Permanent permanent)
        {
            if (cardCondition == null)
                return false;

            return (permanent.IsDigimon || permanent.IsTamer)
                && cardCondition(permanent.TopCard);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.HasMatchConditionOwnersPermanent(card, PermanentCondition)
                || CardEffectCommons.HasMatchConditionOwnersBreedingPermanent(card, PermanentCondition);
        }

        bool CardCondition(CardSource cardSource)
        {
            return cardSource == card;
        }
    }

    // ===== PRIM-W4 (low-frequency tail) =================================================================

    // (P4 slice) CanNotBlockStaticSelfEffect + CanNotBlockStaticEffect moved to CardEffectFactory/CanNotBlock.cs (AS-IS 1:1)

    // (P4 slice) CanNotBeDestroyedStaticEffect moved to CardEffectFactory/CanNotBeDeleted.cs (AS-IS 1:1)

    // (P4 slice) ImmuneFromDPMinusStaticEffect moved to CardEffectFactory/ImmuneFromDPMinus.cs (AS-IS 1:1)

    /// <summary>(PRIM-P0 B.O.6 / #5) <c>CannotReduceCostClass</c> — the play/digivolution cost of this card (or,
    /// with <paramref name="permanentCondition"/>, the owner's matching cards) cannot be reduced. Registers a
    /// continuous CostReduction/Immune restriction honoured by ContinuousModifierGate.Resolve{Play,Digivolution}Cost.
    /// <paramref name="costKind"/> mirrors the AS-IS <c>targetPermanentsCondition</c>: <see cref="CostReductionScope.Digivolve"/>
    /// (AS-IS count&gt;=1, e.g. BT5_021 "opponent can't reduce DIGIVOLUTION costs") protects ONLY the digivolution
    /// cost — NOT the play cost; <see cref="CostReductionScope.Play"/> the reverse; <see cref="CostReductionScope.Both"/>
    /// (default, trivial predicate) protects either.</summary>
    public static ICardEffect CanNotReduceCostStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, CostReductionScope costKind = CostReductionScope.Both, bool scopeAnyPlayer = false)
    {
        string key = costKind switch
        {
            CostReductionScope.Play => ReplacementHelpers.ImmuneFromPlayCostReductionKey,
            CostReductionScope.Digivolve => ReplacementHelpers.ImmuneFromDigivolutionCostReductionKey,
            _ => ReplacementHelpers.ImmuneFromCostReductionKey,
        };
        // (#5) permanentCondition folds the AS-IS cardCondition (which card) AND playerCondition (the payer, via
        // the permanent's OwnerId). scopeAnyPlayer:true lets it reach the OPPONENT's cards — e.g. BT5_021
        // "your OPPONENT can't reduce DIGIVOLUTION costs" = permanentCondition p.OwnerId != card.Owner && IsDigimon,
        // costKind Digivolve, scopeAnyPlayer true.
        return permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, key, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, key, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), scopeAnyPlayer: scopeAnyPlayer);
    }

    /// <summary>(PRIM-P0 B.O.6; R3-W3c-4c B3 flip) <c>CannotAddSecurityClass</c> — <paramref name="scopePlayer"/>
    /// cannot add cards to their security (AS-IS Player.CanAddSecurity). NEW-MODEL: returns the AS-IS kind-class
    /// <see cref="CardEffects.CannotAddSecurityClass"/> (an <c>ICannotAddSecurityEffect</c>, no <c>ToBinding</c>)
    /// consulted by the AS-IS-literal live scan <c>Player.CanAddSecurity</c> (reached by the AddToSecurity / Recover
    /// mutation chokes). <paramref name="causingEffectPredicate"/> mirrors the AS-IS <c>CardEffectCondition</c> —
    /// the restriction fires only when the CAUSING effect's source matches (e.g. IsOpponentEffect); null = block
    /// every add (the AS-IS class requires a non-null CardEffectCondition, so null maps to <c>_ =&gt; true</c>).
    /// PlayerCondition = "the gaining player is <paramref name="scopePlayer"/>" (the old player-scope binding).</summary>
    public static ICardEffect CanNotAddSecurityStaticEffect(HeadlessPlayerId scopePlayer, bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null)
    {
        var effect = new CardEffects.CannotAddSecurityClass();
        effect.SetUpICardEffect("Can't add security", CanUseCondition, card);
        effect.SetUpCannotAddSecurityClass(PlayerCondition: PlayerCondition, CardEffectCondition: CardEffectCondition);
        effect.SetNotShowUI(true);
        if (isInheritedEffect)
        {
            effect.SetIsInheritedEffect(true);
        }

        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
        bool PlayerCondition(Player player) => player.PlayerId == scopePlayer;
        bool CardEffectCondition(ICardEffect cardEffect) =>
            causingEffectPredicate is null || (cardEffect?.EffectSourceCard is { } src && causingEffectPredicate(src));
    }

    /// <summary>(PRIM-P0 B.O.6; R3-W3c-4c B3 flip) <c>CannotAddMemoryClass</c> — <paramref name="scopePlayer"/>
    /// cannot gain memory (AS-IS Player.CanAddMemory). NEW-MODEL: returns the AS-IS kind-class
    /// <see cref="CardEffects.CannotAddMemoryClass"/> (an <c>ICannotAddMemoryEffect</c>, no <c>ToBinding</c>)
    /// consulted by the AS-IS-literal live scan <c>Player.CanAddMemory</c> (reached by the AddMemory mutation choke
    /// and the card-flow <c>Player.AddMemory</c>). <paramref name="causingEffectPredicate"/> mirrors the AS-IS
    /// <c>CardEffectCondition</c> (null = block every gain; maps to <c>_ =&gt; true</c>).</summary>
    public static ICardEffect CanNotAddMemoryStaticEffect(HeadlessPlayerId scopePlayer, bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null)
    {
        var effect = new CardEffects.CannotAddMemoryClass();
        effect.SetUpICardEffect("Can't add memory", CanUseCondition, card);
        effect.SetUpCannotAddMemoryClass(PlayerCondition: PlayerCondition, CardEffectCondition: CardEffectCondition);
        effect.SetNotShowUI(true);
        if (isInheritedEffect)
        {
            effect.SetIsInheritedEffect(true);
        }

        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
        bool PlayerCondition(Player player) => player.PlayerId == scopePlayer;
        bool CardEffectCondition(ICardEffect cardEffect) =>
            causingEffectPredicate is null || (cardEffect?.EffectSourceCard is { } src && causingEffectPredicate(src));
    }

    // (P4 KeyWord slice) AllianceStaticEffect moved to KeyWordEffects/Alliance.cs (AS-IS 1:1)

    // (PRIM-P0 AddSkillClass) player-scope keyword grants — "your Digimon (matching permanentCondition) gain
    // <keyword>". These are the headless port target for AS-IS AddSkillClass whose getEffects grants a
    // <keyword>SelfEffect to a live-matched set: the player-scope binding re-evaluates the set per query, so a
    // Digimon that enters AFTER the grant still gains the keyword (proven in PRIM-P0.AddSkillLiveSet.Tests).
    /// <summary>(PRIM-P0 AddSkill) grants Piercing to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect PiercingStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Piercing, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Blitz to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect BlitzStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Blitz, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Retaliation to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect RetaliationStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Retaliation, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    // (P4 KeyWord slice) ScapegoatStaticEffect moved to KeyWordEffects/Scapegoat.cs (AS-IS 1:1)

    /// <summary>(PRIM-P0 AddSkill) grants Decoy (deletion-replacement) to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect DecoyStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Decoy, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Barrier to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect BarrierStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Barrier, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) AS-IS AddSkillClass whose getEffects splices a TRIGGERED activated effect onto
    /// a live-matched set: <paramref name="nestedTriggeredEffect"/> (built to read the triggering card via
    /// TriggerEntityId and apply the per-card predicate + a nested activated resolution) fires for any event whose
    /// actor is <paramref name="scopePlayer"/> (the live set). The nested effect's ToBinding sets the granted
    /// timing.</summary>
    public static ICardEffect GrantTriggeredEffectToScopedSet(CardSource card, HeadlessPlayerId scopePlayer, ICardEffect nestedTriggeredEffect, bool scopeAnyPlayer = false) =>
        new PlayerScopeTriggerGrantEffect(card, scopePlayer, nestedTriggeredEffect, scopeAnyPlayer);

    // (P4 KeyWord slice) JammingStaticEffect moved to KeyWordEffects/Jamming.cs (AS-IS 1:1)
    // (P4 KeyWord slice) AscensionSelfEffect moved to KeyWordEffects/Ascension.cs (AS-IS 1:1)

    // (P4 slice) ChangeBaseDPGlobalEffect moved to CardEffectFactory/ChangeOriginDP.cs (AS-IS 1:1)

    // (P4 slice) InvertSAttackStaticEffect moved to CardEffectFactory/ChangeSAttack.cs (AS-IS 1:1)

    /// <summary>(b-remediation; R3-W3c-4c B5 flip) <c>ImmuneFromDeDigivolveStaticEffect</c> — AS-IS
    /// <c>ImmuneFromDeDigivolveClass</c> (<c>IImmuneFromDeDigivolveEffect</c>, consumed by
    /// <c>Permanent.ImmuneFromDeDigivolve()</c>): a continuous "cannot be de-digivolved" restriction on self (or,
    /// with <paramref name="permanentCondition"/>, the owner's matching Digimon). NEW-MODEL: returns the AS-IS
    /// kind-class <see cref="CardEffects.ImmuneFromDeDigivolveClass"/> (an <c>IImmuneFromDeDigivolveEffect</c>, no
    /// <c>ToBinding</c>) at <see cref="EffectTiming.None"/>; the LIVE consumer is the AS-IS-literal getter
    /// <c>Permanent.ImmuneFromDeDigivolve()</c> (scans every field permanent's <c>EffectList(None)</c> for a usable
    /// <c>IImmuneFromDeDigivolveEffect</c>) reached uniformly through <c>DeDigivolveHelpers.IsDeDigivolveImmune</c>
    /// (CardController / ActivatedEffects / the mutation sink). AS-IS construction idiom mirrored (EX8_043):
    /// SetUpICardEffect + SetUpImmuneFromDeDigivolveClass(PermanentCondition) — the class declares no ToBinding, so
    /// nothing registers the old <c>CannotBeDeDigivolvedKey</c> binding (registry production stops here).</summary>
    public static ICardEffect ImmuneFromDeDigivolveStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)
    {
        var effect = new CardEffects.ImmuneFromDeDigivolveClass();
        effect.SetUpICardEffect("Isn't affected by <De-Digivolve>", CanUseCondition, card);
        effect.SetUpImmuneFromDeDigivolveClass(PermanentCondition: PermanentCondition);
        effect.SetNotShowUI(true);
        if (isInheritedEffect)
        {
            effect.SetIsInheritedEffect(true);
        }

        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();

        bool PermanentCondition(Permanent permanent)
        {
            if (permanentCondition is null)
            {
                // Old self-scope form (ContinuousSelfRestrictionEffect): protects exactly THIS card's permanent —
                // the AS-IS self idiom `permanent == thisPermanent` (EX8_043, InstanceId equality).
                Permanent? own = ICardEffect.ResolvePermanentOfThisCard(card);
                return own is not null && permanent == own;
            }

            // Old player-scope form (ContinuousPlayerScopeRestrictionEffect): the owner's matching permanents.
            return permanent.OwnerId == card.Owner && permanentCondition(permanent);
        }
    }

    /// <summary>(d-remediation) <c>CanNotSelectBySkillStaticEffect</c> — AS-IS <c>ICanNotSelectBySkillEffect</c> /
    /// <c>Permanent.CanSelectBySkill</c>: the protected permanent(s) cannot be CHOSEN as a target by a skill
    /// effect (untargetability). <paramref name="permanentCondition"/> = AS-IS <c>CanNotSelectBySkill(candidate, …)</c>
    /// = which candidates are protected (self when null). <paramref name="causingEffectPredicate"/> = the skill-side
    /// gate ("cannot be chosen by your opponent's effects" ⇒ source-owner != owner). Registered candidate-scoped
    /// (scopeAnyPlayer so it can protect ANY player's matching cards); consumed by
    /// <see cref="HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect"/>.BuildRequest.</summary>
    /// <summary>(true-scan) AS-IS <c>SetUpCanNotSelectBySkillClass(CanNotSelectBySkill)</c>: <paramref name="predicate"/>
    /// is the joint AS-IS <c>CanNotSelectBySkill(candidate, skillSource)</c> — evaluated at select time by scanning
    /// EVERY field effect (SelectPermanentEffect), not baked into a scope.</summary>
    public static ICardEffect CanNotSelectBySkillStaticEffect(Func<CardSource, CardSource, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new CanNotSelectBySkillEffect(card, predicate, condition);

    // (P4 slice) CanNotBeRemovedStaticEffect moved to CardEffectFactory/CanNotBeRemoved.cs (AS-IS 1:1)

    /// <summary>(d-remediation) <c>CanNotMoveStaticEffect</c> — AS-IS <c>ICanNotMoveEffect</c> /
    /// <c>Permanent.CanMove</c>: the protected permanent(s) cannot MOVE (breeding→battle promotion). Registers a
    /// continuous restriction consulted by the legal-action dispatcher's move gate.</summary>
    /// <summary>(true-scan) AS-IS <c>SetUpCanNotMoveClass(CanNotMove)</c>: <paramref name="predicate"/> is the AS-IS
    /// <c>CanNotMove(candidate, causing)</c>, evaluated by scanning EVERY field effect in the move gate (causing is
    /// null there, AS-IS <c>CanNotMove(TopCard, null)</c>).</summary>
    public static ICardEffect CanNotMoveStaticEffect(Func<CardSource, CardSource?, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new CanNotMoveEffect(card, predicate, condition);

    /// <summary>(d-remediation) <c>DontHaveDPStaticEffect</c> — AS-IS <c>IDontHaveDPEffect</c> /
    /// <c>Permanent.HasDP==false</c>: the protected permanent(s) are treated as having NO DP — AS-IS
    /// <c>Permanent.DP</c> then returns -1, overriding the base DP and every DP modifier.</summary>
    public static ICardEffect DontHaveDPStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool scopeAnyPlayer = false)
    {
        // (R3-W3a narrow flip) NEW-MODEL: returns the AS-IS kind-class DontHaveDPClass (IDontHaveDPEffect)
        // instead of the old-model registry restriction. Judgment basis: the LIVE consumer is the R1-a interface
        // scan Permanent.HasDP (Permanent.cs:127-147 — `cardEffect is IDontHaveDPEffect` over every field
        // permanent's EffectList(None)); the old DontHaveDpKey registry binding has ZERO readers (dead —
        // superseded by the R1 rehousing), so the old-model form was inert. AS-IS construction idiom mirrored:
        // BT17_014 (SetUpICardEffect "Don't have DP" + SetUpDontHaveDPClass(PermanentCondition) + SetNotShowUI).
        // DontHaveDPClass declares no ToBinding, so LegacyBindingBridge.TryToBinding returns false and the
        // enter-play registrar (CardEffectRegistrar.cs:234) registers NOTHING — registry production stops here;
        // availability is the live EffectList scan alone.
        var effect = new CardEffects.DontHaveDPClass();
        effect.SetUpICardEffect("Don't have DP", CanUseCondition, card);
        effect.SetUpDontHaveDPClass(PermanentCondition: PermanentCondition);
        effect.SetNotShowUI(true);
        if (isInheritedEffect)
        {
            effect.SetIsInheritedEffect(true);
        }

        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();

        bool PermanentCondition(Permanent permanent)
        {
            if (permanentCondition is null)
            {
                // Old self-scope form (ContinuousSelfRestrictionEffect): strips DP from exactly THIS card's
                // permanent — the AS-IS self idiom `permanent == selectedPermanent` (BT17_014, InstanceId equality).
                Permanent? own = ICardEffect.ResolvePermanentOfThisCard(card);
                return own is not null && permanent == own;
            }

            // Old player-scope form (ContinuousPlayerScopeRestrictionEffect): the owner's matching permanents;
            // scopeAnyPlayer widens the scope to every player's (the old scopeAnyPlayer knob folded into the
            // predicate — the new-model HasDP scan itself is board-wide, AS-IS Permanent.cs:128).
            return (scopeAnyPlayer || permanent.OwnerId == card.Owner) && permanentCondition(permanent);
        }
    }

    /// <summary>(d-remediation, R3-W3c-4b B1 flip) <c>DontBattleSecurityDigimonStaticEffect</c> — AS-IS
    /// <c>DontBattleSecurityDigimonClass</c>: intrinsic "Ignore Battle" marker (EX4_013). Returns the new-model
    /// kind-class <see cref="CardEffects.DontBattleSecurityDigimonClass"/> (an
    /// <c>IDontBattleSecurityDigimonEffect</c>, no <c>ToBinding</c>) consumed by the security resolver's
    /// AS-IS-literal interface scan (CardController.cs:4136-4169). Returned by the card at
    /// <see cref="EffectTiming.None"/>; the resolver consults it when the card is revealed as a security Digimon
    /// and skips the attacker battle. <paramref name="cardSourceCondition"/> = AS-IS CardSourceCondition
    /// (usually <c>cs == card</c>); <paramref name="condition"/> = the effect's own CanUse gate (AS-IS
    /// <c>CanUseIgnoreBattle</c>, abstracted). The only producer is TfxIgnoreBattle (no real-card producers).</summary>
    public static ICardEffect DontBattleSecurityDigimonStaticEffect(Func<CardSource, bool> cardSourceCondition, CardSource card, Func<bool>? condition)
    {
        var effect = new CardEffects.DontBattleSecurityDigimonClass();
        effect.SetUpICardEffect("Ignore Battle", CanUseCondition, card);
        effect.SetUpDontBattleSecurityDigimonClass(CardSourceCondition: cardSourceCondition);
        effect.SetIsSecurityEffect(true);
        effect.SetNotShowUI(true);
        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
    }

    /// <summary>(d-remediation) <c>CannotIgnoreDigivolutionConditionStaticEffect</c> — AS-IS
    /// <c>ICannotIgnoreDigivolutionConditionEffect</c> / <c>Player.CanIgnoreDigivolutionRequirement</c>: while this
    /// is active NO player may ignore digivolution requirements (BT8_059 "Players can't ignore digivolution
    /// requirements") — it negates the ignore-level/ignore-colour grants. Registered board-wide (scopeAnyPlayer,
    /// so the digivolve target is in scope); honoured by DigivolveAction's evolution-condition gate. Default
    /// <paramref name="scopeAnyPlayer"/>:true mirrors the AS-IS global scan.</summary>
    /// <summary>(true-scan, R3-W3c-4b D flip) AS-IS <c>CannotIgnoreDigivolutionConditionClass</c>:
    /// <paramref name="predicate"/> is the AS-IS joint <c>cannotIgnoreDigivolutionCondition(digivolvingCard,
    /// target)</c>. Returns the new-model kind-class <see cref="CardEffects.CannotIgnoreDigivolutionConditionClass"/>
    /// (an <c>ICannotIgnoreDigivolutionConditionEffect</c>, no <c>ToBinding</c>) consumed by the live getter
    /// <see cref="Player.CanIgnoreDigivolutionRequirement"/> (scanned by DigivolveAction); no registry binding. The
    /// kind-class exposes the AS-IS 3-arg predicate <c>(player, targetPermanent, cardSource)</c>; the joint maps as
    /// <c>predicate(cardSource /*digivolvingCard*/, targetPermanent.TopCard /*target*/)</c>. BT8_059 uses
    /// <c>(_, _) =&gt; true</c> (global lock).</summary>
    public static ICardEffect CannotIgnoreDigivolutionConditionStaticEffect(Func<CardSource, CardSource, bool> predicate, CardSource card, Func<bool>? condition = null)
    {
        var effect = new CardEffects.CannotIgnoreDigivolutionConditionClass();
        effect.SetUpICardEffect("Players can't ignore digivolution requirements", CanUseCondition, card);
        effect.SetUpCannotIgnoreDigivolutionConditionClass(
            (player, targetPermanent, cardSource) => predicate(cardSource, targetPermanent.TopCard));
        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
    }

    /// <summary>(d-remediation, R3-W3c-4b B2 flip) <c>ChangeEndTurnMinMemoryStaticEffect</c> — AS-IS
    /// <c>ChangeEndTurnMinMemoryClass</c> / <c>AutoProcessing.TurnEndMinMemory</c>: SETS the memory the opponent
    /// must reach for the turn to auto-end (default 1) to <paramref name="minMemory"/> (BT14_081/BT17_069 set it
    /// to 3, AS-IS <c>SetUpChangeEndTurnMinMemoryClass(minMemory =&gt; 3)</c>). Returns the new-model kind-class
    /// <see cref="CardEffects.ChangeEndTurnMinMemoryClass"/> (an <c>IChangeEndTurnMinMemoryEffect</c>, no
    /// <c>ToBinding</c>) consumed by the AS-IS-literal live scan in <see cref="Headless.Runtime.HeadlessMainPhaseFlow"/>;
    /// no registry binding.</summary>
    public static ICardEffect ChangeEndTurnMinMemoryStaticEffect(int minMemory, bool isInheritedEffect, CardSource card, Func<bool>? condition)
    {
        var effect = new CardEffects.ChangeEndTurnMinMemoryClass();
        effect.SetUpICardEffect("The turn end condition min-memory", CanUseCondition, card);
        effect.SetUpChangeEndTurnMinMemoryClass(_ => minMemory);
        if (isInheritedEffect)
        {
            effect.SetIsInheritedEffect(true);
        }

        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
    }

    /// <summary>(d-remediation) <c>ChangeBaseCardNameStaticEffect</c> — AS-IS <c>ChangeBaseCardNameClass</c>: SETS
    /// the target's original card name to <paramref name="newName"/> (BT14_097 "original name is [Sukamon]"),
    /// REPLACING the printed name for all name matching (<c>CardSource.CardNames</c> / EqualsCardName). Grant on
    /// the target permanent with a duration for a temporary change.</summary>
    public static ICardEffect ChangeBaseCardNameStaticEffect(string newName, CardSource card, Func<bool>? condition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var effect = new CardEffects.ChangeBaseCardNameClass();
        effect.SetUpICardEffect("Change base card name", CanUseCondition, card);
        effect.SetUpChangeBaseCardNamesClass((_, _) => new List<string> { newName });
        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
    }

    // (P4 KeyWord slice) CollisionStaticEffect moved to KeyWordEffects/Collision.cs (AS-IS 1:1)

    // (P4 slice) VortexCanAttackPlayersStaticEffect moved to CardEffectFactory/VortexCanAttackPlayers.cs (AS-IS 1:1)

    // (P4 slice) ChangeLinkMaxStaticEffect moved to CardEffectFactory/ChangeLinkMax.cs (AS-IS 1:1)

    // (P4 slice) TreatAsDigimonStaticEffect moved to CardEffectFactory/TreatAsDigimon.cs (AS-IS 1:1)

    /// <summary>(PRIM-W4) <c>Gain1MemoryTamerOwnerDigimonConditionalEffect</c> — "[Start of Your Main Phase] if
    /// you have a matching Digimon, gain 1 memory." The per-permanent predicate is captured in
    /// <paramref name="condition"/> at porting time. Registers at <see cref="EffectTiming.OnStartMainPhase"/>
    /// (AS-IS "[Start of Your Main Phase]" — both known cards BT23_081/BT23_083 return it under that timing),
    /// NOT OnStartTurn.</summary>
    public static ICardEffect Gain1MemoryTamerOwnerDigimonConditionalEffect(string effectDescription, Func<Permanent, bool>? permanentCondition, Func<bool>? condition, CardSource card)
    {
        // (R3-F1 fold) AS-IS 1:1 port of DCGO CardEffectFactory.cs:115 Gain1MemoryTamerOwnerDigimonConditionalEffect —
        // was the mirror-invented TriggeredGainMemoryEffect; now the uniform ActivateClass. Substrate:
        // IEnumerator->async Task, StartCoroutine->await; `card.Owner.AddMemory/CanAddMemory/GetBattleAreaDigimons`
        // = HeadlessPlayerId extensions. AS-IS passes non-null `condition`/`permamentCondition`; the mirror
        // signature made both nullable, so the nullable guards below are the retained pre-existing mirror ADAPTATION
        // (a null predicate = "no extra gate"). The empty-description default is likewise the retained mirror guard.
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());

        string EffectDiscription() =>
            string.IsNullOrWhiteSpace(effectDescription) ? "[Start of Your Main Phase] Gain 1 memory." : effectDescription;

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass)
                && card.Owner.CanAddMemory(activateClass)
                && (condition is null || condition())
                && (permanentCondition is null || card.Owner.GetBattleAreaDigimons().Any(permanentCondition));
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            await card.Owner.AddMemory(1, activateClass);
        }

        return activateClass;
    }

    /// <summary>(FR2) Whether <paramref name="card"/>'s owner controls at least one permanent in
    /// <paramref name="searchZones"/> (default: battle area) satisfying <paramref name="predicate"/> (evaluated
    /// as a <see cref="Permanent"/> 1:1).</summary>
    internal static bool OwnerControlsMatchingDigimon(CardSource card, Func<Permanent, bool> predicate, params ChoiceZone[] searchZones)
    {
        if (card.Context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        ChoiceZone[] zonesToSearch = searchZones is { Length: > 0 } ? searchZones : new[] { ChoiceZone.BattleArea };
        foreach (ChoiceZone zone in zonesToSearch)
        {
            foreach (HeadlessEntityId id in zones.GetCards(card.Owner, zone))
            {
                if (predicate(new Permanent(card.Context, id, card.Owner)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>CardEffectFactory.EoTLose3Memory(card)</c> (CardEffectFactory.cs:1467) — the "at end of
    /// turn lose 3 memory" reversal (BT1_021/BT1_040), a bare <c>ActivateClass</c> "Memory -3" whose CanUse/CanActivate
    /// are always-true and whose body is <c>card.Owner.AddMemory(-3)</c>. It carries no timing itself — the OnEndTurn
    /// firing comes from the closure key when it is stored via <c>AddEffectToPlayer(UntilEachTurnEnd, …, OnEndTurn)</c>
    /// (or <c>UntilEachTurnEndEffects.Add(GetCardEffect)</c>) and surfaced by the flipped window's
    /// <c>player.EffectList(OnEndTurn)</c> scan. R3-C2b-2 fold: replaces the mirror-invented old-model
    /// <c>TriggeredGainMemoryEffect</c> with the AS-IS 1:1 ActivateClass (new-model, no ToBinding). Substrate:
    /// IEnumerator→async Task, StartCoroutine→await; AS-IS <c>card.Owner</c> (Player) → mirror HeadlessPlayerId
    /// AddMemory extension.</summary>
    public static ICardEffect EoTLose3Memory(CardSource card)
    {
        ActivateClass activateClass1 = new ActivateClass();
        activateClass1.SetUpICardEffect("Memory -3", CanUseCondition1, card);
        activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
        return activateClass1;

        string EffectDiscription1()
        {
            return "Lose 3 memory.";
        }

        bool CanUseCondition1(Hashtable hashtable)
        {
            return true;
        }

        bool CanActivateCondition1(Hashtable hashtable)
        {
            return true;
        }

        async Task ActivateCoroutine1(Hashtable _hashtable1)
        {
            await card.Owner.AddMemory(-3, activateClass1);
        }
    }

    // (P4 slice) CantSuspendStaticEffect moved to CardEffectFactory/CanNotSuspend.cs (AS-IS 1:1)

    // (P4 slice) CannotReturnToHandStaticEffect moved to CardEffectFactory/CanNotReturnToHand.cs (AS-IS 1:1)

    // (P4 slice) CannotReturnToDeckStaticEffect moved to CardEffectFactory/CanNoReturnToDeck.cs (AS-IS 1:1)

    // (P4 slice) CanNotBeDestroyedByBattleStaticEffect moved to CardEffectFactory/CanNotBeDeletedByBattle.cs (AS-IS 1:1)

    // (P4 slice) CanNotBeTrashedBySkillStaticEffect moved to CardEffectFactory/CanNotBeTrashedByEffect.cs (AS-IS 1:1)

    /// <summary>(PRIM-W4; R3-W3c B6 flip) <c>ImmuneStackTrashingClass</c> — alias of
    /// <see cref="CanNotBeTrashedBySkillStaticEffect"/>. Now returns the AS-IS kind-class
    /// <see cref="CardEffects.ImmuneStackTrashingClass"/> (no ToBinding → registers no legacy binding), served by
    /// the live getter <c>Permanent.ImmuneFromStackTrashing(ICardEffect)</c>. Unconditional (null predicates).</summary>
    public static ICardEffect ImmuneStackTrashingClass(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        CanNotBeTrashedBySkillStaticEffect(null!, null!, isInheritedEffect, card, condition!, "Isn't affected by trashing any stacked card");

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:622
    // ReplaceTopSecurityWithFaceUpOptionMainEffect. Replaces the old mirror-invented version.
    public static ICardEffect ReplaceTopSecurityWithFaceUpOptionMainEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Replace your top security card with this face-up card", CanUseCondition, card);
        activateClass.SetUpActivateClass(null, _ => ReplaceTopSecurityWithFaceUpOptionEffect(card, activateClass), -1, false, EffectDescription());

        string EffectDescription()
        {
            return "[Main] Add your top security card to the hand. Then, place this card face up as the top security card.";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
        }

        return activateClass;
    }

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:684
    // ReplaceTopSecurityWithFaceUpOptionEffect (ADDED — absent on mirror). Zero consumers on the mirror corpus
    // (no real card / no Tfx fixture) — same STOP as ReplaceBottomSecurityWithFaceUpOptionEffect above, design
    // item RD-P6C3-B1 (ContinuousController.instance / CardObjectController.AddHandCards+AddSecurityCard /
    // GManager.GetComponent<Effects>() — unported substrate).
    public static Task ReplaceTopSecurityWithFaceUpOptionEffect(CardSource card, ActivateClass activateClass)
    {
        throw new NotSupportedException(
            "ReplaceTopSecurityWithFaceUpOptionEffect (AS-IS CardEffectFactory.cs:684) needs " +
            "ContinuousController.instance / CardObjectController.AddHandCards+AddSecurityCard / " +
            "GManager.GetComponent<Effects>().CreateRecoveryEffect — unported substrate, design item RD-P6C3-B1.");
    }

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:196
    // PlayMindLinkTamerFromDigivolutionCards. Replaces the old mirror-invented ActivatedPlayFromUnderEffect version.
    // Coroutine->async Task; StartCoroutine->await; card.PermanentOfThisCard()->ICardEffect.ResolvePermanentOfThisCard(card).
    public static ICardEffect PlayMindLinkTamerFromDigivolutionCards(CardSource card, string cardName, string effectDescription)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect($"Play 1 [{cardName}] from this Digimon's digivolution cards", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, effectDescription);
        activateClass.SetIsInheritedEffect(true);

        bool CanSelectCardCondition(CardSource cardSource)
        {
            return cardSource.EqualsCardName(cardName)
                && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleArea(card);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleArea(card)
                && ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectCardCondition) >= 1;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleArea(card))
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                if (selectedPermanent != null)
                {
                    if (selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        int maxCount = 1;

                        List<CardSource> selectedCards = new List<CardSource>();

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                                    canTargetCondition: CanSelectCardCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    canNoSelect: () => true,
                                    selectCardCoroutine: SelectCardCoroutine,
                                    afterSelectCardCoroutine: null,
                                    message: "Select 1 digivolution card to play.",
                                    maxCount: maxCount,
                                    canEndNotMax: false,
                                    isShowOpponent: true,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.Custom,
                                    customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                                    canLookReverseCard: true,
                                    selectPlayer: card.Owner,
                                    cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 digivolution card to play.",
                            "The opponent is selecting 1 digivolution card to play.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectCardEffect.Activate();

                        async Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);

                            await Task.CompletedTask;
                        }

                        await CardEffectCommons.PlayPermanentCards(
                            cardSources: selectedCards,
                            activateClass: activateClass,
                            payCost: false,
                            isTapped: false,
                            root: SelectCardEffect.Root.DigivolutionCards,
                            activateETB: true);
                    }
                }
            }
        }

        return activateClass;
    }

    // (P4 slice) CanNotBeAttackedSelfStaticEffect moved to CardEffectFactory/CanNotBeAttacked.cs (AS-IS 1:1)

    // (P6 stage A retirement) mirror-invented `RevealLibraryClass` DELETED — 0 consumers remained
    // (grep: no card, no Tfx, no test).

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:551 ActivateMainOptionSecurityEffect
    // (ADDED — absent on mirror). Func<ICardEffect,IEnumerator>->Func<ICardEffect,Task>; IEnumerator->async Task;
    // StartCoroutine->await.
    public static ICardEffect ActivateMainOptionSecurityEffect(CardSource card, string effectName, string effectDiscription = "", Func<ICardEffect, Task> afterMainEffect = null)
    {
        ActivateClass mainActivateClass = CardEffectCommons.OptionMainEffect(card);

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect(EffectName(), CanUseCondition, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
        activateClass.SetIsSecurityEffect(true);

        string EffectName()
        {
            if (!string.IsNullOrEmpty(effectName)) return effectName;
            if (mainActivateClass != null) return mainActivateClass.EffectName;
            return "";
        }

        string EffectDiscription()
        {
            if (!string.IsNullOrEmpty(effectDiscription)) return effectDiscription;
            if (mainActivateClass != null) return mainActivateClass.EffectDiscription.Replace("[Main]", "[Security]");
            return "";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerSecurityEffect(CardEffectCommons.OptionMainCheckHashtable(card), card);
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            if (mainActivateClass != null)
            {
                await mainActivateClass.Activate(CardEffectCommons.OptionMainCheckHashtable(card));
            }

            if (afterMainEffect != null)
            {
                await afterMainEffect(activateClass);
            }
        }

        return activateClass;
    }

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:512 PlaceSelfDelayOptionSecurityEffect.
    // Replaces the old mirror-invented PlayThisCardToBattleEffect version. Coroutine->async Task; StartCoroutine->await.
    public static ICardEffect PlaceSelfDelayOptionSecurityEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Place this card in battle area", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
        activateClass.SetIsSecurityEffect(true);

        string EffectDiscription()
        {
            return "[Security] Place this card in its owner's battle area.";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: card, payCost: false, cardEffect: activateClass, isPlayOption: true))
            {
                return true;
            }

            return false;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            await CardEffectCommons.PlaceDelayOptionCards(card: card, cardEffect: activateClass);
        }

        return activateClass;
    }

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:285
    // PlaySelfDigimonAfterBattleSecurityEffect. Replaces the old mirror-invented PlaySelfAtEndOfBattleSecurityEffect
    // version (and its DeleteTimingString helper). Coroutine->async Task; StartCoroutine->await;
    // card.PermanentOfThisCard()->ICardEffect.ResolvePermanentOfThisCard(card);
    // CanNotBeAffected(effect)->CanNotBeAffected(effect.EffectSourceCard?.InstanceId); PlaySE/WaitForSeconds = UI (stripped).
    // Zero consumers on the mirror corpus (no real card / no Tfx fixture references
    // PlaySelfDigimonAfterBattleSecurityEffect). STOP (design item RD-P6C3-B2) at ActivateCoroutine: the AS-IS
    // body nests a SECOND delayed grant (card.Owner.UntilEndBattleEffects.Add(GetCardEffect1) — the AS-IS
    // Player mutable EffectTiming.OnEndBattle bucket, part of the unlanded player-grant store,
    // P6A-PLAYER-EFFECTLIST) and, conditionally, a THIRD (playedDigimon.UntilOpponentTurnEndEffects.Add —
    // same gap on Permanent) plus a DestroyPermanentsClass batch-delete helper with no mirror. All three are
    // out of this build-fix pass's scope; kept as the AS-IS-named entry point (method-name-fidelity contract)
    // for a future card port instead of deleted.
    public static ICardEffect PlaySelfDigimonAfterBattleSecurityEffect(CardSource card, EffectDuration deleteDigimon = EffectDuration.UntilEndBattle)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Play this card at the end of the battle", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
        activateClass.SetIsSecurityEffect(true);

        string EffectDiscription()
        {
            return "[Security] At the end of the battle, play this card without paying its memory cost.";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnExecutingArea(card);
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            throw new NotSupportedException(
                "PlaySelfDigimonAfterBattleSecurityEffect (AS-IS CardEffectFactory.cs:285) needs the AS-IS " +
                "Player.UntilEndBattleEffects / Permanent.UntilOpponentTurnEndEffects mutable grant buckets " +
                "(unlanded player/permanent-grant store, design item P6A-PLAYER-EFFECTLIST) + DestroyPermanentsClass " +
                "— unported substrate, design item RD-P6C3-B2.");
        }

        return activateClass;
    }

    /// <summary>(PRIM-W2) Original: <c>LinkEffect(card, condition)</c> — the &lt;Link&gt; activation: attach
    /// this card to a chosen own Digimon, paying the link cost (read from the card's <c>linkCost</c> data).</summary>
    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:1497 PlaceToSecurityEffect
    // (ADDED — absent on mirror). IEnumerator->async Task; StartCoroutine->await.
    public static OptionResolutionClass PlaceToSecurityEffect(CardSource card, bool toTop, bool isFaceUp = false, Func<bool> Condition = null)
    {
        if (card == null) return null;
        string location = toTop ? "top" : "bottom";
        string facing = isFaceUp ? "face up" : "face down";
        string effectName = $"Place the used Option card on {location} of security {facing}.";
        OptionResolutionClass placeToSecurityEffect = new();
        placeToSecurityEffect.SetUpICardEffect(effectName, CanUseCondition, card);
        placeToSecurityEffect.SetUpOptionResolutionClass(ResolutionCoroutine, CanResolveCondition);
        return placeToSecurityEffect;

        bool CanUseCondition(Hashtable hashtable)
        {
            return Condition == null || Condition();
        }

        bool CanResolveCondition(CardSource optionCard) =>
            new Player(optionCard.Context, optionCard.Owner).CanAddSecurity(placeToSecurityEffect.EffectSourceCard?.InstanceId);

        // Zero consumers on the mirror corpus (the new-model PlayCardsBridge.cs already reimplements this
        // AS-IS hook resolution directly against the sink — see its own citing comments at :111/:159/:161).
        // STOP (design item RD-P6C3-B1, same CardObjectController.AddSecurityCard gap as
        // ReplaceTopSecurityWithFaceUpOptionEffect above) — kept as the AS-IS-named entry point.
        Task ResolutionCoroutine(CardSource optionCard)
        {
            throw new NotSupportedException(
                "PlaceToSecurityEffect (AS-IS CardEffectFactory.cs:1497) needs CardObjectController.AddSecurityCard " +
                "— unported substrate, design item RD-P6C3-B1.");
        }
    }

    #region Add detail for Display
    // (P4 ACTIVATED timing-builders) 1:1 mirror of AS-IS CardEffectFactory.cs:1523 AddDetailClass. Replaces the
    // old mirror-invented DisplayDetailEffect version. Returns the ported AddDetailClass kind-class
    // (CardEffects/AddDetailClass.cs). NOTE: canUseCondition is Func<Hashtable,bool> (AS-IS) — the old version
    // took Func<bool>?; any caller passing Func<bool> is CARD-MIGRATION-NEEDED (currently zero card-file callers).
    public static AddDetailClass AddDetailClass(Func<Hashtable, bool> canUseCondition, Func<Permanent, bool> permanentCondition, string detail, bool triggerEffect, CardSource card)
    {
        AddDetailClass addDetailClass = new();
        addDetailClass.SetUpICardEffect("Added Detail", canUseCondition, card);
        addDetailClass.SetUpAddDetailClass(permanentCondition, detail, triggerEffect);
        return addDetailClass;
    }
    #endregion

    // (P4 KeyWord slice) ArtsDigivolveEffect moved to KeyWordEffects/ArtsDigivolve.cs (AS-IS 1:1)

    // (P4 slice) AddAppfuseMethodByCondition + AddAppfuseMethodByName moved to CardEffectFactory/AddAppfusionMethod.cs (AS-IS 1:1)

    // (P4 slice) AddSelfLinkConditionStaticEffect moved to CardEffectFactory/AddLinkRequirement.cs (AS-IS 1:1)

    // (P4 KeyWord slice) LinkEffect moved to KeyWordEffects/Link.cs (AS-IS 1:1)

    // (P4 KeyWord slice) BlockerStaticEffect moved to KeyWordEffects/Blocker.cs (AS-IS 1:1)

    // (R3-F1b fold) AS-IS 1:1 port of DCGO CardEffectFactory.cs:11 SetMemoryTo3TamerEffect — was the
    // mirror-invented TriggeredSetMemoryEffect; now the uniform ActivateClass (ActivateICardEffect, visible to the
    // AS-IS window collection). Substrate: IEnumerator->async Task, StartCoroutine->await; AS-IS `card.Owner`
    // (Player) -> mirror HeadlessPlayerId (CanAddMemory/SetFixedMemory extensions) / `new Player(context, owner)`
    // for `.MemoryForPlayer` (BT1_054 idiom). EffectDiscription byte-identical to AS-IS.
    public static ICardEffect SetMemoryTo3TamerEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Set Memory to 3", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());

        string EffectDiscription()
        {
            return "[Start of Your Turn] If you have 2 or less memory, set your memory to 3.";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass))
            {
                if (CardEffectCommons.IsOwnerTurn(card))
                {
                    return true;
                }
            }

            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass))
            {
                if (new Player(card.Context, card.Owner).MemoryForPlayer <= 2)
                {
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            await card.Owner.SetFixedMemory(3, activateClass);
        }

        return activateClass;
    }

    // (P4 KeyWord slice) ArmorPurgeEffect moved to KeyWordEffects/ArmorPurge.cs (AS-IS 1:1)

    // (P4 slice) CanNotAttackSelfStaticEffect moved to CardEffectFactory/CanNotAttack.cs (AS-IS 1:1)

    // (EFFECT-MODEL REBUILD / P4 slice: continuous DP) ChangeDPStaticEffect moved to the AS-IS-1:1 generic
    // version in Script/CardEffectFactory/ChangeDP.cs (returns ChangeDPClass; effectName is a required arg there,
    // per AS-IS — callers passing 5 args need the effectName argument, handled in the card-migration step).

    // (R3-C2b-2) AddMemoryTriggerEffect DELETED — the invented old-model memory-trigger factory (produced
    // TriggeredMemoryEffect, a registry-lowering effect) is retired. Every former caller is now the AS-IS 1:1
    // new-model inline ActivateClass "gain/lose N memory" recipe (card.Owner.AddMemory(N, activateClass) + the
    // AS-IS Hashtable CanUse gate) — see BT2_073 / BT1_041 for the pattern the fixtures TfxOnDeleteGainMemory /
    // TfxOnPlayGainMemory now follow.

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:148 PlaySelfTamerSecurityEffect.
    // Replaces the old mirror-invented PlayThisCardToBattleEffect version. Coroutine->async Task; StartCoroutine->await.
    public static ICardEffect PlaySelfTamerSecurityEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Play this card", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
        activateClass.SetIsSecurityEffect(true);

        string EffectDiscription()
        {
            return "[Security] Play this card without paying its memory cost.";
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            // AS-IS `card.Owner.ExecutingCards.Contains(card)` — the mirror re-expresses "is being resolved as
            // an Option" as the live Execution-zone membership predicate (CardEffectCommons.IsExistOnExecutingArea,
            // the established idiom this same file's PlaySelfDigimonAfterBattleSecurityEffect.CanActivateCondition
            // already uses), not an AS-IS Player mutable-list read.
            if (CardEffectCommons.IsExistOnExecutingArea(card))
            {
                if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: card, payCost: false, cardEffect: activateClass))
                {
                    return true;
                }
            }

            return false;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            await CardEffectCommons.PlayPermanentCards(
                    cardSources: new List<CardSource>() { card },
                    activateClass: activateClass,
                    payCost: false,
                    isTapped: false,
                    root: SelectCardEffect.Root.Execution,
                    activateETB: true);
        }

        return activateClass;
    }

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching permanents and delete them"
    /// effect (Option [Main] delete skill, e.g. ST1_16 / ST1_15).</summary>
    public static ICardEffect SelectAndDestroyEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canTarget,
        int maxCount,
        bool canEndNotMax,
        string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Destroy, description), description);

    /// <summary>(PRIM-P0-flow) An activated "choose one of the following modes" menu (AS-IS UserSelectionManager
    /// SetBool/IntSelection). Each mode is a labeled branch effect; a mode with an availability predicate that
    /// returns false is omitted. The selected branch resolves through the same activation flow / sink.</summary>
    public static ICardEffect SelectModeEffect(CardSource card, string description, params ModeChoiceEffect.Mode[] modes) =>
        new ModeChoiceEffect(card, description, modes);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>new SuspendPermanentsClass(perms, ..).Tap()</c>
    /// coroutine: select up to <paramref name="maxCount"/> matching permanents and suspend them.</summary>
    public static ICardEffect SelectAndSuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Tap, description), description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS unsuspend coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and unsuspend them.</summary>
    public static ICardEffect SelectAndUnsuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.UnTap, description), description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS bounce coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and return them to hand.</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Bounce, description), description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> matching permanents and return
    /// them to the owner's deck (top or bottom). AS-IS SelectPermanentEffect.Mode PutLibraryTop/Bottom.</summary>
    public static ICardEffect SelectAndReturnToDeckEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax,
            toTop ? SelectPermanentEffect.Mode.PutLibraryTop : SelectPermanentEffect.Mode.PutLibraryBottom, description), description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> matching permanents and place
    /// them into the owner's security (top or bottom). AS-IS SelectPermanentEffect.Mode PutSecurityTop/Bottom.</summary>
    public static ICardEffect SelectAndPutSecurityEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax,
            toTop ? SelectPermanentEffect.Mode.PutSecurityTop : SelectPermanentEffect.Mode.PutSecurityBottom, description), description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.PlayPermanentCards(.., root)</c>
    /// coroutine: select up to <paramref name="maxCount"/> of the owner's cards in <paramref name="fromZone"/>
    /// (Trash / Hand) matching <paramref name="canTarget"/> and play each onto the battle area (cost-free).
    /// The AS-IS <c>SelectCardEffect.Root</c> maps to <paramref name="fromZone"/>.</summary>
    public static ICardEffect SelectAndPlayFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectAndPlayEffect(card, fromZone, canTarget, maxCount, canEndNotMax, description), description);

    /// <summary>(PRIM-P0 B.O.5) AS-IS <c>CardEffectCommons.PlayOptionCards</c>: select up to
    /// <paramref name="maxCount"/> of the owner's Option cards in <paramref name="sourceZone"/> and play each as a
    /// nested effect (trash → OnUseOption → resolve its [Main]). Cost-free (v1).</summary>
    public static ICardEffect PlayOptionCardEffect(
        CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> optionPredicate, int maxCount, bool canEndNotMax, string description) =>
        new PlayOptionCardEffect(card, sourceZone, optionPredicate, maxCount, canEndNotMax, description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> of the owner's cards in
    /// <paramref name="fromZone"/> (Trash / Library / Security …) matching <paramref name="canTarget"/> and add
    /// each to the owner's hand. AS-IS SelectCardEffect.Mode AddHand.</summary>
    public static ICardEffect SelectAndAddToHandFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description,
        Action<CardSource, MatchStateMutationSink>? onSelectedAny = null) =>
        AsUniformActivated(card, new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.ReturnToHandKind, description, onSelectedAny), description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> of the owner's cards in
    /// <paramref name="fromZone"/> matching <paramref name="canTarget"/> and trash each. AS-IS
    /// SelectCardEffect.Mode Discard.</summary>
    public static ICardEffect SelectAndTrashFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.TrashCardKind, description), description);

    /// <summary>(G16) Select up to <paramref name="maxCount"/> of the owner's CARDS in <paramref name="fromZone"/>
    /// (e.g. Trash) matching <paramref name="canTarget"/> and place each on TOP of the owner's security stack,
    /// FACE-DOWN — the AS-IS "place 1 &lt;X&gt; card from trash face-down on top of security" (IAddSecurity).
    /// Distinct from <c>SelectAndPutSecurityEffect</c> (which targets battle-area PERMANENTS, Mode PutSecurity);
    /// this routes a zone card via <see cref="MatchStateMutationSink.AddToSecurityKind"/> (its defaults =
    /// face-down + top, matching the AS-IS).</summary>
    public static ICardEffect SelectAndPutSecurityFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.AddToSecurityKind, description), description);

    /// <summary>(PRIM-P0-flow B.O.3) AS-IS <c>DigivolveIntoHandOrTrashCard</c>: select 1 of the owner's Digimon
    /// (<paramref name="targetPredicate"/>) and a source card in <paramref name="sourceZone"/> (Hand / Trash,
    /// <paramref name="sourcePredicate"/>) that can digivolve onto it, pay the cost, and digivolve. v1 enforces
    /// requirements.</summary>
    public static ICardEffect SelectAndDigivolveEffect(
        CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> sourcePredicate,
        Func<HeadlessEntityId, bool> targetPredicate, DigivolveCost cost, int costAmount, string description) =>
        new SelectAndDigivolveEffect(card, sourceZone, sourcePredicate, targetPredicate, cost, costAmount, description);

    /// <summary>(PRIM-P0 B.O.4) A one-shot before-pay reduction of THIS card's own play/digivolve cost by
    /// <paramref name="amount"/> when <paramref name="condition"/> holds (AS-IS BeforePayCost ActivateClass →
    /// UntilCalculateFixedCostEffect.Add). Non-interactive.</summary>
    public static ICardEffect BeforePayCostReductionEffect(CardSource card, int amount, Func<bool>? condition, string description) =>
        new BeforePayCostReductionEffect(card, () => amount, condition, description);

    /// <summary>(PRIM-P0 B.O.4) As above with a dynamic reduction amount (e.g. -1 per matching card).</summary>
    public static ICardEffect BeforePayCostReductionEffect(CardSource card, Func<int> amount, Func<bool>? condition, string description) =>
        new BeforePayCostReductionEffect(card, amount, condition, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.AddThisCardToHand(..)</c> — return
    /// this card to the owner's hand.</summary>
    public static IActivatedCardEffect AddThisCardToHandEffect(CardSource card) =>
        new ReturnThisCardToHandEffect(card, "Return this card to the hand.");

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.DigivolveIntoHandOrTrashCard(..)</c>:
    /// select up to <paramref name="maxCount"/> battle-area Digimon matching <paramref name="canTarget"/> and
    /// de-digivolve each by <paramref name="count"/> (remove its top digivolution card[s]).</summary>
    public static ICardEffect SelectAndDeDigivolveEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int count, bool canEndNotMax, string description) =>
        new ActivatedSelectAndDeDigivolveEffect(card, canTarget, maxCount, count, canEndNotMax, description);

    /// <summary>(PRIM-W5) Mirror of the AS-IS <c>CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect</c>:
    /// reveal the top <paramref name="revealCount"/> cards of the owner's deck, select per
    /// <paramref name="conditions"/>, route the rest to <paramref name="remainingTo"/>.</summary>
    public static IActivatedCardEffect SimplifiedRevealDeckTopCardsAndSelect(
        CardSource card, int revealCount, IReadOnlyList<SimplifiedSelectCardConditionClass> conditions,
        RevealDestination remainingTo, string description) =>
        new SimplifiedRevealAndSelectEffect(card, revealCount, conditions, remainingTo, description);

    /// <summary>(P4) Mirror of the FULL AS-IS <c>CardEffectCommons.RevealDeckTopCardsAndSelect</c> with a
    /// <c>SelectCardConditionClass[]</c> (multi-condition sequential passes over the shared revealed pool,
    /// BT10-096/BT10-097/ST17-11 shape). Each original condition maps to one
    /// <see cref="HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass"/> (predicate 1:1, maxCount,
    /// Mode → destination — original <c>Mode.Custom</c> = <see cref="RevealDestination.Custom"/>, read the
    /// picks back from <see cref="RevealMultiSelectEffect.CustomSelections"/>).</summary>
    public static IActivatedCardEffect RevealDeckTopCardsAndSelect(
        CardSource card, int revealCount, IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> selectCardConditions,
        RevealDestination remainingCardsPlace, string description,
        bool canNoAction = false, bool isOpponentDeck = false, bool mutualConditions = false) =>
        new RevealMultiSelectEffect(card, revealCount, selectCardConditions, remainingCardsPlace, description, canNoAction, isOpponentDeck, mutualConditions);

    /// <summary>(PRIM-W5/S2, R3-W3c-1) <c>CanNotAffectedStaticEffect</c> — AS-IS <c>CanNotAffectedClass</c>:
    /// <c>CanNotAffect(target, effect) = CardCondition(target) &amp;&amp; SkillCondition(effect)</c>. Returns the
    /// new-model kind-class <see cref="CardEffects.CanNotAffectedClass"/> (an <c>ICanNotAffectedEffect</c>) consumed
    /// by the live <see cref="CardSource.CanNotBeAffected"/> scan; no registry binding. <paramref name="skillCondition"/>
    /// is the AS-IS <c>Func&lt;ICardEffect,bool&gt;</c> over the CAUSING effect that decides WHICH effects the card is
    /// immune to (e.g. <c>e =&gt; e.EffectSourceCard.Owner != card.Owner &amp;&amp; e.EffectSourceCard.IsDigimon</c> for
    /// "opponent's Digimon effects only"); null falls back to opponent-only.</summary>
    public static ICardEffect CanNotAffectedStaticEffect(Func<Permanent, bool>? permanentCondition, Func<ICardEffect, bool>? skillCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)
    {
        // (R3-W3c-1 flip / RD-W3A-01 RESOLVED) NEW-MODEL: returns the AS-IS kind-class CanNotAffectedClass
        // (ICanNotAffectedEffect) instead of the old-model registry ContinuousImmunityEffect. The LIVE consumer is
        // the R1-e interface scan CardSource.CanNotBeAffected (CardSource.cs:741 — `cardEffect is
        // ICanNotAffectedEffect` over every field permanent's EffectList(None)). The three registry-half consumers
        // that forced the R3-W3a REVERT are now rehomed onto that same live scan (see their R3-W3c-1 comments):
        // GainCanNotBeDeletedByBattle (CardEffectCommons.cs), the ContinuousPlayerScopeRestrictionEffect immunity
        // exemption (ContinuousAndRestrictionEffects.cs), and BlockTiming.HasBlocker. The surviving
        // BlocksOpponentEffect sites (sink, other CardController pipelines, Commons continuous-grant cores) still
        // read the registry but consume ONLY the Progress registry binding — this factory has 0 production callers
        // (test-only), so no card immunity is lost. CanNotAffectedClass declares no ToBinding → the enter-play
        // registrar registers NOTHING; availability is the live EffectList scan alone. AS-IS construction idiom
        // mirrored: PermanentEffectFactory.DigimonEffectImmunity (SetUpICardEffect + SetUpCanNotAffectedClass).
        var effect = new CardEffects.CanNotAffectedClass();
        effect.SetUpICardEffect("Isn't affected by opponent's effect", CanUseCondition, card);
        effect.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
        if (isInheritedEffect)
        {
            effect.SetIsInheritedEffect(true);
        }

        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();

        // AS-IS CanNotAffectedClass.CardCondition (permanentCondition) — WHICH permanents are protected, evaluated
        // live against the protected target CardSource. null keeps the AS-IS self-only grant (protected == holder).
        bool CardCondition(CardSource protectedCard)
        {
            if (permanentCondition is null)
            {
                return protectedCard.InstanceId == card.InstanceId;
            }

            return permanentCondition(new Permanent(protectedCard.Context, protectedCard.InstanceId, protectedCard.Owner));
        }

        // AS-IS CanNotAffectedClass.SkillCondition — WHICH causing effects are blocked, evaluated over the causing
        // ICardEffect. null keeps the opponent-only fallback (the causing effect is sourced by the holder's opponent,
        // AS-IS IsOpponentEffect(cardEffect, card)).
        bool SkillCondition(ICardEffect causingEffect)
        {
            if (skillCondition is not null)
            {
                return skillCondition(causingEffect);
            }

            return causingEffect.EffectSourceCard is not null && causingEffect.EffectSourceCard.Owner != card.Owner;
        }
    }

    /// <summary>(C-3, R3-W3c-4 flip) <c>CanNotTrashFromDigivolutionCardsStaticEffect</c> — AS-IS
    /// <c>CanNotTrashFromDigivolutionCardsClass</c>: <c>CanNotTrashFromDigivolutionCards(source, effect) =
    /// CardCondition(source) &amp;&amp; CardEffectCondition(effect) &amp;&amp; !source.IsFlipped</c>. Returns the
    /// new-model kind-class <see cref="CardEffects.CanNotTrashFromDigivolutionCardsClass"/> (an
    /// <c>ICanNotTrashFromDigivolutionCardsEffect</c>, no <c>ToBinding</c>) consumed by the R1-e live scan
    /// <see cref="CardSource.CanNotTrashFromDigivolutionCards"/>; no registry binding. The EFFECT-trash filter
    /// consults it, the deletion path bypasses it (AS-IS DiscardEvoRoots). <paramref name="cardCondition"/> =
    /// WHICH source (e.g. name contains "X Antibody"); <paramref name="cardEffectCondition"/> = WHICH causing
    /// effect (AS-IS <c>Func&lt;ICardEffect,bool&gt;</c> over the trashing effect; BT9_109 = <c>effect != null</c>
    /// ⇒ always). <paramref name="condition"/> = the effect's own CanUse gate (BT9_109 = host
    /// <c>IsExistOnBattleArea</c>) — protection lapses when the granting host leaves the field. BT9_109 itself
    /// builds the kind-class inline (verbatim); this factory has no real-card producers.</summary>
    public static ICardEffect CanNotTrashFromDigivolutionCardsStaticEffect(
        Func<CardSource, bool> cardCondition, Func<ICardEffect, bool> cardEffectCondition,
        bool isInheritedEffect, CardSource card, Func<bool>? condition)
    {
        var effect = new CardEffects.CanNotTrashFromDigivolutionCardsClass();
        effect.SetUpICardEffect("[Digivolution cards] under this Digimon can't be trashed", CanUseCondition, card);
        effect.SetUpCanNotTrashFromDigivolutionCardsClass(CardCondition: cardCondition, CardEffectCondition: cardEffectCondition);
        if (isInheritedEffect)
        {
            effect.SetIsInheritedEffect(true);
        }

        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
    }

    /// <summary>(E-3) <c>CanNotPlayOptionStaticEffect</c> — AS-IS <c>CanNotPlayClass</c>: a continuous
    /// "an Option matching <paramref name="cardCondition"/> cannot be played" effect scanned by the option-play
    /// legality gate (<see cref="HeadlessDCGO.Engine.Headless.Runtime.CanNotPlayOptionScan"/>).
    /// <paramref name="cardCondition"/> = AS-IS <c>CanNotPlay</c> (WHICH option — owner / IsOption);
    /// <paramref name="condition"/> = the effect's own CanUse gate. Registered as a FIELD static (subject to the
    /// AS-IS stack-position membership); use <see cref="AddCanNotPlayOptionToPlayer"/> for the AS-IS
    /// player-bucket (region ①) duration-bound form.</summary>
    public static ICardEffect CanNotPlayOptionStaticEffect(
        Func<CardSource, bool> cardCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousCanNotPlayOptionEffect(card, cardCondition, isInheritedEffect, condition);

    /// <summary>(PRIM-W5) <c>ChangeCardNamesClass</c> — grants this card an additional name
    /// (<paramref name="addedName"/>), folded into <c>CardSource.CardNames</c>.</summary>
    public static ICardEffect ChangeCardNamesStaticEffect(string addedName, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ChangeCardNamesEffect(card, addedName, isInheritedEffect, condition);

    // ===== (PRIM-W5) special plays — DigiXros / Blast / Blast-DNA =====================================
    // The card DECLARES its recipe (SpecialPlayRecipeRegistry, keyed by card number); SpecialPlayAction then
    // offers/executes the fusion or free digivolve. These factories register the recipe and return a no-op
    // marker for the card's effect list.

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:784 DigiXrosEffectFromNames. Replaces
    // the old mirror-invented SpecialPlayRecipeRegistry version. Verbatim.
    public static AddDigiXrosConditionClass DigiXrosEffectFromNames(CardSource card, int CostReduction, Func<List<CardSource>, CardSource, bool> CanTargetCondition_ByPreSelecetedList, params string[] names)
    {
        AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
        addDigiXrosConditionClass.SetUpICardEffect($"DigiXros", CanUseCondition, card);
        addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
        addDigiXrosConditionClass.SetNotShowUI(true);
        return addDigiXrosConditionClass;

        bool CanUseCondition(Hashtable hashtable) => true;

        DigiXrosCondition GetDigiXros(CardSource cardSource)
        {
            if (cardSource == card)
            {
                return CardEffectCommons.GetDigiXrosConditionsFromNames(card, 2, null, names);
            }

            return null;
        }
    }

    /// <summary>(PRIM-W5) DigiXros with ARBITRARY per-material predicates — the faithful form of the AS-IS
    /// <c>AddDigiXrosConditionClass</c> whose <c>getDigiXrosCondition</c> returns
    /// <c>DigiXrosConditionElement(CanSelectCardCondition, label)</c> per material. Each
    /// <paramref name="materials"/> slot carries the original's <c>CanSelectCardCondition</c> predicate 1:1.</summary>
    public static ICardEffect DigiXrosEffect(CardSource card, int costReduction, params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DigiXros, materials, MemoryCost: 0));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM special-play) DigiXros whose material slots may ALSO be satisfied by cards from the TRASH
    /// (up to <paramref name="maxTrashCount"/>) and/or a Tamer's digivolution sources (up to
    /// <paramref name="maxUnderTamerCount"/>) — AS-IS <c>AddMaxTrashCountDigiXrosClass</c> /
    /// <c>maxTamerDigivolutionCardsCount</c>, whose <c>getMaxTrashCount</c> Func is threaded 1:1 (evaluated per
    /// play). Pass null (or a Func returning 0) for a source not allowed.</summary>
    public static ICardEffect DigiXrosWithExtraMaterialsEffect(
        CardSource card, int costReduction,
        Func<CardSource, int>? maxTrashCount, Func<CardSource, int>? maxUnderTamerCount,
        params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(
            SpecialPlayKind.DigiXros, materials, MemoryCost: 0, Condition: null,
            MaxTrashCount: maxTrashCount, MaxUnderTamerCount: maxUnderTamerCount));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>A material slot matched by card name (the name-equality subset of a DigiXros condition).</summary>
    public static SpecialPlayMaterial MaterialByName(string name) =>
        new(cs => cs.EqualsCardName(name), name);

    private static IReadOnlyList<SpecialPlayMaterial> NameMaterials(IEnumerable<string> names) =>
        names.Select(MaterialByName).ToArray();

    // (P4 KeyWord slice) BlastDigivolveEffect moved to KeyWordEffects/BlastDigivolution.cs (AS-IS 1:1)

    /// <summary>(PRIM special-play) AS-IS Burst Digivolution (<c>BurstDigivolutionCondition</c>): this hand card
    /// digivolves onto a target battle-area Digimon (<paramref name="digimonCondition"/>) while a matching Tamer
    /// (<paramref name="tamerCondition"/>) is returned to the hand, paying <paramref name="cost"/>. The target is
    /// the recipe material; the Tamer is matched + bounced by the action.</summary>
    public static ICardEffect BurstDigivolveEffect(
        CardSource card, Func<CardSource, bool> digimonCondition, Func<CardSource, bool> tamerCondition,
        int cost = 0, Func<bool>? condition = null)
    {
        ArgumentNullException.ThrowIfNull(digimonCondition);
        ArgumentNullException.ThrowIfNull(tamerCondition);
        var target = new SpecialPlayMaterial(digimonCondition, "Burst target Digimon");
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(
            SpecialPlayKind.Burst, new[] { target }, MemoryCost: cost, Condition: condition, TamerCondition: tamerCondition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    // (P4 KeyWord slice) BlastDNADigivolveEffect moved to KeyWordEffects/BlastDNADigivolution.cs (AS-IS 1:1)

    /// <summary>(PRIM-W5) <c>AddJogressConditionClass</c> equivalent — declares this card's Jogress (DNA
    /// digivolve) recipe: the two material names that fuse under it (SpecialPlayKind.DnaDigivolve). Translate
    /// the AS-IS <c>GetJogress</c> callback's material names into <paramref name="names"/>.</summary>
    public static ICardEffect JogressEffectFromNames(CardSource card, Func<bool>? condition, params string[] names)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, NameMaterials(names), MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(Jogress by levels) AS-IS <c>AddJogressLevelsClass</c> — makes THIS card count as extra level(s)
    /// when it is a Jogress / DNA-Digivolution material (e.g. "Also treated as level 6 for DNA Digivolution").
    /// <paramref name="getLevels"/> maps the digivolving (Jogress) card to the extra levels this material grants;
    /// read by <see cref="CardSource.JogressLevelsAgainst"/>. Level-based material predicates then test it.</summary>
    public static ICardEffect AddJogressLevelsEffect(
        CardSource card, Func<CardSource, Permanent, List<int>> getLevels, Func<bool>? condition = null,
        string name = "Also treated as additional levels for DNA Digivolution")
    {
        var effect = new CardEffects.AddJogressLevelsClass();
        effect.SetUpICardEffect(name, CanUseCondition, card);
        effect.SetUpAddJogressLevelsClass(getLevels);
        return effect;

        bool CanUseCondition(Hashtable hashtable) => condition == null || condition();
    }

    /// <summary>(PRIM special-play) AS-IS <c>DNADigivolveWithHandOrTrashCardIntoHandOrTrash</c> — an effect-driven
    /// DNA Digivolution into a hand/trash card (<paramref name="intoCondition"/>) fusing a battle-area permanent
    /// (<paramref name="permanentCondition"/>) with a hand/trash material (<paramref name="materialCondition"/>).
    /// Resolved via the activation flow.</summary>
    public static ICardEffect DnaDigivolveFromHandOrTrashEffect(
        CardSource card, Func<CardSource, bool> intoCondition, Func<CardSource, bool> permanentCondition,
        Func<CardSource, bool> materialCondition, bool intoFromHand, bool materialFromHand,
        string description = "DNA Digivolve using a hand/trash card") =>
        new DnaFromHandOrTrashActivatedEffect(
            card, intoCondition, permanentCondition, materialCondition, intoFromHand, materialFromHand, description);

    /// <summary>(PRIM-W5) Jogress with ARBITRARY per-material predicates (faithful form of
    /// <c>AddJogressConditionClass</c>'s <c>GetJogress</c>).</summary>
    public static ICardEffect JogressEffect(CardSource card, Func<bool>? condition, params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, materials, MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:752 GetJogressConditionClass. Replaces
    // the old mirror-invented JogressEffect/AsMaterial version. Verbatim (canUseCondition is Func<Hashtable,bool>).
    public static AddJogressConditionClass GetJogressConditionClass(Func<Permanent, bool> permanentCondition1, string description1, Func<Permanent, bool> permanentCondition2, string description2, CardSource card, int cost = 0, Func<Hashtable, bool> canUseCondition = null)
    {
        AddJogressConditionClass addJogressConditionClass = new AddJogressConditionClass();
        addJogressConditionClass.SetUpICardEffect($"DNA Digivolution", CanUseCondition, card);
        addJogressConditionClass.SetUpAddJogressConditionClass(getJogressCondition: GetJogress);
        addJogressConditionClass.SetNotShowUI(true);
        return addJogressConditionClass;

        bool CanUseCondition(Hashtable hashtable)
        {
            return canUseCondition == null || canUseCondition(hashtable);
        }

        JogressCondition GetJogress(CardSource cardSource)
        {
            if (cardSource == card)
            {
                return CardEffectCommons.GetJogressConditions(
                    permanentCondition1,
                    description1,
                    permanentCondition2,
                    description2,
                    card
                    );
            }

            return null;
        }
    }

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching Digimon and give each
    /// +<paramref name="changeValue"/> DP for <paramref name="duration"/>" effect (e.g. ST1_13 [Main]).</summary>
    public static ICardEffect SelectAndBuffDpEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.DpDeltaKey, changeValue, duration, description), description);

    /// <summary>An activated "all your Digimon gain +<paramref name="changeValue"/> Security Attack for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST1_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description), description);

    /// <summary>An activated "all your Security Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect, scoped to the owner's Security-zone Digimon
    /// (e.g. ST1_14).</summary>
    public static ICardEffect PlayerScopeBuffSecurityDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopeZone: "Security"), description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> opponent Digimon and trash
    /// <paramref name="trashCount"/> of each host's digivolution cards from the bottom/top" effect
    /// (e.g. ST2_03 / ST2_06 / ST2_09).</summary>
    public static ICardEffect SelectAndTrashDigivolutionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int trashCount, bool fromBottom, string description) =>
        AsUniformActivated(card, new ActivatedSelectTrashDigivolutionEffect(card, canTarget, maxCount, trashCount, fromBottom, description), description);

    /// <summary>(PRIM special-play) AS-IS <c>IDigiBurst</c> — <c>[Digi-Burst N] &lt;effect&gt;</c>: trash N of this
    /// card's own digivolution sources as a cost, then resolve <paramref name="innerEffect"/>. Offered only when
    /// the permanent holds &gt;= N sources. Wrap the card's Digi-Burst body as the inner effect.</summary>
    public static ICardEffect DigiBurstEffect(CardSource card, int count, ICardEffect innerEffect, string description) =>
        new DigiBurstActivatedEffect(card, count, innerEffect, description);

    // (R3-F1 fold) mirror-invented wrapper `UnsuspendSelfTriggerEffect` (returned the invented
    // TriggeredUnsuspendSelfEffect) DELETED — 0 live consumers remained (grep: no card / Tfx / test / engine call;
    // its former card ST2_11 was re-ported). No AS-IS same-named helper exists to fold into, so removal (not a
    // fold) zeroes the TriggeredUnsuspendSelfEffect reference. Class removed from TriggeredEffects.cs.

    /// <summary>An activated "gain/lose <paramref name="amount"/> memory" skill (Option [Main] / [Security],
    /// e.g. ST2_13).</summary>
    public static ICardEffect GainMemoryActivatedEffect(CardSource card, int amount, string description) =>
        new ActivatedMemoryEffect(card, amount, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and return each to its owner's
    /// hand" effect (Option [Main] bounce, e.g. ST2_16).</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Bounce, description), description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon, return each to its owner's
    /// hand, AND trash all of that Digimon's digivolution cards" effect (Option [Main] bounce, e.g. ST4_16).
    /// AS-IS <c>HandBounceClaass.Bounce()</c> unconditionally runs <c>permanent.DiscardEvoRoots()</c>
    /// immediately before the top card leaves the field for EVERY hand-bounce (Permanent.cs:106) — see
    /// <see cref="ActivatedSelectBounceAndDiscardSourcesEffect"/>.</summary>
    public static ICardEffect SelectAndBounceWithSourceDiscardEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string description) =>
        AsUniformActivated(card, new ActivatedSelectBounceAndDiscardSourcesEffect(card, canTarget, maxCount, description), description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and make each unable to attack
    /// and/or block for <paramref name="duration"/>" effect (e.g. ST2_14).</summary>
    public static ICardEffect SelectAndRestrictEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, EffectDuration duration, bool cannotAttack, bool cannotBlock, string description) =>
        new ActivatedTargetRestrictionEffect(card, canTarget, maxCount, duration, cannotAttack, cannotBlock, description);

    // (R3-F1 fold) mirror-invented wrappers `SelfDpBuffTriggerEffect` (returned TriggeredSelfDpBuffEffect) and
    // `RecoveryTriggerEffect` (returned RecoverTriggerEffect) DELETED — 0 live consumers remained (grep: no
    // card / Tfx / test / engine call; former cards ST3_01/ST4_04/BT2_002/BT2_012 and ST3_09 were re-ported). No
    // AS-IS same-named helper exists to fold into, so removal (not a fold) zeroes the TriggeredSelfDpBuffEffect /
    // RecoverTriggerEffect references. Both classes removed from TriggeredEffects.cs.

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and give each
    /// +<paramref name="changeValue"/> Security Attack for <paramref name="duration"/>" effect (e.g. ST3_15 [Main]).</summary>
    public static ICardEffect SelectAndBuffSAttackEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, description), description);

    /// <summary>An activated "all your Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST3_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description), description);

    /// <summary>An activated "all of your opponent's Digimon get +<paramref name="changeValue"/> Security
    /// Attack for <paramref name="duration"/>" player-scope effect, scoped to <paramref name="opponentId"/>
    /// (e.g. ST3_15 [Security] "all opponent Digimon gain Security Attack -1").</summary>
    public static ICardEffect OpponentScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, HeadlessPlayerId opponentId, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopePlayerId: opponentId), description);

    // (P4 slice) ChangeSecurityDigimonCardDPStaticEffect moved to CardEffectFactory/ChangeCardDP.cs (AS-IS 1:1)

    // ============================================================================================================
    // (P4 ACTIVATED timing-builders) 1:1 mirror of AS-IS CardEffectFactory.cs:910-1462 ACTIVATED-effect timing
    // builders. These REPLACE the monolith's old AsUniformActivated/ActivatedEffect-based inline construction —
    // every method returns the ported ActivateClass kind-class (CardEffects/ActivateClass.cs).
    //
    // ASYNC TRANSLATION (the only substrate edits; logic verbatim):
    //   * `Func<Hashtable, ActivateClass, IEnumerator> activateCoroutine` -> `Func<Hashtable, ActivateClass, Task>`
    //     (the card supplies an `async Task` body; the mirror ActivateClass.SetUpActivateClass already takes
    //     `Func<Hashtable,Task>`, so only the delegate TYPE changes — the pass-through lambda is verbatim).
    //   * WhenMovingClass PermanentCondition: AS-IS `card.PermanentOfThisCard()` (returns Permanent) ->
    //     `ICardEffect.ResolvePermanentOfThisCard(card)` (PermanentView->Permanent bridge, per ChangeDP.cs
    //     ADAPTATION (1)); `permanent ==` compares via mirror Permanent value equality (CARDSOURCE-EQUALITY).
    //
    // GATE DIVERGENCE (see docs/audit/rebuild_p4_factory_missing.md): the mirror CardEffectCommons.CanTrigger*
    // family was rebuilt `CardEffectResolveContext ctx`-based, but the AS-IS builders pass a `Hashtable`; and
    // IsExistOnBattleAreaDigimonTrigger / IsExistOnBattleAreaDigimonActivate have no mirror counterpart. These
    // gate calls are kept VERBATIM per the fidelity directive (they surface as call-site errors in the RED build).
    // ============================================================================================================

    #region Shared Effect Condition Creators

    // (P4 ACTIVATED inline-mutation) 1:1 mirror of AS-IS CardEffectFactory.cs:828 ActivateClassesForSharedEffects
    // (ADDED — absent on mirror). Func<Hashtable, ActivateClass, IEnumerator> activateCoroutine ->
    // Func<Hashtable, ActivateClass, Task> (matches the mirror timing-builder classes' signature). Body verbatim.
    public static List<ICardEffect> ActivateClassesForSharedEffects(ref List<ICardEffect> cardEffects,
                                                                    EffectTiming timing,
                                                                    CardSource card,
                                                                    string effectName,
                                                                    Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                                    Func<string, string> effectDescription,
                                                                    bool optional,
                                                                    Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                                    Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                                    Func<Hashtable, bool> isSkippableFunction = null,
                                                                    bool isSkippable = false,
                                                                    int maxCountPerTurn = -1,
                                                                    string hashValue = null,
                                                                    bool whenMoving = false,
                                                                    bool onPlay = false,
                                                                    bool whenDigivolving = false,
                                                                    bool whenAttacking = false,
                                                                    bool onDeletion = false,
                                                                    bool whenLinking = false,
                                                                    bool endOfAttack = false,
                                                                    bool endOfYourTurn = false,
                                                                    bool endOfOpponentTurn = false,
                                                                    bool endOfAllTurns = false,
                                                                    bool startOfYourMainPhase = false,
                                                                    bool counter = false)
    {
        if (whenMoving && timing == EffectTiming.OnMove)
        {
            cardEffects.Add(WhenMovingClass(card, effectName, activateCoroutine, effectDescription("When Moving"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (onPlay && timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(OnPlayClass(card, effectName, activateCoroutine, effectDescription("On Play"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (whenDigivolving && timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(WhenDigivolvingClass(card, effectName, activateCoroutine, effectDescription("When Digivolving"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (whenAttacking && timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(WhenAttackingClass(card, effectName, activateCoroutine, effectDescription("When Attacking"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (onDeletion && timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(OnDeletionClass(card, effectName, activateCoroutine, effectDescription("On Deletion"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (whenLinking && timing == EffectTiming.WhenLinked)
        {
            cardEffects.Add(WhenLinkingClass(card, effectName, activateCoroutine, effectDescription("When Linking"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (endOfAttack && timing == EffectTiming.OnEndAttack)
        {
            cardEffects.Add(EndOfAttackClass(card, effectName, activateCoroutine, effectDescription("End of Attack"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (endOfYourTurn && timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(EndOfYourTurnClass(card, effectName, activateCoroutine, effectDescription("End of Your Turn"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (endOfOpponentTurn && timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(EndOfYourOpponentsTurnClass(card, effectName, activateCoroutine, effectDescription("End of Opponent's Turn"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (endOfAllTurns && timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(EndOfAllTurnsClass(card, effectName, activateCoroutine, effectDescription("End of All Turns"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if(startOfYourMainPhase && timing == EffectTiming.OnStartMainPhase)
        {
            cardEffects.Add(StartOfYourMainPhaseClass(card, effectName, activateCoroutine, effectDescription("Start of Your Main Phase"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }
        if (counter && timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CounterClass(card, effectName, activateCoroutine, effectDescription("Counter"), optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isSkippable: isSkippable));
        }

        return cardEffects;
    }

    #endregion

    #region Boilerplate ActivateClass

    public static ActivateClass ActivateClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, bool> canUseCondition,
                                                Func<Hashtable, ActivateClass, bool> canActivateCondition,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInheritedEffect = false,
                                                bool isLinkedEffect = false,
                                                bool isSecurityEffect = false,
                                                bool isSkippable = false
                                                )
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect(effectName, hashtable => canUseCondition(hashtable, activateClass), card);
        activateClass.SetUpActivateClass(hashtable => canActivateCondition(hashtable, activateClass), hashtable => activateCoroutine(hashtable, activateClass), maxCountPerTurn, optional, effectDescription);
        activateClass.SetHashString(hashValue);
        activateClass.SetIsSecurityEffect(isSecurityEffect);
        activateClass.SetIsSkippableFunction(isSkippableFunction);
        activateClass.SetIsSkippable(isSkippable);
        return activateClass;
    }

    #endregion

    #region When Moving

    public static ActivateClass WhenMovingClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isSkippable = false)
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, false, isSkippable: isSkippable);

        bool PermanentCondition(Permanent permanent)
        {
            // ADAPTATION: AS-IS `card.PermanentOfThisCard()` (Permanent) -> ResolvePermanentOfThisCard bridge.
            return permanent == ICardEffect.ResolvePermanentOfThisCard(card);
        }

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass) &&
                    CardEffectCommons.CanTriggerOnMove(hashtable, PermanentCondition) &&
                    (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }
    }

    #endregion

        #region On Play

    public static ActivateClass OnPlayClass(CardSource card,
                                            string effectName,
                                            Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                            string effectDescription,
                                            bool optional,
                                            Func<Hashtable, bool> isSkippableFunction = null,
                                            Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                            Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                            int maxCountPerTurn = -1,
                                            string hashValue = null,
                                            bool isInherited = false,
                                            bool isSkippable = false)
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, false, isSkippable: isSkippable);

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass) &&
                CardEffectCommons.CanTriggerOnPlay(hashtable, card) &&
                (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }
    }

    #endregion

    #region When Digivolving

    public static ActivateClass WhenDigivolvingClass(CardSource card,
                                                        string effectName,
                                                        Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                        string effectDescription,
                                                        bool optional,
                                                        Func<Hashtable, bool> isSkippableFunction = null,
                                                        Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                        Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                        int maxCountPerTurn = -1,
                                                        string hashValue = null,
                                                        bool isInherited = false,
                                                        bool isLinked = false,
                                                        bool isSkippable = false)
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, isLinked, isSkippable: isSkippable);

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass) &&
                CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card) &&
                (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }
    }

    #endregion

    #region When Attacking
    public static ActivateClass WhenAttackingClass(CardSource card,
                                                    string effectName,
                                                    Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                    string effectDescription,
                                                    bool optional,
                                                    Func<Hashtable, bool> isSkippableFunction = null,
                                                    Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                    Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                    int maxCountPerTurn = -1,
                                                    string hashValue = null,
                                                    bool isInherited = false,
                                                    bool isLinked = false,
                                                    bool isSkippable = false)
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, isLinked, isSkippable: isSkippable);

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass) &&
                CardEffectCommons.CanTriggerOnAttack(hashtable, card) &&
                (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }
    }

    #endregion

    #region On Deletion

    public static ActivateClass OnDeletionClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, isLinked, isSkippable: isSkippable);

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.CanTriggerOnDeletion(hashtable, card) &&
                (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.CanActivateOnDeletion(hashtable, card)
                && (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }
    }

    #endregion

    #region When Linking

    public static ActivateClass WhenLinkingClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false
                                                )
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, isLinked, isSkippable: isSkippable);

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimonTrigger(card, activateClass) &&
                CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card) &&
                (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimonActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }
    }

    #endregion

    #region Security Effect

    public static ActivateClass SecurityClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                bool isSkippable = false)
    {
        ActivateClass activateClass = ActivateClass(card, effectName, CanUseCondition, additionalActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, -1, null, false, false, true, isSkippable);
        return activateClass;

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card) &&
                (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }
    }

    #endregion

    #region End of Attack

    public static ActivateClass EndOfAttackClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, isLinked, isSkippable: isSkippable);

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass)
                && CardEffectCommons.CanTriggerOnAttack(hashtable, card)
                && (additionalUseCondition == null
                    || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }

    }

    #endregion

    #region Counter

    public static ActivateClass CounterClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        ActivateClass activateClass = ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, isLinked, isSkippable: isSkippable);
        activateClass.SetIsCounterEffect(true);
        return activateClass;

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass)
                && CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, permanent => CardEffectCommons.IsOpponentPermanent(permanent, card))
                && (additionalUseCondition == null
                    || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }

    }

    #endregion

    #region Start/End of Turn/Phase, Your/Opponent's/All Turns

/**
 *  Can be used for Your Turn, Opponent's Turn, All Turns, End of [Your|Opponent's|All] Turn, Start of Turn/Phase effects
 *  based on the timing it is used at and additional use conditions passed
 */
    public static ActivateClass TurnTimingClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool yourTurn = false,
                                                bool opponentTurn = false,
                                                bool isSkippable = false)
    {
        return ActivateClass(card, effectName, CanUseCondition, CanActivateCondition, activateCoroutine, effectDescription, optional, isSkippableFunction, maxCountPerTurn, hashValue, isInherited, isLinked, isSkippable: isSkippable);

        bool CanUseCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaTrigger(card, activateClass)
                && (!yourTurn || CardEffectCommons.IsOwnerTurn(card))
                && (!opponentTurn || CardEffectCommons.IsOpponentTurn(card))
                && (additionalUseCondition == null || additionalUseCondition(hashtable, activateClass));
        }

        bool CanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleAreaActivate(card, activateClass) &&
                (additionalActivateCondition == null || additionalActivateCondition(hashtable, activateClass));
        }

    }

    #endregion

    #region Your Turn Classes

    public static ActivateClass StartOfYourTurnClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, true, false, isSkippable);
    }

    public static ActivateClass StartOfYourMainPhaseClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, true, false, isSkippable);
    }

    public static ActivateClass EndOfYourTurnClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, true, false, isSkippable);
    }

    public static ActivateClass YourTurnClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, true, false, isSkippable);
    }

    #endregion

    #region Opponent's Turn Classes

    public static ActivateClass StartOfOpponentsTurnClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, false, true, isSkippable);
    }

    public static ActivateClass StartOfYourOpponentsMainPhaseClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, false, true, isSkippable);
    }

    public static ActivateClass EndOfYourOpponentsTurnClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, false, true, isSkippable);
    }

    public static ActivateClass OpponentsTurnClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, false, true, isSkippable);
    }

    #endregion

    #region All Turn Classes

    public static ActivateClass EndOfAllTurnsClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, true, true, isSkippable);
    }

    public static ActivateClass AllTurnsClass(CardSource card,
                                                string effectName,
                                                Func<Hashtable, ActivateClass, Task> activateCoroutine,
                                                string effectDescription,
                                                bool optional,
                                                Func<Hashtable, bool> isSkippableFunction = null,
                                                Func<Hashtable, ActivateClass, bool> additionalUseCondition = null,
                                                Func<Hashtable, ActivateClass, bool> additionalActivateCondition = null,
                                                int maxCountPerTurn = -1,
                                                string hashValue = null,
                                                bool isInherited = false,
                                                bool isLinked = false,
                                                bool isSkippable = false)
    {
        return TurnTimingClass(card, effectName, activateCoroutine, effectDescription, optional, isSkippableFunction, additionalUseCondition, additionalActivateCondition, maxCountPerTurn, hashValue, isInherited, isLinked, true, true, isSkippable);
    }

    #endregion
}

