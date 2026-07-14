// Source: DCGO/Assets/Scripts/CardEffect/BT9/Purple/BT9_081.cs — 1:1 headless mirror.
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of BOTH activated branches
// (OnEnterFieldAnyone [When Digivolving] + OnDestroyedAnyone [On Deletion]). Death-X-DORUgamon ([Dex]/[DeathX]).
//   [None] AddSelfDigivolutionRequirementStaticEffect (Dorugoramon-in-top-card, cost 2) — the real AS-IS factory
//          call, untouched.
//   [When Digivolving] If this Digimon has [Dorugoramon] in its digivolution cards OR is digivolving from the
//          trash, delete all of your opponent's Digimon with the lowest level.
//   [On Deletion] (C-4 WITNESS) You may play 1 purple/black level-3 Digimon from your trash for free; if 5+
//          [Dex]/[DeathX] cards in trash, may play 1 [DeathXmon] instead. The 5+ count is read LIVE at
//          resolution (post-trash), preserved by the [On Deletion] activated bridge draining AFTER the battle
//          finalize trashes this card's own sources + top.
// AS-IS ActivateClass structure kept verbatim (BT9_081.cs:25-183). Because the new-model CanActivateCondition
// ALSO receives the driving-event Hashtable, the previous pass's from-trash LATCH hack is DROPPED: the OR
// (Dorugoramon-in-sources || CanTriggerWhenDigivolving(hashtable, RootCondition)) is evaluated per-pass exactly
// as AS-IS, both halves from the same hashtable. Substrate translations only: IEnumerator->Task,
// StartCoroutine->await; `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
// `card.Owner.Enemy` -> `CardEffectCommons.OpponentOf(card)` (HeadlessPlayerId) whose `GetBattleAreaDigimons()`
// extension + `.Filter(...)` mirror AS-IS; `new DestroyPermanentsClass(list, CardEffectHashtable(activateClass))
// .Destroy()` -> the mirror ctor (keeps the Hashtable cause); `GManager.instance.GetComponent<SelectCardEffect>()`
// + full AS-IS SetUp(Mode.Custom, Root.Trash) + `PlayPermanentCards(...)` AS-IS-signature bridge (BT1_044 idiom);
// `card.Owner.TrashCards` -> `new Player(card.Context, card.Owner).TrashCards`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Purple;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_081 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("Dorugoramon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            HeadlessPlayerId enemy = CardEffectCommons.OpponentOf(card);

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete opponent's all Digimon with the lowest Level", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] If this Digimon has [Dorugoramon] in its digivolution cards or is digivolving from the trash, delete all of your opponent's Digimon with the lowest level.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsMinLevel(permanent, enemy);
            }

            bool RootCondition(SelectCardEffect.Root root)
            {
                return root == SelectCardEffect.Root.Trash;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                    {
                        if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count((cardSource) => cardSource.CardNames.Contains("Dorugoramon")) >= 1)
                        {
                            return true;
                        }

                        if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card, RootCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> destroyTargetPermanents = enemy.GetBattleAreaDigimons().Filter(PermanentCondition);
                await new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
            }
        }

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 Digimon from trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Deletion] You may play 1 purple or black level 3 Digimon card from your trash without paying its memory cost. If you have 5 or more cards with [Dex] or [DeathX] in their names in your trash, you may play 1 [DeathXmon] from your trash without paying its memory cost instead.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    if (cardSource.HasCardColor("Purple") || cardSource.HasCardColor("Black"))
                    {
                        if (cardSource.Level == 3)
                        {
                            if (cardSource.HasLevel)
                            {
                                if (cardSource.IsDigimon)
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    if (new Player(card.Context, card.Owner).TrashCards.Count((trashCard) => trashCard.ContainsCardName("Dex") || trashCard.ContainsCardName("DeathX")) >= 5)
                    {
                        if (cardSource.CardNames.Contains("DeathXmon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, (cardSource) => CanSelectCardCondition(cardSource)))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, (cardSource) => CanSelectCardCondition(cardSource)))
                {
                    int maxCount = Math.Min(1, new Player(card.Context, card.Owner).TrashCards.Count((cardSource) => CanSelectCardCondition(cardSource)));

                    List<CardSource> selectedCards = new List<CardSource>();

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                message: "Select 1 card to play.",
                                maxCount: maxCount,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                mode: SelectCardEffect.Mode.Custom,
                                root: SelectCardEffect.Root.Trash,
                                customRootCardList: null,
                                canLookReverseCard: true,
                                selectPlayer: card.Owner,
                                cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectCardEffect.Activate();

                    async Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        await Task.CompletedTask;
                    }

                    await CardEffectCommons.PlayPermanentCards(cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Trash, activateETB: true);
                }
            }
        }

        return cardEffects;
    }
}
