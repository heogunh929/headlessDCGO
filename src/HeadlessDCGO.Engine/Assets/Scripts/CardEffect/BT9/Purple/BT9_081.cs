// Source: Assets/Scripts/CardEffect/BT9/Purple/BT9_081.cs — 1:1 headless mirror.
// Death-X-DORUgamon (Dorugoramon line, [Dex]/[DeathX]).
//   [None]  AddSelfDigivolutionRequirementStaticEffect: may digivolve from any permanent with [Dorugoramon]
//           in its top card for cost 2 (ignoreDigivolutionRequirement:false).
//   [When Digivolving] (registered at OnEnterFieldAnyone, CanUse = CanTriggerWhenDigivolving) If this Digimon
//           has [Dorugoramon] in its digivolution cards OR is digivolving from the trash, delete all of your
//           opponent's Digimon with the lowest level.
//   [On Deletion] (OnDestroyedAnyone) — the C-4 WITNESS. You may play 1 purple or black level-3 Digimon from
//           your trash without paying its memory cost. If you have 5+ cards with [Dex] or [DeathX] in their
//           names in your trash, you may play 1 [DeathXmon] from your trash for free INSTEAD. The 5+ count is
//           read LIVE at resolution (CanSelectCardCondition), so it MUST be post-trash — when BT9_081 dies by
//           battle its own digivolution sources + top card are trashed BEFORE this window resolves (AS-IS
//           DestroyPermanentsClass stacks OnDestroyedAnyone, then DiscardEvoRoots + AddTrashCard, then the main
//           autoProcessing resolves it — post-trash). Headless mirrors this: the [On Deletion] activated bridge
//           fires from the card's field->Trash CardMoved event, drained AFTER the battle finalize trashes the
//           sources + top, so the trash count includes this card's own [Dex]/[DeathX] sources.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_081 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            // AS-IS: AddSelfDigivolutionRequirementStaticEffect(permanentCondition, digivolutionCost:2,
            // ignoreDigivolutionRequirement:false, card, condition:null). PermanentCondition = the digivolving-FROM
            // permanent's top card is named [Dorugoramon].
            bool PermanentCondition(Permanent targetPermanent) =>
                targetPermanent.TopCard.CardNames.Contains("Dorugoramon");

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 2,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            HeadlessPlayerId enemy = CardEffectCommons.OpponentOf(card);

            // AS-IS PermanentCondition = IsMinLevel(permanent, card.Owner.Enemy) — an opponent battle-area Digimon
            // whose level is the minimum among the opponent's Digimon.
            bool EnemyLowestLevel(Permanent permanent) => CardEffectCommons.IsMinLevel(permanent, enemy);

            // AS-IS RootCondition (digivolving FROM the trash).
            bool RootCondition(ChoiceZone root) => root == ChoiceZone.Trash;

            // AS-IS: card.PermanentOfThisCard().DigivolutionCards.Count(cs => cs.CardNames.Contains("Dorugoramon")).
            int DorugoramonSourceCount() =>
                new Permanent(card.Context, card.InstanceId, card.Owner).DigivolutionCards
                    .Count(cs => cs.CardNames.Contains("Dorugoramon"));

            // (C-3 재상환 P1-D) AS-IS puts the WHOLE OR — "[Dorugoramon] in sources OR digivolving from the
            // trash" — in CanActivateCondition (BT9_081.cs:52-71), re-evaluated PER PASS on the stacked skill.
            // The board half (Dorugoramon-in-sources) must therefore re-read the LIVE stack each pass (a source
            // trashed/added inside the window flips it), while the event half (from-trash root) reads the
            // driving digivolve event — unavailable to the per-pass Func<bool> — so it is evaluated once at
            // collect (canUse, which carries the ctx) and LATCHED on the card instance. The latch is overwritten
            // on EVERY collect-time evaluation of this effect (each digivolve event re-runs canUse before any
            // per-pass gate can read it), so a stale value never outlives the window that wrote it.
            const string fromTrashLatchKey = "BT9_081.whenDigivolvingFromTrash";

            bool ReadFromTrashLatch() =>
                card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out var inst) && inst is not null
                && inst.Metadata.TryGetValue(fromTrashLatchKey, out object? raw) && raw is true;

            void WriteFromTrashLatch(bool value)
            {
                if (card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out var inst) && inst is not null)
                {
                    card.Context.CardInstanceRepository.Upsert(inst with
                    {
                        Metadata = new Dictionary<string, object?>(inst.Metadata, StringComparer.Ordinal)
                        {
                            [fromTrashLatchKey] = value,
                        }
                    });
                }
            }

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                // AS-IS CanUseCondition = CanTriggerWhenDigivolving(hashtable, card) ONLY — the OR-gate is NOT
                // part of the collect gate (a skill with neither half true still stacks AS-IS and can turn
                // activatable mid-window). The event half of the OR is computed here (the only place the driving
                // event is visible) and latched for the per-pass gate.
                canUse: ctx =>
                {
                    if (!CardEffectCommons.CanTriggerWhenDigivolving(ctx, card))
                    {
                        return false;
                    }

                    WriteFromTrashLatch(CardEffectCommons.CanTriggerWhenDigivolving(ctx, card, RootCondition));
                    return true;
                },
                // AS-IS CanActivateCondition (per-pass): this card is on the battle area AND some opponent
                // Digimon is at the minimum level AND ([Dorugoramon] in the LIVE digivolution cards OR the
                // latched from-trash root). The stack read happens here, per pass — board changes inside the
                // window (a Dorugoramon source trashed or added) flip the gate exactly as AS-IS.
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, EnemyLowestLevel)
                    && (DorugoramonSourceCount() >= 1 || ReadFromTrashLatch()),
                // AS-IS ActivateCoroutine: DestroyPermanentsClass over enemy.GetBattleAreaDigimons().Filter(
                // IsMinLevel) — a mandatory, no-select delete of EVERY opponent lowest-level Digimon.
                body: new ApplyToAllMatchingBody(
                    match: id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                        && CardEffectCommons.IsMinLevel(new Permanent(card.Context, id, enemy), enemy),
                    perTarget: (c, sink, id) => CardEffectCommons.DestroyPermanent(sink, c, id)),
                maxCountPerTurn: null, // AS-IS ORDER = -1.
                isOptional: false,     // AS-IS ISOPTIONAL = false.
                description: "[When Digivolving] If this Digimon has [Dorugoramon] in its digivolution cards or is digivolving from the trash, delete all of your opponent's Digimon with the lowest level."));
        }

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            // AS-IS: card.Owner.TrashCards.Count(cs => ContainsCardName("Dex") || ContainsCardName("DeathX")) >= 5.
            bool DexDeathXCountAtLeast5() =>
                CardEffectCommons.MatchConditionOwnersCardCountInTrash(
                    card, cs => cs.ContainsCardName("Dex") || cs.ContainsCardName("DeathX")) >= 5;

            // AS-IS CanSelectCardCondition (BT9_081.cs): CanPlayAsNewPermanent(payCost:false) gates both branches;
            // then (purple|black && level 3 && HasLevel && IsDigimon) OR (5+ [Dex]/[DeathX] in trash && name
            // contains [DeathXmon]).
            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (!CardEffectCommons.CanPlayAsNewPermanent(cardSource, payCost: false, cardEffect: null))
                {
                    return false;
                }

                if ((cardSource.HasCardColor("Purple") || cardSource.HasCardColor("Black"))
                    && cardSource.Level == 3
                    && cardSource.HasLevel
                    && cardSource.IsDigimon)
                {
                    return true;
                }

                if (DexDeathXCountAtLeast5() && cardSource.CardNames.Contains("DeathXmon"))
                {
                    return true;
                }

                return false;
            }

            bool CanTarget(HeadlessEntityId id) =>
                CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner));

            const string desc = "[On Deletion] You may play 1 purple or black level 3 Digimon card from your trash without paying its memory cost. If you have 5 or more cards with [Dex] or [DeathX] in their names in your trash, you may play 1 [DeathXmon] from your trash without paying its memory cost instead.";

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDestroyedAnyone,
                // AS-IS CanUseCondition = CanTriggerOnDeletion(hashtable, card) — self-scoped (this card was deleted).
                canUse: ctx => CardEffectCommons.CanTriggerOnDeletion(ctx, card),
                // AS-IS CanActivateCondition = IsExistOnTrash(card) && HasMatchConditionOwnersCardInTrash(predicate).
                canActivate: () => CardEffectCommons.IsExistOnTrash(card)
                    && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition),
                // AS-IS ActivateCoroutine: SelectCardEffect(maxCount = min(1, matching), canNoSelect:()=>true, root:
                // Trash) then PlayPermanentCards(payCost:false). maxCount:1; the "may" (AS-IS canNoSelect + ISOPTIONAL
                // true) is folded into the body's canEndNotMax:true (B-5 precedent, BT2_080) — select 0 or 1.
                body: new ActivatedSelectAndPlayFromZonesEffect(
                    card,
                    fromZones: new[] { ChoiceZone.Trash },
                    canTarget: CanTarget,
                    maxCount: 1,
                    canEndNotMax: true,
                    description: desc),
                maxCountPerTurn: null, // AS-IS ORDER = -1.
                isOptional: false,     // (B-5) AS-IS ISOPTIONAL = true folded into the body canEndNotMax:true.
                description: desc));
        }

        return cardEffects;
    }
}
