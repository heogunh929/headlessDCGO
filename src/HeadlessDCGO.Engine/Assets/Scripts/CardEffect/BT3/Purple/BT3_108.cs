// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_108.cs
// 1:1 mirror of the original BT3_108 (BT3/Purple), an Option.
//   [Main] 1 of your Digimon gains <Retaliation> until the end of your opponent's next turn.
//   AS-IS: SelectPermanentEffect Mode.Custom over CanSelectPermanentCondition =
//   IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card), maxCount = Min(1, matchCount),
//   SelectPermanentCoroutine = CardEffectCommons.GainRetaliation(permanent, EffectDuration.
//   UntilOpponentTurnEnd, activateClass). Headless: SelectBody's onEachSelected hook is exactly the
//   documented "grant the picked Digimon a keyword" follow-up shape (ActivatedEffect.cs SelectBody doc:
//   "e.g. grant the picked Digimon a keyword ... via CardEffectCommons.GainBlocker / ..."), so this wires
//   CardEffectCommons.GainRetaliation directly per selected id.
//
//   [Security] Add this card to its owner's hand.
//   -> new AddThisCardToHandEffect(card, description) (mirrors ST3_14 / ST3_04's security-skill shape).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class BT3_108 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            void OnEachSelected(HeadlessEntityId id) =>
                CardEffectCommons.GainRetaliation(new Permanent(card.Context, id, card.Owner), EffectDuration.UntilOpponentTurnEnd, card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition),
                body: new SelectBody(
                    card,
                    CanSelectPermanentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    SelectPermanentEffect.Mode.Custom,
                    "Select 1 Digimon to gain Retaliation.",
                    onEachSelected: OnEachSelected),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] 1 of your Digimon gains <Retaliation> (When this Digimon is deleted after losing a battle, delete the Digimon it was battling) until the end of your opponent's next turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
