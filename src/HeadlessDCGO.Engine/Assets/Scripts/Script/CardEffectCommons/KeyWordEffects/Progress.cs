
// (P6 cluster2, purely additive — see file header) old-model CardEffectCommons sibling (KeyWordEffects/Progress.cs)
// — a different namespace/type than the KeywordBaseBatch2Effect resolver above.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Collections;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

    public static partial class CardEffectCommons
    {
        /// <summary>(P6 cluster2) AS-IS <c>CanActivateProgress</c> (KeyWordEffects/Progress.cs:44, verbatim).</summary>
        public static bool CanActivateProgress(CardSource cardSource) =>
            IsExistOnBattleAreaDigimon(cardSource)
            && GManager.instance!.attackProcess.IsAttacking
            && GManager.instance!.attackProcess.AttackingPermanent?.InstanceId == ICardEffect.ResolvePermanentOfThisCard(cardSource)?.InstanceId;

        /// <summary>(R3-W3b / R2-A) AS-IS <c>ProgressProcess</c> (KeyWordEffects/Progress.cs:62), 1:1: while this
        /// Digimon attacks, append an UntilEndAttack "Isn't affected by opponent's Digimon's effect"
        /// <see cref="CanNotAffectedClass"/> to the attacker's <c>UntilEndAttackEffects</c> bucket. RD-R2-01 is
        /// RESOLVED — the W3 bucket is live (ExecuteProcess uses it) and read by <c>CardSource.CanNotBeAffected</c>
        /// (Permanent.EffectList(None) scan) — so the stale STOP is retired. ADAPTATIONS (established mirror forms):
        /// AS-IS terminal <c>CreateBuffEffect</c> (pure VFX) stripped; <c>cardSource.PermanentOfThisCard()</c> →
        /// <c>ICardEffect.ResolvePermanentOfThisCard</c> (nullable, guarded); <c>IsOpponentEffect(cardEffect,…)</c> →
        /// <c>IsOpponentEffect(cardEffect.EffectSourceCard,…)</c> (the ICardEffect arg folded to its EffectSourceCard,
        /// as CardEffectFactory/KeyWordEffects/Progress.cs already does); the AS-IS <c>IEnumerator</c> body has no real
        /// awaits once VFX is stripped → returns <c>Task.CompletedTask</c>. The live Progress immunity is ALSO applied
        /// independently by <see cref="Headless.Runtime.ProgressImmunity"/> at attack declaration (this method has no
        /// live caller today — AS-IS likewise has none — so behaviour is unchanged).</summary>
        public static Task ProgressProcess(CardSource cardSource, ICardEffect activateClass, Func<Task> beforeOnAttackCoroutine = null)
        {
            Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);

            if (selectedPermanent != null && CanActivateProgress(cardSource))
            {
                CanNotAffectedClass canNotAffectedClass = new CanNotAffectedClass();
                canNotAffectedClass.SetUpICardEffect("Isn't affected by opponent's Digimon's effect", CanUseCondition1, cardSource);
                canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
                selectedPermanent.UntilEndAttackEffects.Add((_timing) => canNotAffectedClass);

                // AS-IS :73 CreateBuffEffect (pure VFX/SE) — stripped.

                bool CanUseCondition1(Hashtable hashtable)
                {
                    return IsPermanentExistsOnBattleArea(selectedPermanent);
                }

                bool CardCondition(CardSource innerCardSource)
                {
                    if (IsPermanentExistsOnBattleArea(selectedPermanent))
                    {
                        if (innerCardSource == selectedPermanent.TopCard)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                bool SkillCondition(ICardEffect cardEffect)
                {
                    if (cardEffect != null)
                    {
                        if (cardEffect.EffectSourceCard != null)
                        {
                            if (IsOpponentEffect(cardEffect.EffectSourceCard, cardSource))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }

            return Task.CompletedTask;
        }
    }
}
