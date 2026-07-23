
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(P6 cluster2) AS-IS-signature <c>CanActivateBlitz(cardSource, activateClass)</c> overload —
        /// AS-IS itself ignores <c>activateClass</c> in this gate (Blitz.cs:10-17 reads only the card/board
        /// state); delegates to the verified substrate <c>CanActivateBlitz(cardSource)</c> (CardEffectCommons.cs).</summary>
        public static bool CanActivateBlitz(CardSource cardSource, ICardEffect activateClass) => CanActivateBlitz(cardSource);

        #region Effect process of [Blitz] (AS-IS KeyWordEffects/Blitz.cs:31)

        /// <summary>(BRIDGE W3) AS-IS <c>BlitzProcess(cardSource, activateClass, beforeOnAttackCoroutine)</c> —
        /// AS-IS-signature overload delegating to the verified substrate <c>BlitzProcess(cardSource)</c>
        /// (gate: <c>CanActivateBlitz</c>, then the effect-driven attack offer — player + any Digimon, AS-IS
        /// SelectAttackEffect canAttackPlayer/defender = true, via <c>EffectDrivenAttack.RequestChoice</c>,
        /// whose target enumeration applies the engine's attack-legality scans).
        ///
        /// Dropped-parameter handling (bridge-map ⚠️, §11.11 rule 4 — nothing silent):
        /// - <paramref name="activateClass"/>: AS-IS threads it into the <c>CanAttack(activateClass)</c> gate
        ///   and <c>SelectAttackEffect.SetUp(cardEffect:)</c>. The no-hook path's substrate gate/offer has
        ///   no causing-effect input — design item RD-W3-7 residual (latent until a cause-conditioned attack
        ///   restriction producer is ported; no such producer exists on the mirror today). The HOOK path (below)
        ///   DOES thread <paramref name="activateClass"/> into <c>SelectAttackEffect.SetUp(cardEffect:)</c> 1:1.
        /// - <paramref name="beforeOnAttackCoroutine"/> (RD-W3-7 RESOLVED): AS-IS runs it after the attacker
        ///   suspend, before the [On Attack] window (AttackProcess.cs:191). A non-null hook now routes 1:1 through
        ///   the mirror <see cref="SelectAttackEffect"/> (the AS-IS SelectAttackEffect port — the SAME surface
        ///   Execute/Vortex/Overclock use), whose <c>Activate</c> declares the attack INLINE via the async-pausable
        ///   <c>AttackDeclarationCommons.DeclareAsync</c>, firing the hook at the AS-IS point. The hook's own select
        ///   (ST13_06's mandatory Jogress destroy) parks the pump in place and resumes (WaitPendingChoiceUnderPump
        ///   idiom). The no-hook path keeps the established deferred <see cref="Headless.Runtime.EffectDrivenAttack"/>
        ///   offer (digest-stable; the sole hook caller is ST13_06, not in any digest game).</summary>
        public static async Task BlitzProcess(CardSource cardSource, ICardEffect activateClass, Func<Task> beforeOnAttackCoroutine = null)
        {
            if (beforeOnAttackCoroutine == null)
            {
                _ = activateClass;   // design item RD-W3-7 residual (see summary — no-hook gate/offer cause-threading).
                BlitzProcess(cardSource);
                await Task.CompletedTask;
                return;
            }

            // (RD-W3-7) AS-IS BlitzProcess (Blitz.cs:31-47) 1:1 — the pre-OnAttack-hook path. Guard, then the AS-IS
            // SelectAttackEffect offer (attacker = this permanent, player + any Digimon targetable) with the
            // beforeOnAttackCoroutine set, awaited. `cardSource.PermanentOfThisCard()` → ResolvePermanentOfThisCard.
            if (CanActivateBlitz(cardSource, activateClass))
            {
                Permanent attacker = ICardEffect.ResolvePermanentOfThisCard(cardSource);
                if (attacker == null)
                {
                    return;
                }

                SelectAttackEffect selectAttackEffect = GManager.instance.GetComponent<SelectAttackEffect>();

                selectAttackEffect.SetUp(
                    attacker: attacker,
                    canAttackPlayerCondition: () => true,
                    defenderCondition: (permanent) => true,
                    cardEffect: activateClass);

                selectAttackEffect.SetBeforeOnAttackCoroutine(beforeOnAttackCoroutine);

                await selectAttackEffect.Activate();
            }
        }

        #endregion

        #region Target 1 Digimon gains [Blitz] (AS-IS KeyWordEffects/Blitz.cs:51)

        /// <summary>(C-Atk) AS-IS <c>CardEffectCommons.GainBlitz</c> (KeyWordEffects/Blitz.cs:51) 1:1: register a
        /// <see cref="CardEffectFactory.BlitzEffect"/> <c>ActivateClass</c> on the target permanent's
        /// <c>OnEnterFieldAnyone</c> duration bucket via <see cref="AddEffectToPermanent"/> (W3 live). The granted
        /// Blitz then fires through the SAME OnEnterFieldAnyone / OnPlay window that collects a printed Blitz
        /// (GetSkillInfos → MultipleSkills): the optional "Will you use Blitz?" opens, and on accept
        /// <c>BlitzProcess</c> opens the immediate effect-driven attack (the RD-CATK-BLITZ re-judgment confirmed
        /// the nested effect-attack DOES open inside the window resolution and resolves into a declared attack via
        /// the deferred <c>EffectDrivenAttack</c> substrate — the SAME path Vortex/Overclock use). NOT the retired
        /// AttackPermanentAction memory-pass phase gate. ADAPTATION (substrate only, matching
        /// <see cref="GainRaid"/>): the AS-IS trailing <c>CreateBuffEffect</c> VFX loop (Effects.cs:1433) and its
        /// <c>CanNotBeAffected</c> guard (Blitz.cs:88-91, gates ONLY the VFX) are stripped (pure UI, no state);
        /// the coroutine becomes a completed <see cref="Task"/>. The <paramref name="isWhenDigivolving"/> flag is
        /// threaded 1:1 into <c>BlitzEffect</c> (it selects the CanTrigger gate — WhenDigivolving vs OnPlay — and
        /// the effect description text).</summary>
        public static async Task GainBlitz(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass, bool isWhenDigivolving)
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

            ActivateClass blitz = CardEffectFactory.BlitzEffect(
                targetPermanent: targetPermanent,
                isInheritedEffect: false,
                condition: CanUseCondition,
                isWhenDigivolving: isWhenDigivolving,
                rootCardEffect: activateClass,
                card: targetPermanent.TopCard);

            AddEffectToPermanent(
                targetPermanent: targetPermanent,
                effectDuration: effectDuration,
                card: card,
                cardEffect: blitz,
                timing: EffectTiming.OnEnterFieldAnyone);

            // AS-IS :88-91 CreateBuffEffect (pure VFX/SE, Effects.cs:1433) + its CanNotBeAffected VFX guard — stripped.
            await Task.CompletedTask;
        }

        #endregion
    }
}
