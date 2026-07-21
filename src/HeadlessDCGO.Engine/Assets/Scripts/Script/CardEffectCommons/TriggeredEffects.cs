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


/// <summary>(W6 tail) the AS-IS <c>StartOfMainAttack</c> activate body: at the owner's main-phase start,
/// open a MANDATORY attack offer for the granted Digimon (AS-IS SetCanNotSelectNotAttack — cannot decline;
/// player or any Digimon).</summary>
public sealed class StartOfMainAttackEffect : Headless.Effects.IHeadlessCardEffect
{
    private readonly EngineContext _context;
    private readonly HeadlessEntityId _attackerId;

    public StartOfMainAttackEffect(EngineContext context, HeadlessEntityId attackerId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _attackerId = attackerId;
    }

    public Headless.Effects.CardEffectDefinition Definition => new(
        new HeadlessEntityId($"start-of-main-attack:{_attackerId.Value}"), _attackerId,
        "[Start of Your Main Phase] Attack with this Digimon.", Headless.Effects.TriggerTimings.OnStartMainPhase,
        isOptional: false);

    public Headless.Effects.CardEffectCanResolveResult CanResolve(Headless.Effects.CardEffectResolveContext context)
    {
        bool onField = _context.ZoneMover is IZoneStateReader zones &&
            _context.CardInstanceRepository.TryGetInstance(_attackerId, out CardInstanceRecord? rec) && rec is not null &&
            zones.GetCards(rec.OwnerId, ChoiceZone.BattleArea).Contains(_attackerId);
        return onField
            ? Headless.Effects.CardEffectCanResolveResult.Success()
            : Headless.Effects.CardEffectCanResolveResult.Failure("The granted Digimon is no longer on the battle area.");
    }

    public ValueTask<Headless.Effects.EffectResult> ResolveAsync(
        Headless.Effects.CardEffectResolveContext context,
        Headless.Effects.IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        Headless.Runtime.EffectDrivenAttack.RequestChoice(
            _context, _attackerId,
            new Headless.Runtime.EffectAttackOptions(WithoutTap: false, AllowPlayerTarget: true, AllowDigimonTarget: true, TargetUnsuspended: true));
        return ValueTask.FromResult(Headless.Effects.EffectResult.Success("Attack offer opened."));
    }
}


// (R3-C2b-2 fold) TriggeredMemoryEffect DELETED — the invented old-model "[When …] gain/lose N memory" effect
// (registry-lowering, its own scheduler resolution) is retired along with its sole factory
// CardEffectFactory.AddMemoryTriggerEffect. Every former caller (ST1_06/09, ST3_04/05, ST2_12, BT2_010/073,
// BT1_114, TfxOnDeleteGainMemory, TfxOnPlayGainMemory, and the BT1_090 EoT reversal) is now the AS-IS 1:1
// new-model inline ActivateClass memory recipe (card.Owner.AddMemory(N, activateClass) + the AS-IS Hashtable
// CanUse gate).


// (R3-F1b fold) TriggeredSetMemoryEffect DELETED — its sole factory (CardEffectFactory.SetMemoryTo3TamerEffect)
// is now the AS-IS 1:1 ActivateClass port (DCGO CardEffectFactory.cs:11). Zero remaining constructions in src or
// tests (G9-026 references the class name only in a stale comment, not a construction).


// (R3-C2b-2 fold) TriggeredGainMemoryEffect DELETED — the invented old-model "gain N memory" effect (the Tamer
// memory-gain family / the EoTLose3Memory backing) is retired. CardEffectFactory.EoTLose3Memory and the
// Gain1MemoryTamer* factories are now AS-IS 1:1 new-model ActivateClass ports (card.Owner.AddMemory(N,
// activateClass), owner-turn gate inline). Zero remaining constructions in src (tests retargeted in R3-C2b-2).


// (R6-Db D4 EXHAUSTED) The mirror-invented one-shot `PlaySelfAtEndOfBattleTriggerEffect` is DELETED together
// with its `PlaySelfAtEndOfBattleSecurityEffect` producer (ActivatedEffects.cs). It was an EffectRegistry
// OnEndBattle-trigger substitute for the AS-IS `Player.UntilEndBattleEffects` bucket + a DeleteAtTurnEnd metadata
// marker for the delete branch; the real AS-IS idiom (UntilEndBattleEffects.Add + PlayPermanentCards, and
// UntilOpponentTurnEndEffects.Add + DestroyPermanentsClass for the delete branch) is now landed directly in
// CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect. RD-P6C3-B2 RESOLVED.

