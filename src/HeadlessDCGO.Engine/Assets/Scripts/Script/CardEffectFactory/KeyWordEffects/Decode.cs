// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Decode.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Decode.cs factory partial.
// AS-IS Decode.cs declares NO auxiliary type. The MIRROR-invented `static class Decode` (namespace
// ...CardEffectFactory.KeyWordEffects) carries a const `DecodeSourceConditionKey` that HAS external consumers
// (DeletionReplacementTiming.cs, CardLeavePlayCleanup.cs, referenced as KeyWordEffects.Decode.DecodeSourceConditionKey),
// so that const holder is PRESERVED (its old `.Create` — zero consumers — is dropped). The ported factory
// methods go in `partial class CardEffectFactory` under ...CardEffectCommons (two block namespaces in one file).
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects
{
    /// <summary>(PRESERVED — external consumers of <c>DecodeSourceConditionKey</c>) Binding-values key carrying the
    /// AS-IS <c>DecodeSelfEffect</c> per-card <c>sourceCondition</c> predicate. Read live by the PRE candidate
    /// filter (DeletionReplacementTiming / CardLeavePlayCleanup). Null = any Digimon source.</summary>
    public static class Decode
    {
        public const string DecodeSourceConditionKey = "decode.sourceCondition";
    }
}

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Collections;
    using System;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

    public partial class CardEffectFactory
    {
        #region Trigger effect of [Decode] on oneself

        public static ActivateClass DecodeSelfEffect(CardSource card, bool isInheritedEffect, string[] decodeStrings, Func<CardSource, bool> sourceCondition, Func<bool> condition,
            ICardEffect rootCardEffect = null)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            bool CanUseCondition()
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       (condition == null || condition());
            }
            if (sourceCondition == null) sourceCondition = _ => true;

            return DecodeEffect(targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect, decodeStrings,
                condition: CanUseCondition, sourceCondition: sourceCondition, rootCardEffect: rootCardEffect, card);
        }

        #endregion

        #region Trigger effect of [Decode]

        public static ActivateClass DecodeEffect(Permanent targetPermanent, bool isInheritedEffect, string[] decodeStrings,
            Func<bool> condition, Func<CardSource, bool> sourceCondition, ICardEffect rootCardEffect, CardSource card)
        {
            if (targetPermanent == null) return null;
            if (targetPermanent.TopCard == null) return null;
            if (card == null) return null;
            if (sourceCondition == null) sourceCondition = _ => true;

            // EX: "Decode (Red/Black)"
            string effectname = $"Decode {decodeStrings[0]}";

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(effectname, CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.DecodeEffectDiscription(decodeStrings));
            activateClass.SetIsInheritedEffect(isInheritedEffect);

            if (rootCardEffect != null)
            {
                activateClass.SetIsInheritedEffect(false);
                activateClass.SetEffectSourcePermanent(targetPermanent);
                activateClass.SetRootCardEffect(rootCardEffect);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card) &&
                       !CardEffectCommons.IsByBattle(hashtable);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateDecode(targetPermanent.TopCard, sourceCondition, activateClass) &&
                       (condition == null || condition());
            }

            Task ActivateCoroutine(Hashtable hashtable)
            {
                return CardEffectCommons.DecodeProcess(targetPermanent.TopCard, sourceCondition, decodeStrings, activateClass);
            }

            return activateClass;
        }

        #endregion
    }
}
