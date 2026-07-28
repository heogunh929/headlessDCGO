
// (R2-A) old-model CardEffectCommons siblings (KeyWordEffects/Vortex.cs) — a different namespace/type than the
// KeywordBaseBatch2Effect resolver above. R1 now provides Permanent.CanAttack/CanAttackTargetDigimon, so both
// CanActivateVortex and VortexProcess are rehoused 1:1 with AS-IS. PermanentHasVortexCanAttackPlayers (AS-IS
// defines it in THIS file) is mirrored here via the R1 live EffectList scan. The AS-IS SelectAttackEffect step
// (no mirror class) is the established EffectDrivenAttack substrate — the same offer the live path
// (EndOfTurnEffectAttack + EffectDrivenAttack) makes.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

    public static partial class CardEffectCommons
    {
        /// <summary>(C-EoT-2) AS-IS <c>CardEffectCommons.GainVortex</c> (KeyWordEffects/Vortex.cs:81) 1:1: register a
        /// <see cref="CardEffectFactory.VortexEffect"/> <c>ActivateClass</c> on the target permanent's
        /// <c>OnEndTurn</c> duration bucket via <see cref="AddEffectToPermanent"/> (W3 live). The granted Vortex
        /// then fires through the SAME OnEndTurn window that collects a printed Vortex (GetSkillInfos →
        /// MultipleSkills → VortexProcess), NOT the retired EndOfTurnEffectAttack gate. ADAPTATION (substrate only):
        /// the AS-IS <c>Effects.CreateBuffEffect</c> VFX/SE loop is pure UI (no state) — stripped; the coroutine
        /// becomes a completed <see cref="Task"/> (== the wave2 GainRaid/GainAlliance shape).</summary>
        public static async Task GainVortex(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
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

            ActivateClass vortex = CardEffectFactory.VortexEffect(
                targetPermanent: targetPermanent,
                isInheritedEffect: false,
                condition: CanUseCondition,
                rootCardEffect: activateClass,
                targetPermanent.TopCard);

            AddEffectToPermanent(
                targetPermanent: targetPermanent,
                effectDuration: effectDuration,
                card: card,
                cardEffect: vortex,
                timing: EffectTiming.OnEndTurn);

            // AS-IS :110-114 CreateBuffEffect (pure VFX/SE) — stripped.
            await Task.CompletedTask;
        }

        /// <summary>(R2-A) AS-IS <c>CanActivateVortex</c> (KeyWordEffects/Vortex.cs:7, verbatim): on the battle
        /// area, this Digimon can make a Vortex attack, and either an opponent Digimon it can Vortex-attack
        /// exists OR a <c>VortexCanAttackPlayers</c> effect lets it attack the player. AS-IS
        /// <c>HasMatchConditionOpponentsPermanent(Func&lt;Permanent&gt;)</c> — the predicate reads the opponent
        /// permanent directly.</summary>
        public static bool CanActivateVortex(CardSource cardSource, ICardEffect activateClass)
        {
            Permanent selfPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);

            return IsExistOnBattleArea(cardSource)
                && selfPermanent != null
                && selfPermanent.CanAttack(activateClass, isVortex: true)
                && (HasMatchConditionOpponentsPermanent(cardSource, permanent =>
                        permanent.IsDigimon
                        && selfPermanent.CanAttackTargetDigimon(permanent, activateClass, isVortex: true))
                    || PermanentHasVortexCanAttackPlayers(selfPermanent));
        }

        /// <summary>(R2-A) AS-IS <c>PermanentHasVortexCanAttackPlayers</c> (KeyWordEffects/Vortex.cs:19): scans
        /// EVERY player's field permanents' + players' <c>EffectList(None)</c> (R1 live scan) for an active
        /// <c>IVortexCanAttackPlayersEffect</c> that accepts <paramref name="permanent"/> as a Vortex attacker.
        /// ADAPTATION: AS-IS <c>GManager.instance.turnStateMachine.gameContext</c> → <c>new GameContext(context)</c>;
        /// the context is read off the permanent's TopCard (the mirror Permanent exposes no public context).</summary>
        public static bool PermanentHasVortexCanAttackPlayers(Permanent permanent)
        {
            var context = permanent.TopCard.Context;

            #region the effects of permanents
            foreach (Player player in new GameContext(context).Players)
            {
                foreach (Permanent fieldPermanent in player.GetFieldPermanents())
                {
                    foreach (ICardEffect cardEffect in fieldPermanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IVortexCanAttackPlayersEffect
                            && cardEffect.CanUse(null)
                            && ((IVortexCanAttackPlayersEffect)cardEffect).VortexCanAttackPlayersPermanent(permanent))
                        {
                            return true;
                        }
                    }
                }
            }
            #endregion

            #region the effects of players
            foreach (Player player in new GameContext(context).Players)
            {
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IVortexCanAttackPlayersEffect
                        && cardEffect.CanUse(null)
                        && ((IVortexCanAttackPlayersEffect)cardEffect).VortexCanAttackPlayersPermanent(permanent))
                    {
                        return true;
                    }
                }
            }
            #endregion

            return false;
        }

        /// <summary>(R2-A) AS-IS <c>VortexProcess</c> (KeyWordEffects/Vortex.cs:56): this Digimon makes a Vortex
        /// attack — any opponent Digimon (incl. unsuspended, AS-IS <c>SetIsVortex</c> + defenderCondition:_=&gt;true),
        /// and the player only while a <c>VortexCanAttackPlayers</c> effect accepts it (snapshotted once at the
        /// start, verbatim AS-IS). (R5-B) the in-place SelectAttackEffect mirror replaces the R2-A
        /// EffectDrivenAttack.RequestChoice ADAPTATION.</summary>
        public static async Task VortexProcess(CardSource cardSource, ICardEffect activateClass)
        {
            Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
            if (selectedPermanent == null)
            {
                return;
            }

            bool canAttackPlayers = PermanentHasVortexCanAttackPlayers(selectedPermanent);

            if (selectedPermanent.CanAttack(activateClass, isVortex: true))
            {
                var selectAttackEffect = GManager.instance!.GetComponent<SelectAttackEffect>();

                selectAttackEffect.SetUp(
                    attacker: selectedPermanent,
                    canAttackPlayerCondition: () => canAttackPlayers,
                    defenderCondition: _ => true,
                    cardEffect: activateClass);

                selectAttackEffect.SetIsVortex();

                await selectAttackEffect.Activate().ConfigureAwait(false);
            }
        }
    }
}
