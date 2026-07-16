// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blocker.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Blocker). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Blocker's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch1Effect
    {
        private CardEffectCanResolveResult CanResolveBlocker(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            return CardEffectCanResolveResult.Success("Blocker target can block.", BaseValues(context, target));
        }
    }
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overloads; delegate to the verified substrate
// `GainBlocker` (CardEffectCommons.cs:3405) / `GainBlockerPlayerEffect` (CardEffectCommons.cs:3198). Kept in
// the flat `...Script.CardEffectCommons` namespace (not the nested `.KeyWordEffects` namespace above) so
// these are genuine overloads of the same partial `CardEffectCommons` type every ported card calls —
// per the established convention (see docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainBlocker</c>
        /// (KeyWordEffects/Blocker.cs:10), 1:1: build the target-locked <see cref="CardEffectFactory.BlockerStaticEffect"/>
        /// and store it in the target permanent's <c>None</c> (continuous) duration bucket via
        /// <see cref="AddEffectToPermanent"/> — so <see cref="Permanent.HasBlocker"/>'s <c>EffectList(None)</c>
        /// <c>IBlockerEffect</c> scan reads the granted keyword. Replaces the invented <c>GainKeywordToPermanent</c>
        /// funnel (a <c>ContinuousKeywordGate.Blocker</c> registry marker that the AS-IS getter never reads).
        /// ADAPTATION: AS-IS's terminal <c>CreateBuffEffect</c> (a Unity presentation coroutine) has no headless
        /// substrate — dropped.</summary>
        public static async Task GainBlocker(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            if (targetPermanent == null) return;
            if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool PermanentCondition(Permanent permanent) => permanent == targetPermanent;

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

            BlockerClass blocker = CardEffectFactory.BlockerStaticEffect(
                permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

            AddEffectToPermanent(
                targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
                cardEffect: blocker, timing: EffectTiming.None);

            await Task.CompletedTask;
        }

        /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainBlockerPlayerEffect</c>
        /// (KeyWordEffects/Blocker.cs:46), 1:1: a PLAYER-scope Blocker grant — the target predicate rides the
        /// <see cref="CardEffectFactory.BlockerStaticEffect"/>'s <c>PermanentCondition</c> and the effect is stored in
        /// the owning player's <c>None</c> bucket via <see cref="AddEffectToPlayer"/> (<see cref="Player.EffectList"/>
        /// surfaces it to <see cref="Permanent.HasBlocker"/>'s player scan). ADAPTATION: the AS-IS per-permanent
        /// <c>CreateBuffEffect</c> VFX loop is pure UI — dropped.</summary>
        public static async Task GainBlockerPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)
        {
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool PermanentCondition(Permanent permanent)
            {
                if (IsPermanentExistsOnBattleArea(permanent))
                {
                    if (!permanent.TopCard.CanNotBeAffected(activateClass))
                    {
                        if (permanentCondition == null || permanentCondition(permanent))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition() => true;

            BlockerClass blocker = CardEffectFactory.BlockerStaticEffect(
                permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

            AddEffectToPlayer(effectDuration: effectDuration, card: card, cardEffect: blocker, timing: EffectTiming.None);

            await Task.CompletedTask;
        }
    }
}
