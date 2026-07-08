// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_090.cs
// 1:1 mirror of the original BT3_090 (BT3/Purple).
//   [When Digivolving] Trash 1 card from the top of both players' security stacks. Then, you may play 1
//   purple or yellow Digimon card with a level of 4 or less from your trash without paying its memory cost.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone gated by CanTriggerWhenDigivolving (headless
//   convention: WhenDigivolving timing), CanActivateCondition = IsExistOnBattleArea(card), ActivateCoroutine
//   = foreach player in Players_ForTurnPlayer (both players): IDestroySecurity(1, fromTop:true); THEN an
//   optional (canNoSelect:true) SelectCardEffect over Root.Trash (Mode.Custom -> PlayPermanentCards).
//   The 3 steps have no cross-dependency (security zones are independent of the trash-play candidate
//   pool), so they are faithfully decomposed into 3 independently-gated registrations under the SAME
//   WhenDigivolving timing, all sharing the identical CanUse/CanActivate gate AS-IS uses for the single
//   bundled coroutine:
//     1) TrashSecurityBody(card.Owner, 1, fromTop:true)      -- mandatory, both players' security top 1.
//     2) TrashSecurityBody(OpponentOf(card), 1, fromTop:true)
//     3) CardEffectFactory.SelectAndPlayFromZoneEffect(card, ChoiceZone.Trash, canTarget, maxCount:1,
//        canEndNotMax:true, description) -- optional (canNoSelect:true AS-IS -> canEndNotMax:true here),
//        cost-free (SelectAndPlayFromZoneEffect never charges memory), matches AS-IS payCost:false.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_090 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanActivate() => CardEffectCommons.IsExistOnBattleArea(card);
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerWhenDigivolving(ctx, card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenDigivolving,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new TrashSecurityBody(card.Owner, count: 1, fromTop: true),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Digivolving] Trash 1 card from the top of your security stack."));

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenDigivolving,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new TrashSecurityBody(CardEffectCommons.OpponentOf(card), count: 1, fromTop: true),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Digivolving] Trash 1 card from the top of your opponent's security stack."));

            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                if (!candidate.IsDigimon)
                {
                    return false;
                }

                if (!CardEffectCommons.CanPlayAsNewPermanent(candidate, payCost: false, cardEffect: null))
                {
                    return false;
                }

                if (!candidate.HasLevel || candidate.Level > 4)
                {
                    return false;
                }

                return candidate.HasCardColor("Purple") || candidate.HasCardColor("Yellow");
            }

            cardEffects.Add(CardEffectFactory.SelectAndPlayFromZoneEffect(
                card,
                fromZone: ChoiceZone.Trash,
                canTarget: CanSelectCardCondition,
                maxCount: 1,
                canEndNotMax: true,
                description: "[When Digivolving] You may play 1 purple or yellow Digimon card with a level of 4 or less from your trash without paying its memory cost."));
        }

        return cardEffects;
    }
}
