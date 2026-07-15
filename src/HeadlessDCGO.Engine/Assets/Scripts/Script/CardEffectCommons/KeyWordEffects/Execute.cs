// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Execute.cs
// (A군 4단계 / Execute 창-재하우징) CanActivateExecute + ExecuteProcess + GainExecute rehoused 1:1 with AS-IS.
// ExecuteProcess's original blocker (RD-R2-01, Permanent.UntilEndAttackEffects) was resolved by the W3 bucket
// (Permanent.cs:1987, live) and PermanentEffectFactory's DeleteSelfEffect/AddDetailClass are now the AS-IS
// ActivateClass overloads (PermanentEffectFactory.cs). The printed/granted Execute now fires through the AS-IS
// OnEndTurn window (GetSkillInfos → MultipleSkills → ExecuteProcess), the same path Vortex/Overclock use
// (C-EoT-2); the invented EndOfTurnEffectAttack + EffectDrivenAttack "Execute-1" firing-half is RETIRED.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    #region Can activate [Execute]

    /// <summary>(R2-A) AS-IS <c>CanActivateExecute</c> (KeyWordEffects/Execute.cs:8, verbatim): on the battle
    /// area and able to make an Execute attack (R1 <c>Permanent.CanAttack(isExecute: true)</c>). isExecute does
    /// NOT bypass summoning sickness (only Rush/isVortex do, Permanent.cs:2244).</summary>
    public static bool CanActivateExecute(CardSource cardSource, ICardEffect activateClass)
    {
        Permanent selfPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);

        return IsExistOnBattleArea(cardSource)
            && selfPermanent != null
            && selfPermanent.CanAttack(activateClass, isExecute: true);
    }

    #endregion

    #region Effect process of [Execute]

    /// <summary>(A군 4단계) AS-IS <c>ExecuteProcess</c> (KeyWordEffects/Execute.cs:18) 1:1: this Digimon makes an
    /// attack (the PLAYER — <c>canAttackPlayerCondition:()=&gt;true</c> — or ANY opponent Digimon incl.
    /// unsuspended, lifted by a per-attack <see cref="CanAttackTargetDefendingPermanentClass"/> appended to
    /// <c>Permanent.UntilEndAttackEffects</c> BEFORE the select), then self-deletes when the attack ends (a
    /// DeleteSelfEffect appended at <c>OnEndAttack</c> + a detail at <c>None</c>, AFTER the select). The
    /// self-delete is a PER-ATTACK effect (UntilEndAttackEffects) — it is NOT registered on a normal main-phase
    /// attack. ADAPTATIONS (substrate only, == VortexProcess): <c>cardSource.PermanentOfThisCard()</c> →
    /// <c>ICardEffect.ResolvePermanentOfThisCard</c> (nullable, guarded); <c>attacker == selectedPermanent</c> →
    /// InstanceId equality; the SelectAttackEffect coroutine → <c>await ...Activate()</c>.</summary>
    public static async Task ExecuteProcess(CardSource cardSource, ICardEffect activateClass)
    {
        Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
        if (selectedPermanent == null)
        {
            return;
        }

        if (selectedPermanent.CanAttack(activateClass, isExecute: true))
        {
            #region Attack unsuspended Digimon

            CanAttackTargetDefendingPermanentClass canAttackTargetDefendingPermanentClass = new CanAttackTargetDefendingPermanentClass();
            canAttackTargetDefendingPermanentClass.SetUpICardEffect("Can attack unsuspended Digimon", CanUseCondition, cardSource);
            canAttackTargetDefendingPermanentClass.SetUpCanAttackTargetDefendingPermanentClass(
                attackerCondition: AttackerCondition,
                defenderCondition: DefenderCondition,
                cardEffectCondition: CardEffectCondition);
            selectedPermanent.UntilEndAttackEffects.Add(_ => canAttackTargetDefendingPermanentClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return IsExistOnBattleArea(cardSource) &&
                       IsOwnerTurn(cardSource);
            }

            bool AttackerCondition(Permanent attacker)
            {
                return attacker.InstanceId == selectedPermanent.InstanceId;
            }

            bool DefenderCondition(Permanent defender)
            {
                return IsPermanentExistsOnOpponentBattleAreaDigimon(defender, cardSource) &&
                       !defender.IsSuspended;
            }

            bool CardEffectCondition(ICardEffect cardEffect)
            {
                return true;
            }

            #endregion

            #region Attack

            SelectAttackEffect selectAttackEffect = GManager.instance!.GetComponent<SelectAttackEffect>();

            selectAttackEffect.SetUp(
                attacker: selectedPermanent,
                canAttackPlayerCondition: () => true,
                defenderCondition: _ => true,
                cardEffect: activateClass);

            await selectAttackEffect.Activate().ConfigureAwait(false);

            #endregion

            #region Delete this Digimon

            selectedPermanent.UntilEndAttackEffects.Add(GetCardEffect);
            selectedPermanent.UntilEndAttackEffects.Add(GetDetailEffect);

            ICardEffect GetCardEffect(EffectTiming timing)
            {
                if (timing == EffectTiming.OnEndAttack)
                {
                    return PermanentEffectFactory.DeleteSelfEffect(selectedPermanent, activateClass);
                }
                return null;
            }

            ICardEffect GetDetailEffect(EffectTiming timing)
            {
                if (timing == EffectTiming.None)
                {
                    return PermanentEffectFactory.AddDetailClass(selectedPermanent, "At end of attack, delete this Digimon.", true, activateClass);
                }
                return null;
            }

            #endregion
        }
    }

    #endregion

    #region Target 1 Digimon gains [Execute]

    /// <summary>(A군 4단계) AS-IS <c>CardEffectCommons.GainExecute</c> (KeyWordEffects/Execute.cs:103) 1:1: register a
    /// <see cref="CardEffectFactory.ExecuteEffect"/> <c>ActivateClass</c> on the target permanent's
    /// <c>OnEndTurn</c> duration bucket via <see cref="AddEffectToPermanent"/> (W3 live). The granted Execute
    /// then fires through the SAME OnEndTurn window that collects a printed Execute (GetSkillInfos →
    /// MultipleSkills → ExecuteProcess). ADAPTATION (substrate only, == GainVortex): the AS-IS
    /// <c>Effects.CreateBuffEffect</c> VFX/SE loop is pure UI (no state) — stripped; the coroutine becomes a
    /// completed <see cref="Task"/>. (No AS-IS card grants Execute — 0 live consumers, as with GainVortex.)</summary>
    public static async Task GainExecute(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent == null) return;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;

        CardSource card = activateClass.EffectSourceCard;

        bool CanUseCondition()
        {
            return IsPermanentExistsOnBattleArea(targetPermanent) &&
                   !targetPermanent.TopCard.CanNotBeAffected(activateClass);
        }

        ActivateClass execute = CardEffectFactory.ExecuteEffect(
            targetPermanent: targetPermanent,
            isInheritedEffect: false,
            condition: CanUseCondition,
            rootCardEffect: activateClass,
            targetPermanent.TopCard);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: execute,
            timing: EffectTiming.OnEndTurn);

        // AS-IS :132-136 CreateBuffEffect (pure VFX/SE) — stripped.
        await Task.CompletedTask;
    }

    #endregion
}
