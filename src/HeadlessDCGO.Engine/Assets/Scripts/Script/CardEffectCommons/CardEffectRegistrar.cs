namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

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


public static class CardEffectRegistrar
{
    /// <summary>The timings auto-registered when a card enters play (continuous + passive triggers).
    /// Player-activated abilities (<see cref="EffectTiming.OptionSkill"/> / <see cref="EffectTiming.SecuritySkill"/>)
    /// are intentionally excluded — their activation flow is built in a later wave.</summary>
    public static readonly IReadOnlyList<EffectTiming> AllTimings = Array.AsReadOnly(new[]
    {
        EffectTiming.None,
        EffectTiming.OnEnterFieldAnyone,
        EffectTiming.OnDetermineDoSecurityCheck,
        EffectTiming.OnUseAttack,
        EffectTiming.WhenDigivolving,
        EffectTiming.OnDestroyedAnyone,
        EffectTiming.OnAllyAttack,
        EffectTiming.OnBlockAnyone,
        // (EX8-3) OnEndTurn self-statics (e.g. <Vortex> via VortexSelfEffect) register at enter-play like the
        // other self-static keyword timings; GR-006's EndOfTurnEffectAttack then reads the live binding at
        // turn end. The original keys <Vortex> under EffectTiming.OnEndTurn (EX8_074 region "Vortex").
        EffectTiming.OnEndTurn,
        EffectTiming.OnStartTurn,
        // (PRIM-P0-timing) auto-register passive triggers on the new high-volume timings. Cards that don't
        // return effects on these timings are unaffected (CardEffects returns empty for the unmatched branch).
        EffectTiming.OnStartMainPhase,
        EffectTiming.OnEndBattle,
        EffectTiming.OnDeclaration,
        // (PRIM-P0-timing batch 2) already-emitted timings, enum-only additions.
        EffectTiming.OnTappedAnyone,
        EffectTiming.OnUnTappedAnyone,
        EffectTiming.OnCounterTiming,
        EffectTiming.WhenLinked,
        EffectTiming.OnLinkCardDiscarded,
        EffectTiming.OnAddDigivolutionCards,
        EffectTiming.OnUseOption,
        EffectTiming.OnDiscardSecurity,
        EffectTiming.AfterPayCost,
        EffectTiming.WhenTopCardTrashed,
        EffectTiming.OnFaceUpSecurityIncreased,
        // (PRIM-P0-timing batch 3a) derived-from-CardMoved timings, enum-only additions.
        EffectTiming.WhenRemoveField,
        EffectTiming.OnLoseSecurity,
        EffectTiming.OnDiscardHand,
        EffectTiming.OnAddHand,
        EffectTiming.OnDiscardLibrary,
        EffectTiming.OnAddSecurity,
        EffectTiming.WhenReturntoHandAnyone,
        EffectTiming.WhenReturntoLibraryAnyone,
        EffectTiming.OnSecurityCheck,
        EffectTiming.OnReturnCardsToHandFromTrash,
        EffectTiming.OnPermamemtReturnedToHand,
        EffectTiming.OnRemovedField,
        EffectTiming.OnLeaveFieldAnyone,
        EffectTiming.OnReturnCardsToLibraryFromTrash,
        // (PRIM-P0-timing batch 3b) end-of-single-attack, collected by EndAttackTriggerHook.
        EffectTiming.OnEndAttack,
        // (PRIM-P0-timing batch 3b) new emit sites (source-discard / attack-target-change).
        EffectTiming.OnDigivolutionCardDiscarded,
        EffectTiming.OnAttackTargetChanged,
        // (c-remediation) digivolution cards returned to deck (AS-IS ReturnToLibraryBottomDigivolutionCardsClass).
        EffectTiming.OnDigivolutionCardReturnToDeckBottom,
        // (PRIM-P0-timing batch 4) would-be-deleted window — registered so HasPreOption can find the effect.
        EffectTiming.WhenPermanentWouldBeDeleted,
    });

    /// <summary>(G6-001) Auto-register the effects of the card instance entering play, resolved from the
    /// dispatch by its card number. No-op (returns false) for cards with no ported effect class — so
    /// un-ported cards are unaffected.</summary>
    public static bool RegisterCard(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        // (effect-play registration) A card can RE-enter play — played out from under its host (G9),
        // replayed from trash (Fortitude/Decode/Partition), or de-digivolved back to the top — while stale
        // self-sourced bindings from a prior stint on the field still linger in the registry (AS-IS has no
        // registry: a permanent's EffectList is simply queried live, so re-entering yields exactly its current
        // effects). Clear this instance's existing self-bindings first so registration is idempotent — removes 0
        // for a first-time play, and prevents the duplicate-effect-id throw on re-entry. Grants sourced from
        // OTHER instances (keyed by their own SourceEntityId) are untouched.
        UnregisterCard(context, instanceId);
        // (B-3 / P1-6) a card entering a fresh play context gets fresh once-per-turn uses — AS-IS CardSource.Init()
        // → InitUseCountThisTurn(). The headless keys the per-turn cap by the card INSTANCE, which is stable across a
        // re-play / de-digivolve / re-stack, so a stale use from an earlier stint this turn would otherwise linger
        // and wrongly cap the effect on re-entry. Reset only THIS card's counts (others untouched). A first-time
        // play removes 0.
        var card = new CardSource(context, instanceId, controller, instance.OwnerId);
        // (P6 stage A) the NEW-model per-turn cap lives on the per-instance CEntity_EffectController
        // (UseEffectsThisTurn) — reset it alongside the legacy OnceFlags reset, mirroring the same AS-IS
        // CardSource.Init() → InitUseCountThisTurn() anchor (CardSource re-entering play re-inits).
        card.cEntity_EffectController.InitUseCountThisTurn();
        RegisterOnEnterPlay(context, effect, def.CardNumber, card);
        return true;
    }

    /// <summary>(G6-001) Remove every binding sourced from <paramref name="instanceId"/> (the card left
    /// play). Returns the number of bindings removed.</summary>
    public static int UnregisterCard(EngineContext context, HeadlessEntityId instanceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty)
        {
            return 0;
        }

        return context.EffectRegistry.RemoveWhere(binding => binding.Request.Context.SourceEntityId == instanceId);
    }

    /// <summary>(faceup security) Build — but do NOT register — a card instance's CONTINUOUS (EffectTiming.None,
    /// non-activated) effect requests, so a face-up security card can be scanned as a live continuous-effect
    /// source WITHOUT the lifecycle of a registry binding (AS-IS re-scans <c>player.SecurityCards where
    /// !IsFlipped</c> live in every getter; there is no registration). Only the None timing is built — a security
    /// card's triggered/activated timings must NOT fire (AS-IS trigger collection scans the field only). Only
    /// bindings carrying the Continuous role for <paramref name="scope"/> are returned (matching how registered
    /// continuous effects are queried). Returns empty for an un-ported card.</summary>
    public static IReadOnlyList<EffectRequest> BuildContinuousRequests(
        EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller, string scope)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (instanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return Array.Empty<EffectRequest>();
        }

        var card = new CardSource(context, instanceId, controller, instance.OwnerId);
        var requests = new List<EffectRequest>();
        int index = 0;
        foreach (ICardEffect cardEffect in effect.CardEffects(EffectTiming.None, card))
        {
            // Activated effects never lower to a continuous request — legacy marker (IActivatedCardEffect)
            // or new-model AS-IS contract (ActivateICardEffect) alike.
            if (cardEffect is IActivatedCardEffect or ActivateICardEffect)
            {
                continue;
            }

            // (P6 stage A) LEGACY-BRIDGE lowering only: an old-model effect still lowers to its EffectBinding
            // (byte-identical to the pre-flip path); a NEW-model kind-class effect has no ToBinding — it is
            // served by the live is-interface scan (stage B), so it contributes no request here.
            if (LegacyBindingBridge.TryToBinding(cardEffect, $"faceupsec:{instanceId.Value}:{def.CardNumber}:None:{index++}", out EffectBinding? binding)
                && binding is not null
                && binding.HasRole(EffectQueryRole.Continuous)
                && binding.QueryScopes.Contains(scope, StringComparer.Ordinal))
            {
                requests.Add(binding.Request);
            }
        }

        return requests;
    }

    public static IReadOnlyList<EffectBinding> RegisterOnEnterPlay(
        EngineContext context,
        CEntity_Effect effect,
        string cardNumber,
        CardSource card)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardNumber);
        ArgumentNullException.ThrowIfNull(card);

        // (P6 STAGE B) ATTACH the effect to the card's controller — the AS-IS setup-time AddCardEffect analog
        // (CEntity_EffectController header MISSING.md: "any consumer that needs cEntity_Effect populated should
        // set it on the controller"). Without this, the new-model interface scan (Permanent/CardSource
        // EffectList → cEntity_Effect.GetCardEffects) cannot see this card's ported continuous kind-classes when
        // the card DEFINITION does not itself dispatch to `effect` (e.g. an effect-play or a test fixture whose
        // definition id is synthetic). For a real card RegisterCard passes the dispatch(def) result, so this is
        // the same class GetOrCreate would have created — behaviour-neutral there, load-bearing for the flip.
        card.cEntity_EffectController.cEntity_Effect = effect;

        var registered = new List<EffectBinding>();
        int index = 0;
        foreach (EffectTiming timing in AllTimings)
        {
            foreach (ICardEffect cardEffect in effect.CardEffects(timing, card))
            {
                // (REGRESSION fix) A factory can legitimately return null from CardEffects when the card has no
                // live board state (e.g. PierceSelfEffect returns null with no live Permanent/TopCard — Pierce.cs:44).
                // Every AS-IS scan boundary filters nulls (mirror CEntity_EffectController.GetCardEffects:217
                // `.Filter(x => x != null)`); this non-AS-IS enter-play registrar enumerates CardEffects directly,
                // so it must tolerate the same nulls rather than feed them to LegacyBindingBridge.ThrowIfNull.
                if (cardEffect is null)
                {
                    continue;
                }

                // Activated / choice effects are resolved via the activation flow, not auto-registered —
                // legacy marker (IActivatedCardEffect) or new-model AS-IS contract (ActivateICardEffect) alike
                // (AS-IS has NO enter-play registration at all: availability is the live EffectList scan,
                // AutoProcessing.cs:770-887).
                if (cardEffect is IActivatedCardEffect or ActivateICardEffect)
                {
                    continue;
                }

                // (P6 stage A) LEGACY-BRIDGE lowering only: an old-model effect keeps its EffectBinding
                // registration (byte-identical path — the stage-B gate consumers still scan the registry). A
                // NEW-model kind-class effect (IChangeDPEffect/… over the abstract ICardEffect) has no
                // ToBinding and registers NOTHING: its availability is the live per-card
                // CardEffects(timing, card) enumeration, which the stage-B is-interface scan reads. Until
                // stage B lands, new-model continuous effects are intentionally invisible to the old gates
                // (documented RED — rebuild_p6_stageA_notes.md §5).
                if (LegacyBindingBridge.TryToBinding(cardEffect, $"{card.InstanceId.Value}:{cardNumber}:{timing}:{index++}", out EffectBinding? binding)
                    && binding is not null)
                {
                    context.EffectRegistry.Register(binding);
                    registered.Add(binding);
                }
            }
        }

        return registered;
    }
}

