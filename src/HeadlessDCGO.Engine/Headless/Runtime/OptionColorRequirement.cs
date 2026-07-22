namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (RD-2) AS-IS <c>CardSource.MatchColorRequirement</c> (CardSource.cs:255-321): an Option can only be played
/// if EVERY one of its colors is present on some field permanent the owner controls — a Digimon/Tamer in the
/// BATTLE area OR the BREEDING area (AS-IS <c>Owner.GetFieldPermanents()</c> spans both frame parents,
/// Player.cs:19-78) — UNLESS an "ignore color condition" effect (<c>IIgnoreColorConditionEffect</c>) applies.
/// <c>CardSource.CanNotPlayThisOption</c> gates play on <c>!MatchColorRequirement</c>. The headless port had no
/// such gate, so every option was playable regardless of colors in play.
/// </summary>
public static class OptionColorRequirement
{
    /// <summary>True when <paramref name="optionCardId"/> (owned by <paramref name="owner"/>) satisfies its
    /// color requirement, or is exempt via an ignore-color effect.</summary>
    public static bool Matches(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId optionCardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (owner.IsEmpty || optionCardId.IsEmpty)
        {
            return true;
        }

        // (1) AS-IS "ignore color requirement" effects (IIgnoreColorConditionEffect) — scanned over field
        // permanents + players + ITSELF (CardSource.cs:263-303). (④) The legacy key-lowered routes (1a) the
        // registry-backed ApplicableEffects scan and (1b) the option's own dispatch-built continuous scan
        // (CardEffectRegistrar.BuildContinuousRequests) are RETIRED: their only producers were OLD-model ToBinding
        // carriers (now deleted), so both were production-inert. The live path is (1c) — the NEW-model
        // IIgnoreColorConditionEffect kind-class scan, which IS the AS-IS three-region ignore scan verbatim.
        //   (1c) (EXEMPLAR-T1) NEW-model IIgnoreColorConditionEffect kind-classes — a P6-rebuild card's
        //   IgnoreColorConditionClass (e.g. LM_054/BT19_091). The mirror CardSource.IgnoreColorConditionActive IS
        //   the AS-IS three-region ignore scan verbatim (field permanents → players → the card itself,
        //   CardSource.cs:263-303), so this gate consults it directly — same surface
        //   CanNotPlayThisOption/MatchColorRequirement read.
        if (new CardSource(context, optionCardId, owner, owner).IgnoreColorConditionActive())
        {
            return true;
        }

        // (2) every option color must appear on some owner field/breeding permanent's effective colors.
        // (AS-IS colorsToCheck = IsDigimon ? DualCardColors : CardColors, CardSource.cs:307) — a DUAL card
        // (Digimon+Option) played as an option uses its separate OptionCardColorRequirements list, not its
        // printed Digimon colors; a pure option uses CardColors.
        var optionSource = new CardSource(context, optionCardId, owner, owner);
        IReadOnlyList<string> optionColors = IsDualCard(context, optionCardId)
            ? optionSource.DualCardColors
            : optionSource.CardColors;
        if (optionColors.Count == 0)
        {
            return true;   // a colorless option imposes no requirement
        }

        var fieldColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.ZoneMover is IZoneStateReader zones)
        {
            foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea })
            {
                foreach (HeadlessEntityId permanentId in zones.GetCards(owner, zone))
                {
                    // (RD-2 latent-2 / AS-IS permanent.TopCard.IsPermanent, CardSource.cs:311 + CEntity_Base.cs:238)
                    // only a Digimon / Tamer / DigiEgg supplies colors — a field Option (a delay-option permanent)
                    // does NOT. A dual card (Digimon+Option) IS a permanent, so IsCardType reports it correctly.
                    if (!IsColorSupplyingPermanent(context, permanentId))
                    {
                        continue;
                    }

                    foreach (string color in new CardSource(context, permanentId, owner, owner).CardColors)
                    {
                        fieldColors.Add(color);
                    }
                }
            }
        }

        return optionColors.All(color => fieldColors.Contains(color));
    }

    /// <summary>AS-IS <c>CardSource.IsPermanent</c> (CEntity_Base.cs:238): the card kind includes Digimon,
    /// Tamer, OR DigiEgg (a dual card reports true for either of its kinds via <see cref="CardRecord.IsCardType"/>).</summary>
    private static bool IsColorSupplyingPermanent(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null ||
            !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) || card is null)
        {
            return false;
        }

        return card.IsCardType("Digimon") || card.IsCardType("Tamer") || card.IsCardType("DigiEgg");
    }

    /// <summary>AS-IS <c>colorsToCheck = IsDigimon ? DualCardColors : CardColors</c> (CardSource.cs:307): a card
    /// played as an option that is ALSO a Digimon (a dual card) checks its dual (option-requirement) colours.</summary>
    private static bool IsDualCard(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) && instance is not null &&
        context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) && card is not null &&
        card.IsCardType("Digimon");
}
