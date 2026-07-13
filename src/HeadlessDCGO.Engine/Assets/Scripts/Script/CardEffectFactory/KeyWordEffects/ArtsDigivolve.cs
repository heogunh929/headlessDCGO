// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/ArtsDigivolve.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS ArtsDigivolve.cs factory partial.
// Returns the OptionResolutionClass kind-class. Replaces the monolith's old invented ArtsDigivolveSelfEffect-based
// ArtsDigivolveEffect.
// VERBATIM-MISSING infra (kept AS-IS per standing no-simplification directive; logged in
// docs/audit/rebuild_p4_factory_missing.md): OptionResolutionClass / SetUpOptionResolutionClass,
// ContinuousController, PlayCardClass. SelectPermanentEffect / SelectCardEffect are the mirror types.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // kind-class layer

public partial class CardEffectFactory
{
    #region Processing of [Arts Digivolve]
    public static OptionResolutionClass ArtsDigivolveEffect(CardSource card)
    {
        if (card == null) return null;
        OptionResolutionClass artsDigivolutionClass = new();
        artsDigivolutionClass.SetUpICardEffect("Arts Digivolve", CanUseCondition, card);
        artsDigivolutionClass.SetUpOptionResolutionClass(ResolutionCoroutine, CanResolveCondition);
        return artsDigivolutionClass;

        bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnExecutingArea(card);

        bool CanResolveCondition(CardSource optionCard) => CardEffectCommons.HasMatchConditionPermanent(CanSelectPermanentCondition, true);

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            return CardEffectCommons.IsOwnerPermanent(permanent, card)
                && permanent.IsDigimon
                && card.CanPlayCardTargetFrame(permanent.PermanentFrame, false, artsDigivolutionClass, SelectCardEffect.Root.Execution);
        }

        IEnumerator ResolutionCoroutine(CardSource optionCard)
        {
            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanentCondition,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: 1,
                canNoSelect: true,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: artsDigivolutionClass);

            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to Arts Digivolve.", "The opponent is selecting 1 Digimon to Arts Digivolve.");

            yield return ContinuousController.instance.StartCoroutine(selectPermanentEffect.Activate());

            IEnumerator SelectPermanentCoroutine(Permanent permanent)
            {
                yield return ContinuousController.instance.StartCoroutine(new PlayCardClass(
                    cardSources: new List<CardSource>() { card },
                    hashtable: null,
                    payCost: false,
                    targetPermanent: permanent,
                    isTapped: false,
                    root: SelectCardEffect.Root.Execution,
                    activateETB: true).PlayCard());
            }
        }
    }
    #endregion
}
