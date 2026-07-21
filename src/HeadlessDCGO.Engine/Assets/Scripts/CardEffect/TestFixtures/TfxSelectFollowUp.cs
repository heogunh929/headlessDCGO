// TEST FIXTURE (not a real card). [Main] (OptionSkill) exercises select-follow-up primitives, branching on a
// "followMode" metadata flag: "seq" returns TWO effects (select+suspend, then draw) to verify the resolver runs
// a returned effect LIST in order (unconditional chaining); "deck" / "sec" select an opponent Digimon and
// return-to-deck / put-to-security; "addhand" / "trashzone" select a zone card and add-to-hand / trash it.
// Used by tests/PRIM-P0 (Build Order 3). Inert in actual play.
// (R6-Da'-1) Re-written from the retired invented `CardEffectFactory.SelectAnd*` helpers to the literal AS-IS
// inline `new ActivateClass()` idiom the printed-card corpus uses:
//   * permanent-select: `GManager.instance.GetComponent<SelectPermanentEffect>()` + full SetUp + await
//     Activate() — Mode.Tap per ST4_15.cs, Mode.PutLibraryBottom per BT2_102.cs, Mode.PutSecurityTop (same
//     idiom, sibling enum value — ST3_09.cs precedent).
//   * zone-card select: `GManager.instance.GetComponent<SelectCardEffect>()` + full 16-param SetUp + await
//     Activate() — Mode.AddHand/Root.Trash per BT2_090.cs, Mode.Discard (BT10_084.cs) with Root.Library.
// The "seq" draw tail keeps the real factory `CardEffectFactory.DrawCardsEffect` (not a retired helper).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSelectFollowUp : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing != EffectTiming.OptionSkill)
        {
            return effects;
        }

        string mode = card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) &&
                      record is not null && record.Metadata.TryGetValue("followMode", out object? raw) && raw is string s
            ? s
            : "seq";

        bool IsOpponentDigimon(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
        }

        // Inline AS-IS permanent-select ActivateClass (ST4_15/BT2_102 idiom): select up to 1 opponent
        // Digimon and apply selectMode (Tap / PutLibraryBottom / PutSecurityTop).
        ActivateClass PermanentSelect(SelectPermanentEffect.Mode selectMode, string description)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, description);

            return activateClass;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, IsOpponentDigimon))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, IsOpponentDigimon));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOpponentDigimon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: selectMode,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        // Inline AS-IS zone-card-select ActivateClass (BT2_090 idiom): select up to 1 of the owner's cards
        // in fromRoot and apply cardMode (AddHand / Discard).
        ActivateClass ZoneCardSelect(SelectCardEffect.Root fromRoot, SelectCardEffect.Mode cardMode, ChoiceZone fromZone, string description)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, description);

            return activateClass;

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource != null;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                int maxCount = Math.Min(1, ((IZoneStateReader)card.Context.ZoneMover)
                    .GetCards(card.Owner, fromZone)
                    .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner))));

                if (maxCount > 0)
                {
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => false,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        message: description,
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: cardMode,
                        root: fromRoot,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    await selectCardEffect.Activate();
                }
            }
        }

        switch (mode)
        {
            case "seq":
                // Unconditional chain: select+suspend an opponent Digimon, THEN draw 1. Both steps in the list.
                effects.Add(PermanentSelect(SelectPermanentEffect.Mode.Tap, "Suspend 1 of your opponent's Digimon."));
                effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
                break;
            case "deck":
                effects.Add(PermanentSelect(SelectPermanentEffect.Mode.PutLibraryBottom, "Return 1 of your opponent's Digimon to the bottom of the deck."));
                break;
            case "sec":
                effects.Add(PermanentSelect(SelectPermanentEffect.Mode.PutSecurityTop, "Place 1 of your opponent's Digimon on top of security."));
                break;
            case "addhand":
                // Zone-card select: add 1 of the owner's Trash cards to hand.
                effects.Add(ZoneCardSelect(SelectCardEffect.Root.Trash, SelectCardEffect.Mode.AddHand, ChoiceZone.Trash, "Add 1 card from your trash to your hand."));
                break;
            case "trashzone":
                // Zone-card select: trash 1 of the owner's Library cards.
                effects.Add(ZoneCardSelect(SelectCardEffect.Root.Library, SelectCardEffect.Mode.Discard, ChoiceZone.Library, "Trash 1 card from your deck."));
                break;
        }

        return effects;
    }
}
