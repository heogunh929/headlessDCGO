// Source: DCGO/Assets/Scripts/CardEffect/EX8/Black/EX8_051.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the ESS trash-source branch.
// The keyword regions are unchanged verbatim factory mirrors:
//   * <Collision> (OnCounterTiming) -> CollisionSelfStaticEffect(false).
//   * <Piercing> (OnDetermineDoSecurityCheck) -> PierceSelfEffect(isInheritedEffect:false).
//   * <Fragment <3>> (WhenPermanentWouldBeDeleted) -> FragmentSelfEffect(false, trashValue:3).
// ESS "When effects trash this card from digivolution cards of a [Mineral] or [Rock] trait Digimon, <De-Digivolve 1>
//   1 of your opponent's Digimon." — AS-IS `new ActivateClass()` + SetIsInheritedEffect(true) +
//   SetUpActivateClass(..., -1, false, ...) (uncapped, mandatory) (EX8_051.cs:41-102). CanUse = host-trait check
//   (GetPermanentFromHashtable.TopCard has Mineral/Rock) && CanTriggerOnTrashSelfDigivolutionCard(cardEffect != null).
//   CanActivate = IsExistOnTrash && HasMatchConditionOpponentsPermanent(opponent battle-area Digimon). Body =
//   SelectPermanentEffect(Mode.Custom, maxCount:1) -> new IDegeneration(pick, 1).Degeneration().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `GManager.instance.GetComponent<SelectPermanentEffect>()` + full AS-IS SetUp (bridge W4); `new IDegeneration(pick,
// 1, activateClass)` -> the mirror carrier ctor with `activateClass.EffectSourceCard?.InstanceId`; AS-IS
// `Func<Permanent,bool> CanSelectOpponentsDigimon` passed directly to SetUp's canonical Func<Permanent,bool>
// overload (no id adapter).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Black;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_051 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Collision
        if (timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CardEffectFactory.CollisionSelfStaticEffect(false, card, null));
        }
        #endregion

        #region Piercing
        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Fragment
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            string EffectDiscription()
            {
                return "<Fragment <3>> (When this Digimon would be deleted, by trashing any 3 of its digivolution cards, it isn't deleted.)";
            }

            cardEffects.Add(CardEffectFactory.FragmentSelfEffect(isInheritedEffect: false, card: card, condition: null, trashValue: 3, effectName: "Fragment <3>", effectDiscription: EffectDiscription()));
        }
        #endregion

        #region Trash Source - ESS
        if (timing == EffectTiming.OnDigivolutionCardDiscarded)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("De-Digivolve 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When effects trash this card from digivolution cards of a [Mineral] or [Rock] trait Digimon, <De-Digivolve 1> 1 of your opponent's Digimon.";
            }

            bool CanSelectOpponentsDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                Permanent trashedPermanent = CardEffectCommons.GetPermanentFromHashtable(hashtable);
                return (trashedPermanent.TopCard.ContainsTraits("Mineral") || trashedPermanent.TopCard.ContainsTraits("Rock")) &&
                       CardEffectCommons.CanTriggerOnTrashSelfDigivolutionCard(hashtable, cardEffect => cardEffect != null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, CanSelectOpponentsDigimon))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectOpponentsDigimon,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to De-Digivolve.", "The opponent is selecting 1 Digimon to De-Digivolve.");
                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    await new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
