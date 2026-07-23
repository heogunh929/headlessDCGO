// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/ArtsDigivolve.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS ArtsDigivolve.cs factory partial.
// Returns the OptionResolutionClass kind-class.
//
// RD-P6C2-10 RESOLUTION (was the P4/P6C2 deferred STOP): the resolution path now lands on the mirror frame
// surface built for this goal — CardSource.CanPlayCardTargetFrame (CardSource.cs, the AS-IS :1116-1194 seat)
// over Permanent.PermanentFrame (Permanent.cs, FrameID = field-list index ADAPTATION, RD-P6C1-1/RD-P6C1-2) —
// and the existing Hashtable-ctor PlayCardClass (CardController.cs:2301). SelectPermanentEffect /
// SelectCardEffect are the mirror types; GManager.instance.GetComponent<SelectPermanentEffect>() is the
// established select idiom (BT25_104 Main). Substrate translations (see file header conventions):
// IEnumerator -> async Task, ContinuousController.StartCoroutine(X) -> await X; SelectPermanentEffect's
// id-based canTargetCondition -> PermanentOf(id) adapter (BT19_091 / BT25_104 idiom); AS-IS global
// HasMatchConditionPermanent(pred, true) -> HasMatchConditionPermanent(card, pred, true) (the mirror overload
// scans all players + breeding identically — CardEffectCommons.cs:4079/EnumerateFieldPermanentViews — and the
// predicate's own IsOwnerPermanent term makes the card-context arg scope-neutral).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // kind-class layer
using HeadlessDCGO.Engine.Headless.Bridge;    // GManager
using HeadlessDCGO.Engine.Headless.Services;  // HeadlessEntityId, CardInstanceRecord

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

        bool CanResolveCondition(CardSource optionCard) =>
            CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition, true);

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            return CardEffectCommons.IsOwnerPermanent(permanent, card)
                && permanent.IsDigimon
                && card.CanPlayCardTargetFrame(permanent.PermanentFrame, false, artsDigivolutionClass, SelectCardEffect.Root.Execution);
        }

        async Task ResolutionCoroutine(CardSource optionCard)
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

            await selectPermanentEffect.Activate();

            async Task SelectPermanentCoroutine(Permanent permanent)
            {
                await new PlayCardClass(
                    cardSources: new List<CardSource>() { card },
                    hashtable: null,
                    payCost: false,
                    targetPermanent: permanent,
                    isTapped: false,
                    root: SelectCardEffect.Root.Execution,
                    activateETB: true).PlayCard();
            }
        }
    }
    #endregion
}
