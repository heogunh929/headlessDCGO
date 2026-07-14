// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Vortex.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Vortex). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Vortex's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateVortex). The LIVE "this Digimon makes an effect-driven attack" path is the
// S1 hub EffectDrivenAttack — this branch is the grant/mirror layer (resolving emits GrantVortex).
// AS-IS Vortex target (DataBase.VortexEffectDiscription + VortexProcess): "At the end of your turn, this
// Digimon may attack an opponent's DIGIMON (any — defenderCondition _ => true with SetIsVortex; NOT the
// player), can attack the turn it was played." Player-target requires a SEPARATE VortexCanAttackPlayers
// effect, not the base keyword. GR-006 wires this via EndOfTurnEffectAttack -> EffectDrivenAttack with
// EffectAttackOptions(AllowDigimonTarget:true, TargetUnsuspended:true, AllowPlayerTarget:FALSE).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveVortex(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            // AS-IS CanActivateVortex: this Digimon is on the battle area (the dispatch already enforces battle
            // area). The full "can attack a Digimon or player" eligibility is enforced live by
            // EffectDrivenAttack.GetTargets when the attack is offered.
            return CardEffectCanResolveResult.Success("Vortex can initiate an effect-driven attack.", BaseValues(context, target));
        }
    }
}

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

    public static partial class CardEffectCommons
    {
        /// <summary>(R2-A) AS-IS <c>CanActivateVortex</c> (KeyWordEffects/Vortex.cs:7, verbatim): on the battle
        /// area, this Digimon can make a Vortex attack, and either an opponent Digimon it can Vortex-attack
        /// exists OR a <c>VortexCanAttackPlayers</c> effect lets it attack the player. ADAPTATION: AS-IS
        /// <c>HasMatchConditionOpponentsPermanent(Func&lt;Permanent&gt;)</c> → the entity-id overload
        /// (<c>PermanentOf</c> bridges each id back to a <see cref="Permanent"/>).</summary>
        public static bool CanActivateVortex(CardSource cardSource, ICardEffect activateClass)
        {
            Permanent selfPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);

            return IsExistOnBattleArea(cardSource)
                && selfPermanent != null
                && selfPermanent.CanAttack(activateClass, isVortex: true)
                && (HasMatchConditionOpponentsPermanent(cardSource, (Headless.Services.HeadlessEntityId id) =>
                    {
                        Permanent permanent = PermanentOf(cardSource, id);
                        return permanent.IsDigimon
                            && selfPermanent.CanAttackTargetDigimon(permanent, activateClass, isVortex: true);
                    })
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
        /// start, verbatim AS-IS). ADAPTATION: AS-IS SelectAttackEffect → the EffectDrivenAttack substrate.</summary>
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
                Headless.Runtime.EffectDrivenAttack.RequestChoice(
                    cardSource.Context, selectedPermanent.InstanceId,
                    new Headless.Runtime.EffectAttackOptions(WithoutTap: false, AllowPlayerTarget: canAttackPlayers, AllowDigimonTarget: true, TargetUnsuspended: true));
            }

            await Task.CompletedTask;
        }
    }
}
