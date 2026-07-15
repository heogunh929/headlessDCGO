// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Raid.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainRaid` (CardEffectCommons.cs:3441). AS-IS siblings CanActivateRaid/RaidProcess (same file) are not in
// the bridge map's 91-helper intersection and are left for a later batch.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>(C-Atk) AS-IS <c>CardEffectCommons.GainRaid</c> (KeyWordEffects/Raid.cs:81) 1:1: register a
    /// <see cref="CardEffectFactory.RaidEffect"/> <c>ActivateClass</c> on the target permanent's
    /// <c>OnAllyAttack</c> duration bucket via <see cref="AddEffectToPermanent"/> (W3 live). The granted
    /// Raid then fires through the SAME OnAllyAttack window that collects a printed Raid (GetSkillInfos →
    /// MultipleSkills), NOT the retired RaidAttackSwitch gate. ADAPTATION (substrate only): the AS-IS
    /// <c>CreateBuffEffect</c> VFX loop (Effects.cs:1433) is stripped (pure UI, no state); the coroutine
    /// becomes a completed <see cref="Task"/>.</summary>
    public static async Task GainRaid(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent == null) return;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;

        CardSource card = activateClass.EffectSourceCard;

        bool CanUseCondition()
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(activateClass))
                {
                    return true;
                }
            }

            return false;
        }

        ActivateClass raid = CardEffectFactory.RaidEffect(
            targetPermanent: targetPermanent,
            isInheritedEffect: false,
            condition: CanUseCondition,
            rootCardEffect: activateClass,
            targetPermanent.TopCard);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: raid,
            timing: EffectTiming.OnAllyAttack);

        // AS-IS :117-120 CreateBuffEffect (pure VFX/SE, Effects.cs:1433) — stripped.
        await Task.CompletedTask;
    }

    /// <summary>(P6 cluster2) AS-IS <c>CanActivateRaid</c> (KeyWordEffects/Raid.cs:9, verbatim): this Digimon is
    /// the current attacker and has the highest DP among the opponent's unsuspended Digimon (excluding the
    /// current defender).</summary>
    public static bool CanActivateRaid(CardSource card, Permanent targetPermanent)
    {
        if (targetPermanent?.TopCard is null || !IsPermanentExistsOnBattleArea(targetPermanent))
        {
            return false;
        }

        var attackProcess = GManager.instance!.attackProcess;
        if (!attackProcess.IsAttacking || attackProcess.AttackingPermanent?.InstanceId != targetPermanent.InstanceId)
        {
            return false;
        }

        bool PermanentCondition(Permanent permanent1) =>
            permanent1.InstanceId != attackProcess.DefendingPermanent?.InstanceId && !permanent1.IsSuspended;

        HeadlessPlayerId enemyId = new Player(card.Context, targetPermanent.TopCard!.Owner).Enemy!.PlayerId;
        return HasMatchConditionPermanent(card, (HeadlessEntityId id) => IsMaxDP(PermanentOf(card, id), enemyId, PermanentCondition));
    }

    /// <summary>(P6 cluster2) AS-IS <c>RaidProcess</c> (KeyWordEffects/Raid.cs:40): owner chooses 1 of the
    /// opponent's unsuspended highest-DP Digimon and switches the attack's target to it via the existing
    /// <c>AttackProcess.SwitchDefender</c> (mirror shape: entity ids, not <see cref="ICardEffect"/>/
    /// <see cref="Permanent"/>, and no cancellation-token parameter).</summary>
    public static async Task RaidProcess(CardSource card, Permanent targetPermanent, ICardEffect activateClass, CancellationToken cancellationToken = default)
    {
        if (targetPermanent?.TopCard is null)
        {
            return;
        }

        HeadlessPlayerId enemyId = new Player(card.Context, targetPermanent.TopCard!.Owner).Enemy!.PlayerId;
        bool CanSelectPermanentCondition(HeadlessEntityId id) =>
            IsMaxDP(PermanentOf(card, id), enemyId, permanent1 => !permanent1.IsSuspended);

        if (!HasMatchConditionPermanent(card, CanSelectPermanentCondition))
        {
            return;
        }

        Headless.Choices.ChoiceRequest request = EffectChoiceHelpers.CreatePermanentRequest(
            targetPermanent.TopCard!.Owner,
            "Select 1 Digimon that this Digimon attacks to.",
            minCount: 0, maxCount: 1, canSkip: true,
            EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
                .Where(p => CanSelectPermanentCondition(p.InstanceId))
                .Select(p => EffectChoiceHelpers.Candidate(p.InstanceId, p.InstanceId.Value, Headless.Choices.ChoiceZone.BattleArea, isSelectable: true, p.OwnerId))
                .ToList());
        Headless.Choices.ChoiceResult result = await card.Context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        await GManager.instance!.attackProcess.SwitchDefender(
            activateClass?.EffectSourceCard?.InstanceId, isBlock: false, result.SelectedIds[0]).ConfigureAwait(false);
    }
}
