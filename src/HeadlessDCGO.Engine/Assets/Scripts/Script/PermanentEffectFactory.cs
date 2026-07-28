namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public static class PermanentEffectFactory
{
    /// <summary>(AD1-S) 1:1 mirror of AS-IS <c>PermanentEffectFactory.CanNotSwitchAttackTargetEffect(targetPermanent,
    /// activateClass)</c> (PermanentEffectFactory.cs:109-127): "This Digimon's attack target can't be switched."
    /// CanUse mirror = target on the battle area AND the controller's turn (<c>IsOwnerTurn</c>) — evaluated
    /// LIVE; predicate = <c>permanent == targetPermanent</c> (locks the effect to this attacker).
    /// <paramref name="activateClass"/> is accepted for source-signature fidelity (the AS-IS
    /// <c>CanNotBeAffected(activateClass)</c> live guard has no port surface on a bare ICardEffect — the
    /// grant is a SELF/own effect in every AS-IS caller, where that guard is vacuous).
    /// Register with <c>ToBinding(id, EffectDuration.UntilEachTurnEnd)</c> to mirror the AS-IS
    /// <c>UntilEachTurnEndEffects.Add(...)</c> bucket.</summary>
    public static CardEffects.CanNotSwitchAttackTargetClass CanNotSwitchAttackTargetEffect(
        CardEffectCommons.Permanent targetPermanent, CardEffectCommons.ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(targetPermanent);
        _ = activateClass;
        CardEffectCommons.CardSource topCard = targetPermanent.TopCard;
        var effect = new CardEffects.CanNotSwitchAttackTargetClass();
        effect.SetUpICardEffect(
            "This Digimon's attack target can't be switched.",
            (System.Collections.Hashtable _) => CardEffectCommons.CardEffectCommons.IsExistOnBattleArea(topCard)
                && CardEffectCommons.CardEffectCommons.IsOwnerTurn(topCard),
            topCard);
        effect.SetUpCanNotSwitchAttackTargetClass(
            permanent => permanent is not null && permanent.InstanceId == targetPermanent.InstanceId);
        return effect;
    }

    /// <summary>(AD1 / 이연④-b RD-IMM-01 RESOLVED) 1:1 mirror of AS-IS
    /// <c>PermanentEffectFactory.DigimonEffectImmunity(permanent)</c> (PermanentEffectFactory.cs:51-76):
    /// "Not affected by opponent's Digimon's effects." Builds the AS-IS kind-class
    /// <see cref="CardEffects.CanNotAffectedClass"/> (an <c>ICanNotAffectedEffect</c>) so the LIVE
    /// <see cref="CardEffectCommons.CardSource.CanNotBeAffected"/> scan actually SEES it — replacing the retired
    /// old-model <c>ContinuousImmunityEffect</c> (ICardEffect-only, invisible to that scan) that left this grant
    /// production-INERT on the real cards BT25_019 / EX11_074. <c>CardCondition</c> protects exactly this permanent
    /// (AS-IS <c>cardSource == permanent.TopCard</c> + live existence); <c>SkillCondition</c> admits only
    /// opponent-owned DIGIMON effects (AS-IS <c>IsOpponentEffect(cardEffect, permanent.TopCard) &amp;&amp;
    /// cardEffect.IsDigimonEffect</c>); CanUse mirrors AS-IS <c>IsExistOnBattleAreaDigimon</c>. The caller adds this
    /// to its <c>UntilOpponentTurnEndEffects</c> bucket (AS-IS <c>Until…Effects.Add</c>), surfaced by
    /// <c>Permanent.EffectList_Added</c> at every timing. ADAPTATION (substrate only): the AS-IS
    /// <c>IsOpponentEffect(ICardEffect,CardSource)</c> overload reduces the causing effect to its
    /// <c>EffectSourceCard</c>; the mirror overload takes that source card directly (established Progress.cs idiom).</summary>
    public static CardEffects.CanNotAffectedClass DigimonEffectImmunity(CardEffectCommons.Permanent permanent)
    {
        ArgumentNullException.ThrowIfNull(permanent);

        bool CanUseCondition1(Hashtable hashtable)
        {
            return CardEffectCommons.CardEffectCommons.IsExistOnBattleAreaDigimon(permanent.TopCard);
        }

        bool CardCondition(CardEffectCommons.CardSource cardSource)
        {
            return cardSource == permanent.TopCard
                && CardEffectCommons.CardEffectCommons.IsExistOnBattleAreaDigimon(permanent.TopCard);
        }

        bool SkillCondition(CardEffectCommons.ICardEffect cardEffect)
        {
            return CardEffectCommons.CardEffectCommons.IsOpponentEffect(cardEffect.EffectSourceCard, permanent.TopCard)
                && cardEffect.IsDigimonEffect;
        }

        CardEffects.CanNotAffectedClass canNotAffectedClass = new CardEffects.CanNotAffectedClass();
        canNotAffectedClass.SetUpICardEffect("Not affected by opponent's Digimon's effects", CanUseCondition1, permanent.TopCard);
        canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
        canNotAffectedClass.SetEffectSourcePermanent(permanent);
        return canNotAffectedClass;
    }

    /// <summary>(AD1 / 이연④-b RD-IMM-01 RESOLVED) 1:1 mirror of AS-IS
    /// <c>PermanentEffectFactory.OptionEffectImmunity(permanent)</c> (PermanentEffectFactory.cs:80-105):
    /// "Not affected by opponent's Option effects." As <see cref="DigimonEffectImmunity"/> but the
    /// <c>SkillCondition</c> admits only opponent-owned OPTION effects (AS-IS <c>IsOpponentEffect &amp;&amp;
    /// !cardEffect.IsDigimonEffect &amp;&amp; !cardEffect.IsTamerEffect</c>).</summary>
    public static CardEffects.CanNotAffectedClass OptionEffectImmunity(CardEffectCommons.Permanent permanent)
    {
        ArgumentNullException.ThrowIfNull(permanent);

        bool CanUseCondition1(Hashtable hashtable)
        {
            return CardEffectCommons.CardEffectCommons.IsExistOnBattleAreaDigimon(permanent.TopCard);
        }

        bool CardCondition(CardEffectCommons.CardSource cardSource)
        {
            return cardSource == permanent.TopCard
                && CardEffectCommons.CardEffectCommons.IsExistOnBattleAreaDigimon(permanent.TopCard);
        }

        bool SkillCondition(CardEffectCommons.ICardEffect cardEffect)
        {
            return CardEffectCommons.CardEffectCommons.IsOpponentEffect(cardEffect.EffectSourceCard, permanent.TopCard)
                && !cardEffect.IsDigimonEffect && !cardEffect.IsTamerEffect;
        }

        CardEffects.CanNotAffectedClass canNotAffectedClass = new CardEffects.CanNotAffectedClass();
        canNotAffectedClass.SetUpICardEffect("Not affected by opponent's Option effects", CanUseCondition1, permanent.TopCard);
        canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
        canNotAffectedClass.SetEffectSourcePermanent(permanent);
        return canNotAffectedClass;
    }

    /// <summary>(AD1) 1:1 mirror of AS-IS <c>PermanentEffectFactory.CollisionEffect(targetPermanent,
    /// activateClass)</c> (PermanentEffectFactory.cs:131-144): grants &lt;Collision&gt; to exactly this permanent.
    /// Delegates to <c>CollisionStaticEffect</c> with <c>permanentCondition = permanent == targetPermanent</c>
    /// and the live existence gate (AS-IS CanUseCondition = <c>IsPermanentExistsOnBattleArea</c>). Replaces the
    /// flattened binding-rule form that dropped the target predicate. <paramref name="activateClass"/> is
    /// accepted for source-signature fidelity (AS-IS <c>CanNotBeAffected(activateClass)</c> guard is vacuous on
    /// a self grant, no port surface).</summary>
    public static CardEffectCommons.ICardEffect CollisionEffect(
        CardEffectCommons.Permanent targetPermanent, CardEffectCommons.ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(targetPermanent);
        _ = activateClass;
        CardEffectCommons.CardSource topCard = targetPermanent.TopCard;
        return CardEffectCommons.CardEffectFactory.CollisionStaticEffect(
            permanentCondition: permanent => permanent is not null && permanent.InstanceId == targetPermanent.InstanceId,
            isInheritedEffect: false,
            card: topCard,
            condition: () => CardEffectCommons.CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent));
    }

    // ---------------------------------------------------------------------------------------------------------
    // (G-clean) The invented string-key binding-rule overloads (CreateRegistry / DeleteSelfEffect(string,…) /
    // DigimonEffectImmunity(string,…) / OptionEffectImmunity(string,…) / CollisionEffect(string,…) /
    // AddDetailClass(string,…) / Bind) and PermanentEffectFactoryBinding.cs were physically deleted — they had 0
    // src consumers (only tests/G3J-002 exercised them). The G3Z-001 Phase-3 aggregate contract was updated to
    // drop that gate goal. The AS-IS ActivateClass overloads below (Permanent-first) are unrelated and stay:
    // DeleteSelfEffect / AddDetailClass are the AS-IS PermanentEffectFactory (DCGO PermanentEffectFactory.cs:11-47
    // / :146-161) that ExecuteProcess appends to Permanent.UntilEndAttackEffects.
    // ---------------------------------------------------------------------------------------------------------

    #region Effect of a Permanent to Delete Itself
    /// <summary>(A군 4단계) 1:1 mirror of AS-IS <c>PermanentEffectFactory.DeleteSelfEffect(permanent, cardEffect,
    /// deleteOnOwnturn, deleteOnOpponentsTurn)</c> (PermanentEffectFactory.cs:11-47): an <c>ActivateClass</c>
    /// "Delete this Digimon" whose <c>CanUse</c> gate is the target still on the battle area + not immune, split
    /// by whose turn it is (owner-turn vs opponent-turn flags), and whose body deletes the permanent through
    /// <see cref="CardEffectCommons.DestroyPermanentsClass"/>. Used by ExecuteProcess as the OnEndAttack
    /// self-delete. ADAPTATION (substrate only): the coroutine
    /// <c>ContinuousController.StartCoroutine(new DestroyPermanentsClass(...).Destroy())</c> becomes
    /// <c>await new DestroyPermanentsClass(...).Destroy()</c> (async model, == every card's Destroy call).</summary>
    public static CardEffects.ActivateClass DeleteSelfEffect(
        CardEffectCommons.Permanent permanent,
        CardEffectCommons.ICardEffect cardEffect,
        bool deleteOnOwnturn = true,
        bool deleteOnOpponentsTurn = true)
    {
        CardEffects.ActivateClass activateClass = new CardEffects.ActivateClass();
        activateClass.SetUpICardEffect("Delete this Digimon", CanUseCondition, permanent.TopCard);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, "");
        activateClass.SetEffectSourcePermanent(permanent);
        return activateClass;

        bool CanUseCondition(Hashtable hashtable)
        {
            if (permanent.TopCard != null
                && CardEffectCommons.CardEffectCommons.IsExistOnBattleArea(permanent.TopCard)
                && !permanent.TopCard.CanNotBeAffected(cardEffect))
            {
                if (CardEffectCommons.CardEffectCommons.IsOwnerTurn(permanent.TopCard))
                {
                    return deleteOnOwnturn;
                }
                else
                {
                    return deleteOnOpponentsTurn;
                }
            }
            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return permanent.TopCard != null
                && CardEffectCommons.CardEffectCommons.IsExistOnBattleArea(permanent.TopCard);
        }

        async Task ActivateCoroutine(Hashtable hashtable)
        {
            await new CardEffectCommons.DestroyPermanentsClass(
                new List<CardEffectCommons.Permanent> { permanent },
                CardEffectCommons.CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
        }
    }
    #endregion

    #region Add Detail for Display
    /// <summary>(A군 4단계) 1:1 mirror of AS-IS <c>PermanentEffectFactory.AddDetailClass(targetPermanent, detail,
    /// triggerEffect, activateClass)</c> (PermanentEffectFactory.cs:146-161): an <c>AddDetailClass</c> that shows
    /// <paramref name="detail"/> on the target permanent while it exists and is not immune. Used by ExecuteProcess
    /// (EffectTiming.None) to display "At end of attack, delete this Digimon." ADAPTATION: AS-IS
    /// <c>permanent == targetPermanent</c> → InstanceId equality (mirror Permanent is a fresh wrapper per access,
    /// as in <see cref="CollisionEffect(CardEffectCommons.Permanent, CardEffectCommons.ICardEffect)"/> /
    /// <see cref="CanNotSwitchAttackTargetEffect"/>).</summary>
    public static CardEffects.AddDetailClass AddDetailClass(
        CardEffectCommons.Permanent targetPermanent, string detail, bool triggerEffect, CardEffectCommons.ICardEffect activateClass)
    {
        return CardEffectCommons.CardEffectFactory.AddDetailClass(CanUseCondition, PermanentCondition, detail, triggerEffect, activateClass.EffectSourceCard);

        bool PermanentCondition(CardEffectCommons.Permanent permanent)
        {
            return permanent is not null
                && permanent.InstanceId == targetPermanent.InstanceId;
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent)
                && !targetPermanent.TopCard.CanNotBeAffected(activateClass);
        }
    }
    #endregion
}
