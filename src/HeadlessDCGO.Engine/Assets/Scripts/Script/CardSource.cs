namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// Headless mirror of the original <c>CardSource</c> — the handle a card-effect builder receives. Carries
/// the live instance id, the controlling / owning player, and the live <see cref="EngineContext"/> so
/// condition predicates can read turn / zone / stack state.
/// </summary>
public sealed class CardSource
{
    public CardSource(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller, HeadlessPlayerId? owner = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty)
        {
            throw new ArgumentException("Card source instance id must not be empty.", nameof(instanceId));
        }

        if (controller.IsEmpty)
        {
            throw new ArgumentException("Card source controller id must not be empty.", nameof(controller));
        }

        Context = context;
        InstanceId = instanceId;
        Controller = controller;
        Owner = owner ?? controller;
    }

    public EngineContext Context { get; }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId Controller { get; }

    public HeadlessPlayerId Owner { get; }

    // (EFFECT-MODEL REBUILD, design item CARDSOURCE-EQUALITY) AS-IS relies on stable per-card object identity for
    // `==`/`!=`/`Contains` on CardSource (CanActivate's inherited/linked determination, IsSameEffect, factory
    // PermanentCondition `permanent == targetPermanent`, LinkedCards.Contains(card), …). The mirror CardSource is a
    // lightweight VIEW reconstructed on every access, so without value equality every such comparison is
    // reference-unequal and silently wrong (a bug the big-bang red window hides). Identity = the card INSTANCE
    // (InstanceId) within the same match (Context) — two views of the same live card compare equal.
    public override bool Equals(object? obj) =>
        obj is CardSource other && InstanceId.Equals(other.InstanceId) && ReferenceEquals(Context, other.Context);

    public override int GetHashCode() => InstanceId.GetHashCode();

    public static bool operator ==(CardSource? left, CardSource? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(CardSource? left, CardSource? right) => !(left == right);

    /// <summary>(EFFECT-MODEL REBUILD / FOUNDATION) AS-IS <c>CardSource.cEntity_EffectController</c>
    /// (CardSource.cs:50, a plain public field on the original Unity Component) — the per-card-instance
    /// use-count / effect-list layer <c>ICardEffect.CanTrigger/CanActivate</c> (Assets/Scripts/Script/
    /// ICardEffect.cs, this goal) reach through. Backed by <see cref="CEntity_EffectControllerStore"/> (see
    /// its header for why a store is needed: this <see cref="CardSource"/> is a view reconstructed on every
    /// access, not a stable per-card object) — always returns the SAME controller for the same
    /// (<see cref="Context"/>, <see cref="InstanceId"/>) pair.</summary>
    public CEntity_EffectController cEntity_EffectController =>
        CEntity_EffectControllerStore.GetOrCreate(Context, InstanceId);

    /// <summary>Mirror of <c>CardSource.PermanentOfThisCard()</c>: the permanent (stack) this card is part
    /// of, whether it is the top card or a buried digivolution source. Empty if the card is not in a
    /// battle-area permanent.</summary>
    public PermanentView PermanentOfThisCard()
    {
        var zones = (IZoneStateReader)Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(Owner, ChoiceZone.BattleArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(Context.CardInstanceRepository, Context.CardRepository, top);
            if (top == InstanceId || stack.UnderCards.Any(under => under.InstanceId == InstanceId))
            {
                return new PermanentView(stack);
            }
        }

        return new PermanentView(DigivolutionStack.Empty);
    }

    // ===== (EFFECT-MODEL REBUILD / P2) AS-IS CardSource.EffectList family (CardSource.cs:981-1035) ==========
    // Thin delegation to the per-card-instance controller (cEntity_EffectController.GetCardEffects, ported P1) +
    // the ported IEnumerableExtension.Filter. 1:1 with AS-IS. Every returned effect with no source card is
    // stamped with THIS card (AS-IS's SetEffectSourceCard back-fill).

    /// <summary>AS-IS <c>CardSource.EffectList(EffectTiming)</c> (CardSource.cs:981).</summary>
    public List<ICardEffect> EffectList(EffectTiming timing)
    {
        return EffectList_ForCard(timing, this);
    }

    /// <summary>AS-IS <c>CardSource.EffectList_ForCard(EffectTiming, CardSource)</c> (CardSource.cs:990).</summary>
    public List<ICardEffect> EffectList_ForCard(EffectTiming timing, CardSource cardSource)
    {
        List<ICardEffect> _EffectList = cEntity_EffectController
            .GetCardEffects(timing, cardSource)
            .Filter(cardEffect => cardEffect != null);

        foreach (ICardEffect cardEffect in _EffectList)
        {
            if (cardEffect.EffectSourceCard == null)
            {
                cardEffect.SetEffectSourceCard(this);
            }
        }

        return _EffectList;
    }

    /// <summary>AS-IS <c>CardSource.EffectList_ExceptAddedEffects(EffectTiming)</c> (CardSource.cs:1011).</summary>
    public List<ICardEffect> EffectList_ExceptAddedEffects(EffectTiming timing)
    {
        return EffectList_ForCard_ExceptAddedEffects(timing, this);
    }

    /// <summary>AS-IS <c>CardSource.EffectList_ForCard_ExceptAddedEffects(EffectTiming, CardSource)</c> (CardSource.cs:1020).</summary>
    public List<ICardEffect> EffectList_ForCard_ExceptAddedEffects(EffectTiming timing, CardSource cardSource)
    {
        List<ICardEffect> _EffectList = cEntity_EffectController
            .GetCardEffects_ExceptAddedEffects(timing, cardSource)
            .Filter(cardEffect => cardEffect != null);

        foreach (ICardEffect cardEffect in _EffectList)
        {
            if (cardEffect.EffectSourceCard == null)
            {
                cardEffect.SetEffectSourceCard(this);
            }
        }

        return _EffectList;
    }

    // ===== (PRIM-W5-0) card-query view — the member surface card predicates read =====================
    // Backed by the definition CardRecord (colors/level/traits/type) + instance metadata. Enables 1:1
    // mirror of the original `cardSource.<X>` / `permanent.TopCard.<X>` predicates.

    private CardRecord? Definition =>
        Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? inst) && inst is not null
            && Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) ? def
            : (Context.CardRepository.TryGetCard(InstanceId, out CardRecord? self) ? self : null);

    private static IReadOnlyList<string> ReadStrings(IReadOnlyDictionary<string, object?>? meta, string key)
    {
        if (meta is null || !meta.TryGetValue(key, out object? raw) || raw is null) return Array.Empty<string>();
        return raw switch
        {
            IEnumerable<string> ss => ss.ToArray(),
            string s => s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => Array.Empty<string>(),
        };
    }

    // ===== (P6C3, COLOR-MODEL-DUALITY reconciliation) string <-> CardColor conversion =====================
    // The mirror colour accessors below keep their established STRING signatures (their consumer corpus:
    // OptionColorRequirement, BT2_099, CardEffectCommons colour predicates), while the fold now runs through
    // the AS-IS-typed kind-class interfaces (IChange(Base)CardColorEffect transform List<CardColor>). The
    // enum is closed (CEntity_Base.cs CardColor) and the mirror's string values are exactly the enum names,
    // so the conversion is lossless; an unparseable string (possible only in hand-written fixtures) is
    // dropped from the enum view rather than guessed.

    /// <summary>(P6C3) The AS-IS-typed view of a colour-name list (shared with HashtableSetting's
    /// "CardColors" payload and AddDigivolutionRequirement's enum comparison).</summary>
    public static List<CardColor> ToCardColorList(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var colors = new List<CardColor>();
        foreach (string name in names)
        {
            if (Enum.TryParse(name, ignoreCase: true, out CardColor color))
            {
                colors.Add(color);
            }
        }

        return colors;
    }

    /// <summary>(P6C3) The mirror string view of an AS-IS colour list.</summary>
    public static List<string> ToColorNames(IEnumerable<CardColor> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return colors.Select(c => c.ToString()).ToList();
    }

    // (P6C3) AS-IS colour fold shape (CardSource.cs:364-401 BaseCardColors / :446-483 CardColors): the
    // effects of ITSELF apply only while the card is NOT a permanent (`PermanentOfThisCard() == null`),
    // then the effects of all field permanents apply (gameContext.Players_ForTurnPlayer scan). Substrate:
    // `new GameContext(Context)` is the same per-match view GManager.instance.turnStateMachine.gameContext
    // resolves to.
    private List<CardColor> FoldColorEffects<TInterface>(List<CardColor> colors, Func<TInterface, List<CardColor>, List<CardColor>> apply)
        where TInterface : class
    {
        if (PermanentOfThisCard().IsEmpty)
        {
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is TInterface transform && cardEffect.CanUse(null))
                {
                    colors = apply(transform, colors);
                }
            }
        }

        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is TInterface transform && cardEffect.CanUse(null))
                    {
                        colors = apply(transform, colors);
                    }
                }
            }
        }

        return colors;
    }

    /// <summary>(A3 / P6C3 re-fold) The card's BASE colors (mirror of <c>BaseCardColors</c>,
    /// CardSource.cs:364-401): printed colors transformed by every active
    /// <see cref="IChangeBaseCardColorEffect"/> (self while not in play + all field permanents), Distinct.</summary>
    public IReadOnlyList<string> BaseCardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(ReadStrings(Definition?.Metadata, "colors"));
            colors = FoldColorEffects<IChangeBaseCardColorEffect>(colors, (e, c) => e.GetBaseCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(A3 / P6C3 re-fold) The card's colors (mirror of <c>CardColors</c>, CardSource.cs:446-483):
    /// seeds from the fully-resolved <see cref="BaseCardColors"/> (base-change BEFORE change, AS-IS two-stage
    /// order), then every active <see cref="IChangeCardColorEffect"/> transforms the list, Distinct.</summary>
    public IReadOnlyList<string> CardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(BaseCardColors);
            colors = FoldColorEffects<IChangeCardColorEffect>(colors, (e, c) => e.GetCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(RD-2 latent-1 / AS-IS metadata key for a DUAL card's separate option colour requirement list —
    /// <c>CEntity_Base.OptionCardColorRequirements</c>, CEntity_Base.cs:41.)</summary>
    public const string OptionColorRequirementsKey = "optionColorRequirements";

    /// <summary>(RD-2 latent-1) The card's BASE dual colours (mirror of <c>BaseDualCardColors</c>,
    /// CardSource.cs:403-441): seeds from <see cref="OptionColorRequirementsKey"/> (a dual card's option-play
    /// colour requirement, distinct from its printed Digimon colours) then folds every active
    /// <see cref="CardEffects.ChangeBaseCardColorClass"/> effect — the same two-stage colour machinery as
    /// <see cref="BaseCardColors"/>, seeded differently.</summary>
    public IReadOnlyList<string> BaseDualCardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(ReadStrings(Definition?.Metadata, OptionColorRequirementsKey));
            colors = FoldColorEffects<IChangeBaseCardColorEffect>(colors, (e, c) => e.GetBaseCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(RD-2 latent-1) The card's dual colours (mirror of <c>DualCardColors</c>, CardSource.cs:283-341):
    /// <see cref="BaseDualCardColors"/> then every active <see cref="CardEffects.ChangeCardColorClass"/> effect.
    /// AS-IS uses this (not <see cref="CardColors"/>) as the option colour requirement when the played card is a
    /// Digimon (a dual Digimon+Option card).</summary>
    public IReadOnlyList<string> DualCardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(BaseDualCardColors);
            colors = FoldColorEffects<IChangeCardColorEffect>(colors, (e, c) => e.GetCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(R1-e) AS-IS <c>CardSource.MatchColorRequirement</c> (CardSource.cs:255-321): whether an OPTION
    /// card's colour requirement is satisfied — an active <see cref="IIgnoreColorConditionEffect"/> waives it
    /// (<see cref="IgnoreColorConditionActive"/>, AS-IS inlines the same three ignore-scan regions), otherwise
    /// EVERY colour the card requires (<see cref="DualCardColors"/> when it is also a Digimon, else
    /// <see cref="CardColors"/>) must be present on SOME owner field permanent whose top card is a permanent.
    /// A non-Option card trivially matches. ADAPTATION: <c>Owner.GetFieldPermanents()</c> =
    /// <c>new Player(Context, Owner).GetFieldPermanents()</c>; <c>TopCard.IsPermanent</c> = the extension
    /// <c>TopCard.IsPermanent()</c>.</summary>
    public bool MatchColorRequirement
    {
        get
        {
            if (IsOption)
            {
                // "ignore color requirement" effects (AS-IS inlines the permanents/players/itself scan =
                // IgnoreColorConditionActive).
                if (IgnoreColorConditionActive())
                {
                    return true;
                }

                IReadOnlyList<string> colorsToCheck = IsDigimon ? DualCardColors : CardColors;

                bool matchColorRequirement = colorsToCheck.Every(cardColor =>
                    new Player(Context, Owner).GetFieldPermanents().Some(permanent =>
                        // AS-IS TopCard.IsPermanent (_cEntity_Base.IsPermanent) = the printed permanent-type flag
                        // = PlayCardClass.IsPermanent extension body, inlined to avoid a cross-namespace using.
                        (permanent.TopCard.IsDigimon || permanent.TopCard.IsTamer || permanent.TopCard.IsDigiEgg)
                        && permanent.TopCard.CardColors.Contains(cardColor)));

                if (!matchColorRequirement)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>(A3 / P6C3 re-fold) The card's traits (mirror of <c>CardTraits</c>, CardSource.cs:2581-2604):
    /// printed traits transformed by the card's OWN <see cref="IChangeTraitsEffect"/> effects
    /// (AS-IS scans self EffectList only, ungated by permanent membership; no Distinct).</summary>
    public IReadOnlyList<string> CardTraits
    {
        get
        {
            List<string> traits = ReadStrings(Definition?.Metadata, "traits").ToList();
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is IChangeTraitsEffect transform && cardEffect.CanUse(null))
                {
                    traits = transform.ChangTraits(traits, this);
                }
            }

            return traits;
        }
    }

    // (P6C3) The pre-flip registry-fold helpers (FoldListTransforms / SelfTransforms over the dead per-kind
    // binding keys) are retired with the interface-scan re-folds above — no producer writes those keys since
    // the kind-class 1:1 rebuild.

    // (A3) the AS-IS `cardEffect.CanUse(null)` gate — the binding's stored continuous condition.
    internal static bool EffectConditionPasses(EffectRequest effect) =>
        !effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? raw)
        || raw is not Func<bool> condition
        || condition();

    /// <summary>(bridge W4) AS-IS <c>CardSource.BaseENGCardNameFromEntity</c> (CardSource.cs:1359,
    /// <c>_cEntity_Base.CardName_ENG</c>) — the PRINTED base-entity English name, UNtransformed (unlike
    /// <see cref="CardNames"/>, no ChangeBaseCardName/ChangeCardNames folds — AS-IS reads the raw entity).
    /// Card-corpus use: the informational <c>effectName</c> argument to <c>SetUpICardEffect</c>
    /// (BT1_092/BT1_094 pattern).</summary>
    public string BaseENGCardNameFromEntity => Definition?.Name ?? string.Empty;

    /// <summary>Continuous-binding key for an added card name (AS-IS ChangeCardNamesClass).</summary>
    public const string AddedCardNameKey = "addedCardName";

    /// <summary>(P6C3 re-fold) Mirror of AS-IS <c>CardSource.BaseCardNames</c> (CardSource.cs:1371-1436):
    /// the printed name, transformed by active <see cref="IChangeBaseCardNameEffect"/> effects (a REPLACE;
    /// BT14_097 "original name is [Sukamon]"). AS-IS branch structure preserved: a DIGIVOLUTION SOURCE folds
    /// only its own non-granted effects (<see cref="EffectList_ExceptAddedEffects(EffectTiming)"/>); any other
    /// card folds self (only while not a permanent) + all field permanents + player effects. Design item
    /// RD-P6C3-A1: the AS-IS dual-card branch (<c>!isPermanent &amp;&amp; _cEntity_Base.IsDualCard</c> adds
    /// <c>dualEffect</c>, the second printed name) has no mirror data carrier yet — lands with dual-card
    /// definition data.</summary>
    public IReadOnlyList<string> BaseCardNames
    {
        get
        {
            var baseCardNames = Definition is { } d ? new List<string> { d.Name } : new List<string>();

            PermanentView thisPermanent = PermanentOfThisCard();
            bool isPermanent = !thisPermanent.IsEmpty;
            bool isDigivolutionCard = isPermanent && thisPermanent.DigivolutionCards.Any(under => under.InstanceId == InstanceId);

            if (isDigivolutionCard)
            {
                foreach (ICardEffect cardEffect in EffectList_ExceptAddedEffects(EffectTiming.None))
                {
                    if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                    {
                        baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                    }
                }
            }
            else
            {
                if (!isPermanent)
                {
                    foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                        {
                            baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                        }
                    }
                }

                // (R1-e order fix) AS-IS runs the permanents pass ACROSS BOTH players first, THEN the players pass
                // (CardSource.cs:1411-1427) — two separate passes, not interleaved per-player.
                // the effects of permanents
                foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
                {
                    foreach (Permanent permanent in player.GetFieldPermanents())
                    {
                        foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                        {
                            if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                            {
                                baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                            }
                        }
                    }
                }

                // the effects of players
                foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
                {
                    foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                        {
                            baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                        }
                    }
                }
            }

            return baseCardNames;
        }
    }

    /// <summary>(R1-e) AS-IS <c>CardSource.CardNames</c> (CardSource.cs:1442-1459): <see cref="BaseCardNames"/>
    /// (cloned) transformed by the card's own <see cref="IChangeCardNamesEffect"/> effects — the AS-IS scan of
    /// <c>EffectList_ExceptAddedEffects</c> (non-null, IChangeCardNamesEffect, <c>CanUse(null)</c>), then Distinct.
    /// The pre-R1-e <see cref="AddedCardNameKey"/> registry read is DROPPED: AS-IS has no such branch — an added
    /// card name is an <see cref="IChangeCardNamesEffect"/> already enumerated by the scan.</summary>
    public IReadOnlyList<string> CardNames
    {
        get
        {
            List<string> cardNames = BaseCardNames.ToList(); // AS-IS BaseCardNames.Clone() (mirror BaseCardNames is IReadOnlyList)

            // the effects of itself
            EffectList_ExceptAddedEffects(EffectTiming.None)
                .Filter(cardEffect => cardEffect != null)
                .Filter(cardEffect => cardEffect is IChangeCardNamesEffect && cardEffect.CanUse(null))
                .ForEach(cardEffect => cardNames = ((IChangeCardNamesEffect)cardEffect).ChangeCardNames(cardNames, this));

            return cardNames.Distinct().ToList();
        }
    }

    /// <summary>(P6C3) Mirror of AS-IS <c>CardSource.CardNames_DigiXros</c> (CardSource.cs:2193-2207):
    /// <see cref="CardNames"/> extended by the card's OWN <see cref="IChangeCardNamesForDigiXrosEffect"/>
    /// effects (AS-IS scans self <c>EffectList(EffectTiming.None)</c> only, same shape as
    /// <see cref="CardTraits"/> — ungated by permanent membership, no re-Distinct after the fold).</summary>
    public IReadOnlyList<string> CardNames_DigiXros
    {
        get
        {
            List<string> cardNames = CardNames.ToList();
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is IChangeCardNamesForDigiXrosEffect transform && cardEffect.CanUse(null))
                {
                    cardNames = transform.ChangeCardNamesForDigiXros(cardNames, this);
                }
            }

            return cardNames;
        }
    }

    /// <summary>The card's PRINTED level, or -1. AS-IS <c>HasLevel</c> is printed-data based
    /// (CEntity_Base.cs:317) — level-change folds never alter it.</summary>
    private int PrintedLevel => Definition?.Metadata is { } m && m.TryGetValue("level", out object? raw) && raw is int lv ? lv : -1;

    /// <summary>(A3 / P6C3 re-fold) The card's level (mirror of <c>Level =&gt; TreatedLevel</c>,
    /// CardSource.cs:941-975): printed level transformed by the card's OWN
    /// <see cref="IChangeCardLevelEffect"/> effects (AS-IS scans self EffectList only). -1 mirrors the AS-IS
    /// no-level sentinel (1145140) — no gameplay code compares the sentinel; all consumers guard on
    /// <see cref="HasLevel"/> first.</summary>
    public int Level
    {
        get
        {
            int level = PrintedLevel;
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is IChangeCardLevelEffect transform && cardEffect.CanUse(null))
                {
                    level = transform.GetCardLevel(level, this);
                }
            }

            return level;
        }
    }

    /// <summary>The card's printed number (e.g. "BT10-012"), used as the SpecialPlayRecipe key.</summary>
    public string CardNumber => Definition?.CardNumber ?? string.Empty;

    // (C7) type judgements go through CardRecord.IsCardType — AS-IS CardKinds is a LIST, so a dual card
    // (e.g. Digimon/Option hybrid) reports true for BOTH kinds.
    public bool IsDigimon => Definition?.IsCardType("Digimon") == true;
    public bool IsTamer => Definition?.IsCardType("Tamer") == true;
    public bool IsOption => Definition?.IsCardType("Option") == true;
    /// <summary>(MIG2) AS-IS <c>CardSource.IsDigiEgg</c> (CardSource.cs:3466) — both fixture spellings.</summary>
    public bool IsDigiEgg => Definition?.IsCardType("DigiEgg") == true || Definition?.IsCardType("Digitama") == true;
    /// <summary>(MIG2 substrate) Whether a card DEFINITION is registered at all. AS-IS every CardSource carries
    /// a CEntity — a definition-less instance exists only in abstract test fixtures; rule predicates that would
    /// trash a card for its TYPE gate on this (fixture guard, same family as the D-2 defined-DP guard).</summary>
    public bool HasDefinition => Definition is not null;
    /// <summary>(MIG2) AS-IS <c>CardSource.IsFlipped</c> — the shared face-down instance flag.</summary>
    public bool IsFlipped => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? f) && f is not null
        && f.Metadata.TryGetValue("isFlipped", out object? flip) && flip is true;
    /// <summary>(joint-migration) public card-type check for scope synthesis in restriction producers.</summary>
    public bool IsCardType(string cardType) => Definition?.IsCardType(cardType) == true;
    public bool IsToken => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isToken", out object? t) && t is bool b && b;

    // (A3) printed-data based like AS-IS CEntity_Base.HasLevel — a level-change fold does not grant a level.
    public bool HasLevel => PrintedLevel >= 0;

    /// <summary>(Jogress by levels / P6C3 re-fold) 1:1 of AS-IS <c>Permanent.Levels_ForJogress(CardSource)</c>
    /// (Permanent.cs:3554-3605): the levels THIS card's permanent counts as when used as a Jogress /
    /// DNA-Digivolution material against <paramref name="jogressCard"/> (the digivolving card). AS-IS seeds
    /// the MATERIAL PERMANENT's <c>Level</c> gated on <c>cardSource.HasLevel</c> (the jogress card's printed
    /// level — verbatim AS-IS gate), then adds every level contributed by an active
    /// <see cref="IAddJogressLevelsEffect"/> across all field permanents' and players' effects
    /// (<c>GetJogressLevels(cardSource, this)</c>). The mirror keeps this accessor on CardSource (its
    /// established consumer surface); the permanent identity is resolved live.</summary>
    public IReadOnlyList<int> JogressLevelsAgainst(CardSource jogressCard)
    {
        ArgumentNullException.ThrowIfNull(jogressCard);
        var levels = new List<int>();
        Permanent material = ICardEffect.ResolvePermanentOfThisCard(this) ?? new Permanent(Context, InstanceId, Owner);
        if (jogressCard.HasLevel)
        {
            levels.Add(material.Level);
        }

        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IAddJogressLevelsEffect addLevels && cardEffect.CanUse(null))
                    {
                        levels.AddRange(addLevels.GetJogressLevels(jogressCard, material));
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IAddJogressLevelsEffect addLevels && cardEffect.CanUse(null))
                {
                    levels.AddRange(addLevels.GetJogressLevels(jogressCard, material));
                }
            }
        }

        return levels;
    }

    /// <summary>(W6-P) printed-data based like AS-IS <c>HasDP</c> — the card defines a DP at all.</summary>
    public bool HasDP => Definition?.Metadata.TryGetValue("dp", out object? dp) == true && dp is int;

    /// <summary>(R1-a) AS-IS <c>CardSource.BaseCardDP</c> (CardSource.cs:2376: <c>=&gt; _cEntity_Base.DP</c>) — the
    /// card's PRINTED base DP, read off the card definition. ADAPTATION: the mirror materialises the printed dp on
    /// the card INSTANCE metadata ("dp") at creation, so this reads the instance value first (the exact seed the
    /// engine used before this rehousing), falling back to the definition and then 0 for a dp-less abstract fixture.</summary>
    public int BaseCardDP
    {
        get
        {
            if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? inst) && inst is not null
                && inst.Metadata.TryGetValue("dp", out object? raw) && raw is int instanceDp)
            {
                return instanceDp;
            }

            return Definition?.Metadata.TryGetValue("dp", out object? def) == true && def is int printed ? printed : 0;
        }
    }

    /// <summary>(R1-a) AS-IS <c>CardSource.BaseDP</c> (CardSource.cs:2377: <c>public int BaseDP = 0;</c>) — the
    /// per-instance ADDITIVE base-DP field (default 0; the <c>Permanent.BaseDP</c> seed is
    /// <c>BaseCardDP + BaseDP</c>). ADAPTATION: the AS-IS mutable field maps to instance metadata ("baseDp") on the
    /// transient view (same substrate idiom as <c>Permanent.IsSuspended</c>); default 0 as an unset AS-IS int
    /// field, so the seed is unchanged unless a future writer sets it.</summary>
    public int BaseDP
    {
        get =>
            Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("baseDp", out object? raw) && raw is int b ? b : 0;
        set
        {
            if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    ["baseDp"] = value,
                };
                Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(R1-e) AS-IS <c>CardSource.CardDP</c> (CardSource.cs:2383-2443): the card's live DP — printed
    /// <see cref="BaseCardDP"/> + the additive <see cref="BaseDP"/>, transformed by every usable
    /// <see cref="IChangeCardDPEffect"/> whose <c>CardCondition(this)</c> holds (scan of all field permanents' then
    /// all players' effects, TURN player first). AS-IS applies the <c>IsUpDown</c> (relative) effects first, then the
    /// non-IsUpDown (absolute) effects, and clamps at 0. Returns -1 clamped to 0 when the card has no printed DP.
    /// ADAPTATION: <c>gameContext.Players_ForTurnPlayer</c> = <c>new GameContext(Context).Players_ForTurnPlayer</c>.</summary>
    public int CardDP
    {
        get
        {
            int cardDP = -1;

            if (HasDP)
            {
                cardDP = BaseCardDP;
                cardDP += BaseDP;

                // card effects that change card DP
                List<ICardEffect> changeCardDPCardEffects = new List<ICardEffect>();

                // the effects of permanents
                changeCardDPCardEffects = changeCardDPCardEffects
                    .Concat(
                    new GameContext(Context).Players_ForTurnPlayer
                        .Map(player => player.GetFieldPermanents())
                        .Flat()
                        .Map(permanent => permanent.EffectList(EffectTiming.None))
                        .Flat()
                        .Filter(cardEffect => cardEffect is IChangeCardDPEffect && cardEffect.CanUse(null)
                            && ((IChangeCardDPEffect)cardEffect).CardCondition(this)))
                        .ToList();

                // the effects of players
                changeCardDPCardEffects = changeCardDPCardEffects
                    .Concat(
                    new GameContext(Context).Players_ForTurnPlayer
                        .Map(player => player.EffectList(EffectTiming.None))
                        .Flat()
                        .Filter(cardEffect => cardEffect is IChangeCardDPEffect && cardEffect.CanUse(null)
                            && ((IChangeCardDPEffect)cardEffect).CardCondition(this)))
                        .ToList();

                List<ICardEffect> cardEffects_ChangeDP_IsUpDown = changeCardDPCardEffects
                    .Filter(cardEffect => ((IChangeCardDPEffect)cardEffect).IsUpDown());

                List<ICardEffect> cardEffects_ChangeDP_NotIsUpDown = changeCardDPCardEffects
                    .Filter(cardEffect => !((IChangeCardDPEffect)cardEffect).IsUpDown());

                cardEffects_ChangeDP_IsUpDown
                    .ForEach(cardEffect => cardDP = ((IChangeCardDPEffect)cardEffect).GetDP(cardDP, this));

                cardEffects_ChangeDP_NotIsUpDown
                    .ForEach(cardEffect => cardDP = ((IChangeCardDPEffect)cardEffect).GetDP(cardDP, this));
            }

            return Math.Max(0, cardDP);
        }
    }

    /// <summary>(W6 tail) AS-IS <c>HasPlayCost</c> — the card defines a play cost.</summary>
    public bool HasPlayCost => Definition?.PlayCost is not null;

    /// <summary>(W6 tail) The card's PRINTED play cost — behaviourally this mirrors AS-IS
    /// <c>BasePlayCostFromEntity</c> (CardSource.cs:757), NOT the AS-IS <c>GetCostItself</c> (CardSource.cs:769),
    /// which is modifier-FOLDED: <c>Max(0, GetChangedCostItselef(BasePlayCostFromEntity, Root.None,
    /// [PermanentOfThisCard()]))</c>. The name is kept for the existing call sites; a caller whose AS-IS analog
    /// reads the folded <c>GetCostItself</c> (Min/MaxCost comparisons, CostJustBeforeRemoveField) carries a
    /// documented reduction here (no per-card cost-change effects fold in); a caller whose AS-IS analog reads
    /// <c>BasePlayCostFromEntity</c> (e.g. BT22_035's "play cost 4 or less") is exact.</summary>
    public int GetCostItself => Definition?.PlayCost ?? 0;

    // ===== (R2-C) AS-IS play / digivolution cost pipeline — 1:1 rehousing of CardSource.cs:635-933 =========
    // The single top-level pay-cost orchestrator the play / digivolve / option chokes call: base printed cost
    // (or, for a digivolution target, the minimum matching CostList entry) -> DigiXros / Assembly special
    // reductions (CanReduceCost-gated) -> the ChangeCostClass 2-group fold (GetChangedCostItselef then
    // GetChangedPayingCost) -> Math.Max(0). AS-IS has NO registry; the mirror mid-migration additionally carries
    // still-substrate LEGACY continuous cost bindings (BeforePayCostReduction / ChangePlayCostPlayer producers)
    // whose AS-IS analog is an IChangeCostEffect folded below — those are UNIONed in as a substrate step
    // (ContinuousModifierGate.FoldLegacy*CostModifiers) until their producers are new-model. CannotReduceCost is
    // honoured UNIFORMLY through Player.CanReduceCost (both the legacy fold's canReduce knob AND, live inside the
    // fold below, ChangeCostClass.GetCost's own veto) — a single immunity representation.

    /// <summary>(R2-C) 1:1 mirror of AS-IS <c>CardSource.PayingCost(root, targetPermanents, checkAvailability,
    /// ignoreLevel, FixedCost)</c> (CardSource.cs:635-658): the base printed play cost, or — for a single
    /// non-null digivolution target — the minimum matching <see cref="CostList"/> entry, folded through
    /// <see cref="GetPayingCostWithBaseCost"/>.</summary>
    public int PayingCost(SelectCardEffect.Root root, List<Permanent>? targetPermanents, bool checkAvailability = false, bool ignoreLevel = false, int FixedCost = -1)
    {
        int baseCost = Definition?.PlayCost ?? 0;   // AS-IS _cEntity_Base.PlayCost

        if (targetPermanents != null)
        {
            if (targetPermanents.Count == 1)
            {
                Permanent targetPermanent = targetPermanents[0];

                if (targetPermanent != null)
                {
                    List<int> costList = CostList(targetPermanent, ignoreLevel: ignoreLevel, checkAvailability: checkAvailability);

                    if (costList.Count >= 1)
                    {
                        baseCost = costList.Min();
                    }
                }
            }
        }

        return GetPayingCostWithBaseCost(baseCost, root, targetPermanents, checkAvailability: checkAvailability, FixedCost: FixedCost);
    }

    /// <summary>(R2-C) 1:1 mirror of AS-IS <c>CardSource.GetPayingCostWithBaseCost(baseCost, root,
    /// targetPermanents, checkAvailability, FixedCost)</c> (CardSource.cs:664-751): the FixedCost override, the
    /// DigiXros / Assembly special reductions (AS-IS :670-737), then <see cref="GetChangedCostItselef"/> +
    /// <see cref="GetChangedPayingCost"/> and the 0 floor. The mirror UNION step folds the still-substrate
    /// LEGACY continuous cost bindings via <see cref="ContinuousModifierGate.FoldLegacyPlayCostModifiers"/> /
    /// <see cref="ContinuousModifierGate.FoldLegacyDigivolutionCostModifiers"/>.</summary>
    public int GetPayingCostWithBaseCost(int baseCost, SelectCardEffect.Root root, List<Permanent>? targetPermanents, bool checkAvailability = false, int FixedCost = -1)
    {
        int Cost = FixedCost >= 0 ? FixedCost : baseCost;

        bool isEvolution = targetPermanents != null && targetPermanents.Some((permanent) => permanent != null);

        Player ownerPlayer = new Player(Context, Owner);

        // ===== DigiXros (AS-IS CardSource.cs:670-701) =====
        // Headless adaptation: AS-IS's `!(!Owner.isYou && GManager.instance.IsAI)` availability guard has no
        // headless analog (no AI / seat-you distinction); the single-context mirror is always the AS-IS
        // "not the AI opponent" branch, so the availability early-return is taken unconditionally. On the live
        // pay path (checkAvailability:false, no DigiXros material selection => playCard != this) the whole
        // region is a no-op.
        if (!isEvolution)
        {
            if (HasDigiXros)
            {
                if (ownerPlayer.CanReduceCost(null, this))
                {
                    if (checkAvailability)
                    {
                        return 0;
                    }

                    SelectDigiXrosClass selectDigiXrosClass = GManager.instance.GetComponent<SelectDigiXrosClass>();

                    if (selectDigiXrosClass != null)
                    {
                        if (selectDigiXrosClass.playCard == this)
                        {
                            Cost -= selectDigiXrosClass.selectedDigicrossCards.Count * digiXrosCondition.reduceCostPerCard;
                        }
                    }
                }
            }
        }

        // ===== Assembly (AS-IS CardSource.cs:705-737) — STOP (design item RD-R2C-ASSEMBLY) =====
        // The AS-IS Assembly cost-reduction region reads `GManager.GetComponent<SelectAssemblyClass>()`'s
        // playCard / selectedAssemblyCards interactive-selection state. The mirror SelectAssemblyClass is a
        // STATIC feasibility helper (no component/playCard state — the interactive Assembly selection is a
        // parameterized SpecialPlayAction that computes its own paid cost), so this sub-region has no 1:1 mirror.
        // It never fired on this generic pay path (no live selection sets playCard == this), so its absence is
        // behaviour-invariant.

        // ===== (UNION substrate) legacy continuous cost bindings — mirror mid-migration only =====
        // `canReduce` is the live CannotReduceCost veto (Player.CanReduceCost), the SAME signal the new-model
        // ChangeCostClass.GetCost re-derives below — one uniform immunity.
        bool canReduce = ownerPlayer.CanReduceCost(targetPermanents, this);
        if (isEvolution)
        {
            HeadlessEntityId digivolveTargetPermanentId =
                (targetPermanents != null && targetPermanents.Count >= 1 && targetPermanents[0] != null)
                    ? targetPermanents[0].InstanceId
                    : default;
            Cost = ContinuousModifierGate.FoldLegacyDigivolutionCostModifiers(Context, InstanceId, Cost, digivolveTargetPermanentId, canReduce);
        }
        else
        {
            Cost = ContinuousModifierGate.FoldLegacyPlayCostModifiers(Context, InstanceId, Cost, canReduce);
        }

        // ===== AS-IS GetChangedCostItselef (:741) + GetChangedPayingCost (:743) — new-model IChangeCostEffect =====
        Cost = GetChangedCostItselef(Cost, root, targetPermanents, checkAvailability);

        Cost = GetChangedPayingCost(Cost, root, targetPermanents, checkAvailability);

        if (Cost < 0)
        {
            Cost = 0;
        }

        return Cost;
    }

    /// <summary>(R2-C) 1:1 mirror of AS-IS <c>CardSource.GetChangedCostItselef</c> (CardSource.cs:775-858): fold
    /// the "not-paying" (<c>IsChangePayingCost()==false</c>) <see cref="IChangeCostEffect"/> group over
    /// Players_ForTurnPlayer's field permanents' + players' (+ this card's own, while it is not a permanent)
    /// <c>EffectList(None)</c>, NotIsUpDown effects first then IsUpDown, clamped at 0. AS-IS's <c>checkAvailability
    /// &amp;&amp; GManager.IsAI</c> opponent-target short-circuit (:777-789) has no headless analog (no AI seat) and
    /// is omitted (behaviour-invariant).</summary>
    public int GetChangedCostItselef(int Cost, SelectCardEffect.Root root, List<Permanent>? targetPermanents, bool checkAvailability = false)
    {
        List<ICardEffect> changeCostCardEffects = new List<ICardEffect>();

        bool Matches(ICardEffect cardEffect) =>
            cardEffect is IChangeCostEffect && cardEffect.CanUse(null)
            && ((IChangeCostEffect)cardEffect).CardCondition(this)
            && !(((IChangeCostEffect)cardEffect).IsCheckAvailability() && !checkAvailability)
            && !((IChangeCostEffect)cardEffect).IsChangePayingCost();

        // the effects of permanents
        changeCostCardEffects = changeCostCardEffects
            .Concat(
            new GameContext(Context).Players_ForTurnPlayer
                .Map(player => player.GetFieldPermanents())
                .Flat()
                .Map(permanent => permanent.EffectList(EffectTiming.None))
                .Flat()
                .Filter(Matches))
                .ToList();

        // the effects of players
        changeCostCardEffects = changeCostCardEffects
            .Concat(
            new GameContext(Context).Players_ForTurnPlayer
                .Map(player => player.EffectList(EffectTiming.None))
                .Flat()
                .Filter(Matches))
                .ToList();

        // the effects of itself
        if (PermanentOfThisCard().IsEmpty)
        {
            changeCostCardEffects = changeCostCardEffects
                .Concat(
                EffectList(EffectTiming.None)
                    .Filter(Matches))
                .ToList();
        }

        List<ICardEffect> changeCostCardEffects_NotIsUpDown = changeCostCardEffects
            .Filter(cardEffect => !((IChangeCostEffect)cardEffect).IsUpDown());

        List<ICardEffect> changeCostCardEffects_IsUpDown = changeCostCardEffects
            .Filter(cardEffect => ((IChangeCostEffect)cardEffect).IsUpDown());

        changeCostCardEffects_NotIsUpDown
            .ForEach(cardEffect => Cost = ((IChangeCostEffect)cardEffect).GetCost(Cost, this, root, targetPermanents!));

        changeCostCardEffects_IsUpDown
            .ForEach(cardEffect => Cost = ((IChangeCostEffect)cardEffect).GetCost(Cost, this, root, targetPermanents!));

        return Math.Max(0, Cost);
    }

    /// <summary>(R2-C) 1:1 mirror of AS-IS <c>CardSource.GetChangedPayingCost</c> (CardSource.cs:864-933): the
    /// "paying" (<c>IsChangePayingCost()==true</c>) <see cref="IChangeCostEffect"/> group, same scan / order /
    /// clamp as <see cref="GetChangedCostItselef"/> — AS-IS runs both sequentially over the same running
    /// Cost.</summary>
    public int GetChangedPayingCost(int Cost, SelectCardEffect.Root root, List<Permanent>? targetPermanents, bool checkAvailability = false)
    {
        List<ICardEffect> changePayingCostCardEffects = new List<ICardEffect>();

        bool Matches(ICardEffect cardEffect) =>
            cardEffect is IChangeCostEffect && cardEffect.CanUse(null)
            && ((IChangeCostEffect)cardEffect).CardCondition(this)
            && !(((IChangeCostEffect)cardEffect).IsCheckAvailability() && !checkAvailability)
            && ((IChangeCostEffect)cardEffect).IsChangePayingCost();

        // the effects of permanents
        changePayingCostCardEffects = changePayingCostCardEffects
            .Concat(
            new GameContext(Context).Players_ForTurnPlayer
                .Map(player => player.GetFieldPermanents())
                .Flat()
                .Map(permanent => permanent.EffectList(EffectTiming.None))
                .Flat()
                .Filter(Matches))
                .ToList();

        // the effects of players
        changePayingCostCardEffects = changePayingCostCardEffects
            .Concat(
            new GameContext(Context).Players_ForTurnPlayer
                .Map(player => player.EffectList(EffectTiming.None))
                .Flat()
                .Filter(Matches))
                .ToList();

        // the effects of itself
        if (PermanentOfThisCard().IsEmpty)
        {
            changePayingCostCardEffects = changePayingCostCardEffects
                .Concat(
                EffectList(EffectTiming.None)
                    .Filter(Matches))
                .ToList();
        }

        List<ICardEffect> changePayingCostCardEffects_NotIsUpDown = changePayingCostCardEffects
            .Filter(cardEffect => !((IChangeCostEffect)cardEffect).IsUpDown());

        List<ICardEffect> changePayingCostCardEffects_IsUpDown = changePayingCostCardEffects
            .Filter(cardEffect => ((IChangeCostEffect)cardEffect).IsUpDown());

        changePayingCostCardEffects_NotIsUpDown
            .ForEach(cardEffect => Cost = ((IChangeCostEffect)cardEffect).GetCost(Cost, this, root, targetPermanents!));

        changePayingCostCardEffects_IsUpDown
            .ForEach(cardEffect => Cost = ((IChangeCostEffect)cardEffect).GetCost(Cost, this, root, targetPermanents!));

        return Math.Max(0, Cost);
    }

    /// <summary>(R2-C) 1:1 mirror of AS-IS <c>CardSource.CostList(targetPermanent, ignoreLevel,
    /// checkAvailability)</c> (CardSource.cs:617-627 — the <c>EvoCosts</c> projection filtered to matching
    /// paths). STOP: the printed <c>BaseEvoCostsFromEntity</c> half of the EvoCost engine (EvoCost value type +
    /// card data) is a separate R1 lift (design item RD-P6C1-2); only reached by the (non-live) AS-IS mirror
    /// PlayCard evolution path.</summary>
    public List<int> CostList(Permanent targetPermanent, bool ignoreLevel, bool checkAvailability)
    {
        _ = targetPermanent;
        _ = ignoreLevel;
        _ = checkAvailability;
        throw new NotSupportedException(
            "STOP: CardSource.CostList (AS-IS CardSource.cs:617) — the printed EvoCost engine has no mirror " +
            "(design item RD-P6C1-2); the live digivolution-cost base is DigivolutionCostHelpers, not this.");
    }

    /// <summary>(C9) mirror of AS-IS <c>CardSource.IsLinked</c> (CardSource.cs:2947):
    /// <c>PermanentOfThisCard().LinkedCards.Contains(this)</c> — true while this card is a LINK card of a
    /// battle-area permanent (link cards are tracked separately from digivolution sources:
    /// <c>LinkHelpers.LinkedCardIdsKey</c>). Evaluated LIVE — breaking the link flips it false.</summary>
    public bool IsLinked
    {
        get
        {
            var zones = (IZoneStateReader)Context.ZoneMover;
            foreach (HeadlessEntityId hostId in zones.GetCards(Owner, ChoiceZone.BattleArea))
            {
                if (Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null
                    && LinkHelpers.ReadLinkedCardIds(host.Metadata).Contains(InstanceId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool IsLevel(int level) => Level == level;
    public bool HasCardColor(string color) => CardColors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
    public bool EqualsCardName(string name) => CardNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    public bool ContainsCardName(string fragment) => CardNames.Any(n => n.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    public bool EqualsTraits(string trait) => CardTraits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));
    public bool ContainsTraits(string fragment) => CardTraits.Any(t => t.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>(bridge W5) 1:1 mirror of AS-IS <c>CardSource.HasXAntibodyTraits</c> (CardSource.cs:1975):
    /// <c>CardTraits.Some(DataBase.IsXAntibodyString)</c> — any trait normalising to "xantibody"
    /// (BT9_109 "[When Attacking] … digivolve into a Digimon card with [X Antibody] in its traits").</summary>
    public bool HasXAntibodyTraits => CardTraits.Some(DataBase.IsXAntibodyString);

    /// <summary>(R1-e) AS-IS <c>CardSource.CanNotBeAffected(ICardEffect _cardEffect)</c> (CardSource.cs:1060-1110):
    /// whether an active <see cref="ICanNotAffectedEffect"/> shields THIS card from the given causing effect. A null
    /// cause is never blocked (AS-IS :1062). The AS-IS three-region scan (all field permanents' effects → all
    /// players' effects → THIS card's own effects while not a permanent), gated by <c>CanUse(null)</c> and
    /// <c>CanNotAffect(this, _cardEffect)</c>. ADAPTATION: <c>gameContext.Players</c> (full roster) =
    /// <c>new GameContext(Context).Players</c>. Rehoused from the mirror <see cref="ContinuousImmunityGate"/>
    /// delegation to this AS-IS-literal scan.</summary>
    public bool CanNotBeAffected(ICardEffect _cardEffect)
    {
        if (_cardEffect == null) return false;

        // the effects of permanents
        if (new GameContext(Context).Players
            .Map(player => player.GetFieldPermanents())
            .Flat()
            .Map(permanent => permanent.EffectList(EffectTiming.None))
            .Flat()
            .Some(cardEffect => cardEffect is ICanNotAffectedEffect
                && cardEffect.CanUse(null)
                && ((ICanNotAffectedEffect)cardEffect).CanNotAffect(this, _cardEffect)))
        {
            return true;
        }

        // the effects of players
        if (new GameContext(Context).Players
                .Map(player => player.EffectList(EffectTiming.None))
                .Flat()
                .Some(cardEffect => cardEffect is ICanNotAffectedEffect
                    && cardEffect.CanUse(null)
                    && ((ICanNotAffectedEffect)cardEffect).CanNotAffect(this, _cardEffect)))
        {
            return true;
        }

        // the effects of itself
        if (PermanentOfThisCard().IsEmpty)
        {
            if (EffectList(EffectTiming.None)
                    .Some(cardEffect => cardEffect is ICanNotAffectedEffect
                        && cardEffect.CanUse(null)
                        && ((ICanNotAffectedEffect)cardEffect).CanNotAffect(this, _cardEffect)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>(R1-e) AS-IS <c>CardSource.willBeRemoveSources</c> (CardSource.cs:2472, a public bool field): the
    /// "already being removed from sources this pass" mark. ADAPTATION: the AS-IS mutable field maps to instance
    /// metadata (the <see cref="BaseDP"/> transient-view idiom); default false as an unset AS-IS bool field.</summary>
    public bool willBeRemoveSources
    {
        get =>
            Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("willBeRemoveSources", out object? raw) && raw is true;
        set
        {
            if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    ["willBeRemoveSources"] = value,
                };
                Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(R1-e) AS-IS <c>CardSource.CanNotTrashFromDigivolutionCards(ICardEffect _cardEffect)</c>
    /// (CardSource.cs:2478-2529): this source is protected from digivolution-stack trashing — the in-flight
    /// <see cref="willBeRemoveSources"/> mark (AS-IS :2480) OR a usable <see cref="ICanNotTrashFromDigivolutionCardsEffect"/>
    /// across the AS-IS three-region scan (all field permanents → all players → THIS card while not a permanent),
    /// gated by <c>CanUse(null)</c> and <c>CanNotTrashFromDigivolutionCards(this, _cardEffect)</c>. Rehoused from the
    /// mirror <see cref="TrashProtectionScan"/> delegation to this AS-IS-literal scan; AS-IS has no separate
    /// static-grant flag branch (the static grant is an effect enumerated by the scan).</summary>
    public bool CanNotTrashFromDigivolutionCards(ICardEffect _cardEffect)
    {
        if (willBeRemoveSources)
            return true;

        // the effects of permanents
        if (new GameContext(Context).Players
            .Map(player => player.GetFieldPermanents())
            .Flat()
            .Map(permanent => permanent.EffectList(EffectTiming.None))
            .Flat()
            .Some(cardEffect => cardEffect is ICanNotTrashFromDigivolutionCardsEffect
            && cardEffect.CanUse(null)
            && ((ICanNotTrashFromDigivolutionCardsEffect)cardEffect).CanNotTrashFromDigivolutionCards(this, _cardEffect)))
        {
            return true;
        }

        // the effects of players
        if (new GameContext(Context).Players
                .Map(player => player.EffectList(EffectTiming.None))
                .Flat()
                .Some(cardEffect => cardEffect is ICanNotTrashFromDigivolutionCardsEffect
                && cardEffect.CanUse(null)
                && ((ICanNotTrashFromDigivolutionCardsEffect)cardEffect).CanNotTrashFromDigivolutionCards(this, _cardEffect)))
        {
            return true;
        }

        // the effects of itself
        if (PermanentOfThisCard().IsEmpty)
        {
            if (EffectList(EffectTiming.None)
                    .Some(cardEffect => cardEffect is ICanNotTrashFromDigivolutionCardsEffect
                    && cardEffect.CanUse(null)
                    && ((ICanNotTrashFromDigivolutionCardsEffect)cardEffect).CanNotTrashFromDigivolutionCards(this, _cardEffect)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.HasSameCardName(cardSource)</c> (CardSource.cs:1465):
    /// whether ANY of <paramref name="other"/>'s (folded) names equals one of THIS card's names — AS-IS folds
    /// <paramref name="other"/>.CardNames through THIS card's <see cref="EqualsCardName"/>.</summary>
    public bool HasSameCardName(CardSource other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.CardNames.Any(name => EqualsCardName(name));
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.CanLink(PayCost, allowBreeding)</c>
    /// (CardSource.cs:3140-3199): whether THIS card's declared <see cref="LinkCondition"/> is satisfied by at
    /// least one owner permanent — the SAME <c>canLinkSomewhere</c> scan <see cref="CanLinkToTargetPermanent"/>
    /// performs inline, exposed here as the AS-IS-named surface. <paramref name="allowBreeding"/> widens the
    /// scan from owner BATTLE-area Digimon to owner BATTLE+BREEDING permanents (no Digimon filter) — the AS-IS
    /// branch asymmetry. <paramref name="payCost"/> would add the MaxMemoryCost vs GetChangedLinkCost check
    /// (CardSource.cs:3149/3175); no headless folded-link-cost primitive exists (design item C2-02 /
    /// MIG5-CANLINK-PAYCOST), so <c>payCost: true</c> throws.</summary>
    public bool CanLink(bool payCost = false, bool allowBreeding = false)
    {
        LinkCondition? link = LinkConditionOf();
        if (link is null)
        {
            return false;
        }

        if (payCost)
        {
            throw new NotSupportedException(
                "CardSource.CanLink(payCost: true) has no headless GetChangedLinkCost primitive — design item C2-02 / MIG5-CANLINK-PAYCOST.");
        }

        var zones = (IZoneStateReader)Context.ZoneMover;
        return allowBreeding
            ? zones.GetCards(Owner, ChoiceZone.BattleArea).Concat(zones.GetCards(Owner, ChoiceZone.BreedingArea))
                .Any(id => link.digimonCondition(new Permanent(Context, id, Owner)))
            : zones.GetCards(Owner, ChoiceZone.BattleArea)
                .Any(id => CardEffectCommons.IsOwnerBattleAreaDigimon(this, id)
                    && link.digimonCondition(new Permanent(Context, id, Owner)));
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.HasDigimonColor(color)</c> (CardSource.cs:1580-1585):
    /// <see cref="HasCardColor"/> gated on <see cref="IsDigimon"/> (AS-IS <c>DigimonCardColors</c> == the card's
    /// colours when it is a Digimon, empty otherwise).</summary>
    public bool HasDigimonColor(string color) => IsDigimon && HasCardColor(color);

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.HasOptionColor(color)</c> (CardSource.cs:1587-1592):
    /// gated on <see cref="IsOption"/>; a DUAL card (also Digimon) reads <see cref="DualCardColors"/> instead of
    /// <see cref="CardColors"/> — the base/dual split this file already models.</summary>
    public bool HasOptionColor(string color)
    {
        if (!IsOption)
        {
            return false;
        }

        IReadOnlyList<string> colors = IsDigimon ? DualCardColors : CardColors;
        return colors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>(R1-e) AS-IS <c>CardSource.CanNotEvolve(Permanent targetPermanent)</c> (CardSource.cs:1291-1353):
    /// whether an active <see cref="ICanNotDigivolveEffect"/> forbids THIS card from digivolving onto
    /// <paramref name="targetPermanent"/> — a token on EITHER side always blocks; otherwise the AS-IS three-region
    /// scan (all field permanents → all players → THIS card while not a permanent), gated by <c>CanUse(null)</c>
    /// and <c>CanNotEvolve(targetPermanent, this)</c>. Rehoused from the mirror
    /// <see cref="ContinuousRestrictionGate.EvaluateDigivolve"/> delegation to this AS-IS-literal scan.</summary>
    public bool CanNotEvolve(Permanent targetPermanent)
    {
        ArgumentNullException.ThrowIfNull(targetPermanent);
        if (targetPermanent.IsToken)
        {
            return true;
        }

        if (IsToken)
        {
            return true;
        }

        // card effects that can't digivolve — the effects of permanents
        if (new GameContext(Context).Players
            .Map(player => player.GetFieldPermanents())
            .Flat()
            .Map(permanent => permanent.EffectList(EffectTiming.None))
            .Flat()
            .Some(cardEffect => cardEffect is ICanNotDigivolveEffect
                && cardEffect.CanUse(null)
                && ((ICanNotDigivolveEffect)cardEffect).CanNotEvolve(targetPermanent, this)))
        {
            return true;
        }

        // the effects of players
        if (new GameContext(Context).Players
                .Map(player => player.EffectList(EffectTiming.None))
                .Flat()
                .Some(cardEffect => cardEffect is ICanNotDigivolveEffect
                    && cardEffect.CanUse(null)
                    && ((ICanNotDigivolveEffect)cardEffect).CanNotEvolve(targetPermanent, this)))
        {
            return true;
        }

        // the effects of itself
        if (PermanentOfThisCard().IsEmpty)
        {
            if (EffectList(EffectTiming.None)
                    .Some(cardEffect => cardEffect is ICanNotDigivolveEffect
                        && cardEffect.CanUse(null)
                        && ((ICanNotDigivolveEffect)cardEffect).CanNotEvolve(targetPermanent, this)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>(W6-L / P6C3 re-fold) Mirror of AS-IS <c>CardSource.linkCondition</c> (CardSource.cs:2727-2741):
    /// the first usable <see cref="IAddLinkConditionEffect"/>'s condition from THIS card's live
    /// <see cref="EffectList(EffectTiming)"/> (which already enumerates the dispatched per-card effect class —
    /// the pre-flip dispatch-first/registry-fallback split is superseded by the flip's enumeration model).</summary>
    public LinkCondition? LinkConditionOf()
    {
        foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddLinkConditionEffect link && cardEffect.CanUse(null) &&
                link.GetLinkCondition(this) is LinkCondition found)
            {
                return found;
            }
        }

        return null;
    }

    // ===== (C-Del 3b / RD-P6C2-4) DigiXros requirement =====================================================

    /// <summary>(RD-P6C2-4) 1:1 mirror of AS-IS <c>CardSource.digiXrosCondition</c> (CardSource.cs:2959-2986):
    /// the first usable <see cref="IAddDigiXrosConditionEffect"/>'s <see cref="DigiXrosCondition"/> from THIS
    /// card's live <see cref="EffectList(EffectTiming)"/>(None) — the effect-driven DigiXros material requirement.</summary>
    public DigiXrosCondition digiXrosCondition
    {
        get
        {
            foreach (ICardEffect cardEffect in this.EffectList(EffectTiming.None))
            {
                if (cardEffect is IAddDigiXrosConditionEffect)
                {
                    if (cardEffect.CanUse(null))
                    {
                        DigiXrosCondition digiXrosCondition = ((IAddDigiXrosConditionEffect)cardEffect).GetDigiXrosCondition(this);

                        if (digiXrosCondition != null)
                        {
                            return digiXrosCondition;
                        }
                    }
                }
            }

            return null;
        }
    }

    /// <summary>(RD-P6C2-4) 1:1 mirror of AS-IS <c>CardSource.HasDigiXros</c> (CardSource.cs:2569).</summary>
    public bool HasDigiXros => digiXrosCondition != null;

    /// <summary>(RD-P6C2-4) 1:1 mirror of AS-IS <c>CardSource.IsContainDigiXrosCondition</c> (CardSource.cs:3422-3448):
    /// whether THIS card (which must be the TopCard of its own owner permanent and carry a DigiXros requirement)
    /// counts <paramref name="cardSource"/> as satisfying at least one of its DigiXros condition material slots.</summary>
    public bool IsContainDigiXrosCondition(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (cardSource.Owner == Owner)
            {
                // ADAPTATION: AS-IS `PermanentOfThisCard()` returns a `Permanent` with `.TopCard`; the mirror's
                // same-named accessor returns a `PermanentView`, so bridge to the real mirror `Permanent` via
                // `ICardEffect.ResolvePermanentOfThisCard` (the established idiom, see ICardEffect.cs:450-458).
                Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(this);

                if (permanent != null)
                {
                    if (permanent.TopCard.HasDigiXros)
                    {
                        if (this == permanent.TopCard)
                        {
                            if (digiXrosCondition != null)
                            {
                                if (digiXrosCondition.elements.Count((element) => element.CardCondition(cardSource)) >= 1)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>(C-2 witness / BT22_035) 1:1 mirror of AS-IS <c>CardSource.CanLinkToTargetPermanent(target,
    /// PayCost:false, allowBreeding:false)</c> (CardSource.cs:3337) folded with <c>CanLink(false)</c>
    /// (CardSource.cs:3140): whether THIS card may be attached as a LINK card onto
    /// <paramref name="targetPermanent"/>. The host must be a real (non-token) battle-area — not breeding-area
    /// — permanent, and THIS card must carry a declared <see cref="LinkCondition"/> whose <c>digimonCondition</c>
    /// matches BOTH the host AND (CanLink) at least one owner battle-area Digimon (a battle-area host trivially
    /// satisfies that second clause). PayCost=false skips the memory check; the PayCost / allowBreeding variants
    /// land with their first witness (design item C2-02).</summary>
    public bool CanLinkToTargetPermanent(Permanent? targetPermanent, bool PayCost = false, bool allowBreeding = false)
    {
        if (targetPermanent is null)
        {
            return false;
        }

        // AS-IS: target.TopCard != null && !target.TopCard.IsToken.
        if (targetPermanent.TopCard.IsToken)
        {
            return false;
        }

        var zones = (IZoneStateReader)Context.ZoneMover;
        // AS-IS (CardSource.cs:3343): `allowBreeding || !target.Owner.GetBreedingAreaPermanents().Contains(target)`
        // — a breeding-area permanent is a valid link host ONLY when the caller allows breeding (the rule
        // predicate IsDigimonLackLinkCondition passes true; its trash-list re-filter passes false).
        if (!allowBreeding && zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BreedingArea).Contains(targetPermanent.InstanceId))
        {
            return false;
        }

        // AS-IS this.CanLink(false, allowBreeding) (CardSource.cs:3140): THIS card declares a link condition
        // matched by >= 1 owner permanent — GetBattleAreaDigimons normally, GetFieldPermanents (battle +
        // breeding, NO Digimon filter) when allowBreeding — the AS-IS branch asymmetry, preserved.
        LinkCondition? link = LinkConditionOf();
        if (link is null)
        {
            return false;
        }

        bool canLinkSomewhere = allowBreeding
            ? zones.GetCards(Owner, ChoiceZone.BattleArea).Concat(zones.GetCards(Owner, ChoiceZone.BreedingArea))
                .Any(id => link.digimonCondition(new Permanent(Context, id, Owner)))
            : zones.GetCards(Owner, ChoiceZone.BattleArea)
                .Any(id => CardEffectCommons.IsOwnerBattleAreaDigimon(this, id)
                    && link.digimonCondition(new Permanent(Context, id, Owner)));
        if (!canLinkSomewhere)
        {
            return false;
        }

        // AS-IS PayCost branch (GetChangedLinkCost vs Owner.MaxMemoryCost) = design item C2-02 (lands with its
        // first witness; every in-scope caller passes false, matching the AS-IS rule-process call sites).
        // AS-IS linkCondition.digimonCondition(target).
        return link.digimonCondition(targetPermanent);
    }

    /// <summary>(W6-F / P6C3 re-fold) Mirror of AS-IS <c>CardSource.appFusionCondition</c>
    /// (CardSource.cs:3005-3027 shape): the first usable <see cref="IAddAppFusionConditionEffect"/>'s
    /// condition from THIS card's live <see cref="EffectList(EffectTiming)"/> (dispatch covered by the
    /// flip's enumeration model).</summary>
    public AppFusionCondition? AppFusionConditionOf()
    {
        foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddAppFusionConditionEffect appfusion && cardEffect.CanUse(null) &&
                appfusion.GetAppFusionCondition(this) is AppFusionCondition found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>(AD1-A) Mirror of AS-IS <c>CardSource.HasAssembly</c> (CardSource.cs:2575).</summary>
    public bool HasAssembly => AssemblyConditionOf() is not null;

    /// <summary>(AD1-A) Mirror of AS-IS <c>CardSource.assemblyCondition</c> (CardSource.cs:3043-3065): the
    /// first USABLE <c>IAddAssemblyConditionEffect</c>'s condition for THIS card. The AS-IS accessor reads
    /// the card's own EffectList — zone-independent (a HAND card has it, which is when Assembly matters) —
    /// so the primary source is the card's dispatched effect class; registered bindings are the fallback
    /// (test fixtures / registry-only setups).</summary>
    public AssemblyCondition? AssemblyConditionOf()
    {
        // (P6C3 re-fold) AS-IS assemblyCondition (CardSource.cs:3043-3065): first usable
        // IAddAssemblyConditionEffect from the live EffectList scan (dispatch covered by the flip's
        // enumeration model; the pre-flip dispatch-first/registry-fallback split is superseded).
        foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddAssemblyConditionEffect assembly && cardEffect.CanUse(null) &&
                assembly.GetAssemblyCondition(this) is AssemblyCondition found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>(P6C3) AS-IS <c>CardSource.IsBeingRevealed</c> (CardSource.cs:3565, a public auto-property
    /// on the persistent Unity component). The mirror CardSource is a transient view, so the flag lives in
    /// instance metadata (the <c>IsSuspended</c> setter pattern). Design item RD-P6C3-A2: the AS-IS WRITERS
    /// (the reveal pipeline stamping the flag around a reveal) are unported — until that slice lands the
    /// flag is only ever its default false, exactly like an AS-IS card that is not mid-reveal.</summary>
    public bool IsBeingRevealed
    {
        get => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue("isBeingRevealed", out object? raw) && raw is true;
        set
        {
            if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    ["isBeingRevealed"] = value,
                };
                Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(P6C3) AS-IS <c>CardSource.PermanentJustBeforeRemoveField</c> (CardSource.cs:3571, a public
    /// auto-property: the permanent this card belonged to just before it left the field — read by the
    /// OnDeletion Hashtable gates). Transient-view carrier = a per-match service store keyed by InstanceId
    /// (the <c>Permanent.oldIsTapped_playCard</c> substrate pattern). Design item RD-P6C3-A3: the AS-IS
    /// WRITER (CardController stamps it right before RemoveFromAllArea) belongs to the unported
    /// CardController deletion slice — until it lands the property is null, as for an AS-IS card that never
    /// left the field.</summary>
    public Permanent? PermanentJustBeforeRemoveField
    {
        get => Context.TryGetService(out PermanentJustBeforeRemoveFieldStore? store) && store is not null
            && store.Values.TryGetValue(InstanceId, out Permanent? permanent) ? permanent : null;
        set
        {
            if (!Context.TryGetService(out PermanentJustBeforeRemoveFieldStore? store) || store is null)
            {
                store = new PermanentJustBeforeRemoveFieldStore();
                Context.RegisterService(store);
            }

            store.Values[InstanceId] = value;
        }
    }

    /// <summary>(P6C3) Per-match backing store for <see cref="PermanentJustBeforeRemoveField"/>.</summary>
    private sealed class PermanentJustBeforeRemoveFieldStore
    {
        public Dictionary<HeadlessEntityId, Permanent?> Values { get; } = new Dictionary<HeadlessEntityId, Permanent?>();
    }

    /// <summary>(P6C3) AS-IS <c>CardSource.HasSaveText</c> (CardSource.cs:2181 =
    /// <c>HasText("&lt;Save&gt;")</c>, a printed-text scan). The mirror carries no rules text; the
    /// established mirror carrier of "&lt;Save&gt; is on this card" is the instance <c>hasSave</c> metadata
    /// flag OR a live Save keyword grant — exactly the pair the live deletion-replacement pipeline gates on
    /// (<see cref="Headless.Runtime.DeletionReplacementGate.TrySaveAsync"/>).</summary>
    public bool HasSaveText =>
        Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? saveRecord) && saveRecord is not null
        && Headless.Runtime.DeletionReplacementGate.HasReplacementKeyword(
            saveRecord, Headless.Runtime.DeletionReplacementGate.HasSaveKey, Headless.Runtime.ContinuousKeywordGate.Save, Context.EffectRegistry);

    // (R1-e boundary) The AS-IS printed-cost engine — CardSource.EvoCosts's BaseEvoCostsFromEntity projection,
    // CostList, PayingCost/GetPayingCostWithBaseCost, GetChangedLinkCost's value fold — is NOT rehoused here: it
    // needs the EvoCost value type + _cEntity_Base.EvoCosts card data and drives the play/digivolve cost pipeline
    // (PlayCardClass STOP RD-P6C1-2, DigivolutionCostHelpers/LinkHelpers) = R2 몫 (플레이 파이프라인 내부 판정).
    // What R1-e keeps on CardSource is the READ-layer scan portion: the added-requirement scan below
    // (AddedDigivolutionCosts) + IgnoreColorConditionActive + MatchColorRequirement.

    // ===== (RD-P6B-15) added digivolution requirement — AS-IS CardSource.EvoCosts / CostList ==============
    // AS-IS CardSource.EvoCosts (DCGO CardSource.cs:534-611) + CostList (:617-627). The ADDED-requirement
    // portion only — the three `IAddDigivolutionRequirementEffect` scan regions (players' EffectList, all
    // field permanents' EffectList, and THIS card's own EffectList while it is not a permanent / is a source
    // of its own permanent). For each usable effect, `GetEvoCost(targetPermanent, this, ignore,
    // checkAvailability)` is evaluated 1:1; a return >= 0 is a matching added digivolution path at that memory
    // cost (the closure itself gates color / level-range / permanentCondition / cardCondition, exactly like
    // the AS-IS AddDigivolutionRequirementClass). The printed `BaseEvoCostsFromEntity` region of AS-IS EvoCosts
    // is NOT mirrored here — that is the mirror's own EvolutionCondition / DigivolutionCostHelpers path
    // (DigivolveAction). The new-model kind-class registers no binding, so this live interface scan is the
    // ONLY way the added path becomes observable; DigivolveAction UNIONs it with its legacy
    // AddedDigivolutionRequirementEffect (registry-key) consumer.

    /// <summary>(RD-P6B-15) The memory costs of every ADDED digivolution path (AS-IS
    /// <c>CardSource.EvoCosts</c>/<c>CostList</c>, the <c>IAddDigivolutionRequirementEffect</c> scan) for
    /// digivolving THIS card onto <paramref name="targetPermanent"/> (each entry = a usable effect whose
    /// <c>GetEvoCost</c> returned &gt;= 0). Empty = no added path applies.</summary>
    public List<int> AddedDigivolutionCosts(Permanent targetPermanent, CardEffectCommons.IgnoreRequirement ignore, bool checkAvailability)
    {
        var costs = new List<int>();

        void Collect(IEnumerable<ICardEffect> effects)
        {
            foreach (ICardEffect cardEffect in effects)
            {
                if (cardEffect is IAddDigivolutionRequirementEffect requirement && cardEffect.CanUse(null))
                {
                    int cost = requirement.GetEvoCost(targetPermanent, this, ignore, checkAvailability);
                    if (cost >= 0)
                    {
                        costs.Add(cost);
                    }
                }
            }
        }

        // (AS-IS region 1) the effects of players — the mirror player-grant store is empty today
        // (design item P6A-PLAYER-EFFECTLIST), so this contributes nothing until GiveEffectToPlayer flips.
        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            Collect(player.EffectList(EffectTiming.None));
        }

        // (AS-IS region 2) the effects of all field permanents (both players, turn-player first).
        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                Collect(permanent.EffectList(EffectTiming.None));
            }
        }

        // (AS-IS region 3) the effects of ITSELF — only while this card is not a permanent, or is a
        // digivolution source of its own permanent (AS-IS `PermanentOfThisCard() == null ||
        // PermanentOfThisCard().DigivolutionCards.Contains(this)`).
        PermanentView thisPermanent = PermanentOfThisCard();
        if (thisPermanent.IsEmpty || thisPermanent.DigivolutionCards.Any(under => under.InstanceId == InstanceId))
        {
            Collect(EffectList(EffectTiming.None));
        }

        return costs;
    }

    // ===== (RD-P6B-17) ignore-color requirement — AS-IS CardSource.MatchColorRequirement scan ============
    // AS-IS CardSource.MatchColorRequirement (DCGO CardSource.cs:261-303), the IIgnoreColorConditionEffect
    // regions: whether an active "ignore color requirement" effect (UseRequirements -> IgnoreColorConditionClass)
    // waives THIS card's color requirement. Scans gameContext.Players (FULL roster) field permanents' +
    // players' + this card's OWN EffectList(None) for IIgnoreColorConditionEffect, gated by CanUse(null) &&
    // IgnoreColorCondition(this) — any match => waived. The new-model kind-class registers no binding, so this
    // live interface scan is the only way it becomes observable; DigivolveAction's color-ignore check UNIONs it
    // with its legacy IgnoreColorRequirementKey consumer.

    /// <summary>(RD-P6B-17) Whether an active <see cref="IIgnoreColorConditionEffect"/> waives THIS card's
    /// colour requirement (AS-IS <c>CardSource.MatchColorRequirement</c>'s ignore-colour scan).</summary>
    public bool IgnoreColorConditionActive()
    {
        bool Some(IEnumerable<ICardEffect> effects) =>
            effects.Any(cardEffect => cardEffect is IIgnoreColorConditionEffect ignore
                && cardEffect.CanUse(null)
                && ignore.IgnoreColorCondition(this));

        // (AS-IS region 1) the effects of permanents.
        foreach (Player player in new GameContext(Context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                if (Some(permanent.EffectList(EffectTiming.None)))
                {
                    return true;
                }
            }
        }

        // (AS-IS region 2) the effects of players — the mirror player-grant store is empty today.
        foreach (Player player in new GameContext(Context).Players)
        {
            if (Some(player.EffectList(EffectTiming.None)))
            {
                return true;
            }
        }

        // (AS-IS region 3) the effects of itself.
        return Some(EffectList(EffectTiming.None));
    }
}


// ===== (EFFECT-MODEL REBUILD / path-fidelity) AS-IS places every play-condition type in CardSource.cs as
// separate top-level classes (CardSource.cs:4182-4358). Relocated here from the mirror-invented Conditions.cs
// so the file grouping matches AS-IS 1:1. =====================================================================

/// <summary>(AD1-A) 1:1 mirror of AS-IS <c>AssemblyConditionElement</c> (CardSource.cs:4339-4358): one
/// material slot of an Assembly condition — an arbitrary card predicate, a required count, and an optional
/// gate against the already-selected materials.</summary>
public sealed class AssemblyConditionElement
{
    public AssemblyConditionElement(
        Func<CardSource, bool> cardCondition,
        bool skipAllIfNoSelect = true,
        string? selectMessage = null,
        int elementCount = 0,
        Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList = null)
    {
        CardCondition = cardCondition ?? throw new ArgumentNullException(nameof(cardCondition));
        this.skipAllIfNoSelect = skipAllIfNoSelect;
        this.selectMessage = selectMessage ?? string.Empty;
        ElementCount = elementCount;
        this.CanTargetCondition_ByPreSelecetedList = CanTargetCondition_ByPreSelecetedList;
    }

    public Func<CardSource, bool> CardCondition { get; }
    public bool skipAllIfNoSelect { get; }
    public int ElementCount { get; }
    public Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList { get; }
    public string selectMessage { get; }
}


/// <summary>(AD1-A) 1:1 mirror of AS-IS <c>AssemblyCondition</c> (CardSource.cs:4313-4337): the material
/// element list plus ONE flat <c>reduceCost</c>, applied only when the FULL set is assembled. Materials come
/// from the OWNER'S TRASH and end up UNDER the played permanent as digivolution cards.</summary>
public sealed class AssemblyCondition
{
    /// <summary>Old single-condition form ("1 condition × N times").</summary>
    public AssemblyCondition(
        AssemblyConditionElement element,
        Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList,
        string? selectMessage, int elementCount, int reduceCost)
    {
        ArgumentNullException.ThrowIfNull(element);
        elements = new List<AssemblyConditionElement>
        {
            new(element.CardCondition, element.skipAllIfNoSelect, selectMessage ?? element.selectMessage,
                elementCount, CanTargetCondition_ByPreSelecetedList ?? element.CanTargetCondition_ByPreSelecetedList),
        };
        this.elementCount = elementCount;
        this.reduceCost = reduceCost;
    }

    /// <summary>The A×B×C… DigiXros-like form (each element carries its own count).</summary>
    public AssemblyCondition(List<AssemblyConditionElement> elements, int reduceCost)
    {
        this.elements = elements ?? throw new ArgumentNullException(nameof(elements));
        elementCount = elements.Sum(element => element.ElementCount);
        this.reduceCost = reduceCost;
    }

    public List<AssemblyConditionElement> elements { get; }
    public int elementCount { get; }
    public int reduceCost { get; }
}


/// <summary>(W6-L) 1:1 mirror of AS-IS <c>LinkCondition</c> (CardSource.cs:4286): "this card may LINK onto
/// an owner battle-area Digimon matching <c>digimonCondition</c>, paying <c>cost</c> memory". LinkDP is NOT
/// declared here — it is per-card data (definition metadata <c>linkDP</c>, folded by LinkHelpers).</summary>
public sealed class LinkCondition
{
    public LinkCondition(Func<Permanent, bool> digimonCondition, int cost)
    {
        this.digimonCondition = digimonCondition ?? throw new ArgumentNullException(nameof(digimonCondition));
        this.cost = cost;
    }

    public Func<Permanent, bool> digimonCondition { get; }
    public int cost { get; }
}


/// <summary>(W6-F) 1:1 mirror of AS-IS <c>AppFusionCondition</c> (CardSource.cs:4298): "may App-Fuse onto an
/// owner Digimon whose TOP matches one material and one of whose LINK cards matches a DIFFERENT material,
/// paying <c>cost</c>". Executed as an EVOLUTION (the chosen link card joins the fused sources).</summary>
public sealed class AppFusionCondition
{
    public AppFusionCondition(Func<Permanent, CardSource, bool> linkedCondition, Func<Permanent, bool> digimonCondition, int cost)
    {
        this.linkedCondition = linkedCondition ?? throw new ArgumentNullException(nameof(linkedCondition));
        this.digimonCondition = digimonCondition ?? throw new ArgumentNullException(nameof(digimonCondition));
        this.cost = cost;
    }

    public Func<Permanent, CardSource, bool> linkedCondition { get; }
    public Func<Permanent, bool> digimonCondition { get; }
    public int cost { get; }
}


/// <summary>1:1 mirror of AS-IS <c>JogressConditionElement</c> (CardSource.cs:4204-4229): one evo-root slot of
/// a Jogress (DNA-digivolution) condition — a permanent predicate plus its selection message.
/// <c>EvoRootCondition</c> wraps the raw predicate so a null predicate reads as "any permanent qualifies".</summary>
public sealed class JogressConditionElement
{
    public JogressConditionElement(Func<Permanent, bool> evoRootCondition, string selectMessage)
    {
        EvoRootCondition = evoRootCondition;
        SelectMessage = selectMessage;
    }

    Func<Permanent, bool>? _evoRootCondition = null;

    public Func<Permanent, bool> EvoRootCondition
    {
        get { return permanent => _evoRootCondition == null || _evoRootCondition(permanent); }
        private set { _evoRootCondition = value; }
    }

    public string SelectMessage { get; private set; } = "";
}


/// <summary>1:1 mirror of AS-IS <c>JogressCondition</c> (CardSource.cs:4182-4202): exactly TWO evo-root slots
/// plus a memory <c>cost</c>. The ctor's guarded copy (only copies when the incoming array length matches the
/// fixed 2) is preserved verbatim.</summary>
public sealed class JogressCondition
{
    public JogressCondition(JogressConditionElement[] elements, int cost)
    {
        this.elements = new JogressConditionElement[2];

        if (this.elements.Length == elements.Length)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                this.elements[i] = elements[i];
            }
        }

        this.cost = cost;
    }

    public JogressConditionElement[] elements { get; private set; } = new JogressConditionElement[2];
    public int cost { get; private set; } = 0;
}


/// <summary>1:1 mirror of AS-IS <c>DigiXrosConditionElement</c> (CardSource.cs:4252-4265): one material slot of
/// a DigiXros — a card predicate, a selection message, and whether failing to select this slot skips the whole
/// DigiXros.</summary>
public sealed class DigiXrosConditionElement
{
    public DigiXrosConditionElement(Func<CardSource, bool> cardCondition, string selectMessage, bool skipAllIfNoSelect = false)
    {
        CardCondition = cardCondition;
        this.selectMessage = selectMessage;
        this.skipAllIfNoSelect = skipAllIfNoSelect;
    }

    public Func<CardSource, bool> CardCondition { get; private set; }
    public string selectMessage { get; private set; } = "";
    public bool skipAllIfNoSelect { get; private set; } = false;
}


/// <summary>1:1 mirror of AS-IS <c>DigiXrosCondition</c> (CardSource.cs:4231-4250): the material element list,
/// an optional cross-material gate against the already-selected set, and a per-material memory cost reduction.</summary>
public sealed class DigiXrosCondition
{
    public DigiXrosCondition(List<DigiXrosConditionElement> elements, Func<List<CardSource>, CardSource, bool> CanTargetCondition_ByPreSelecetedList, int reduceCostPerCard)
    {
        this.elements = new List<DigiXrosConditionElement>();

        foreach (DigiXrosConditionElement element in elements)
        {
            this.elements.Add(element);
        }

        this.CanTargetCondition_ByPreSelecetedList = CanTargetCondition_ByPreSelecetedList;
        this.reduceCostPerCard = reduceCostPerCard;
    }

    public List<DigiXrosConditionElement> elements { get; private set; } = new List<DigiXrosConditionElement>();
    public Func<List<CardSource>, CardSource, bool> CanTargetCondition_ByPreSelecetedList { get; private set; }
    public int reduceCostPerCard { get; private set; } = 0;
}


/// <summary>1:1 mirror of AS-IS <c>BurstDigivolutionCondition</c> (CardSource.cs:4267-4284): a burst-digivolve
/// onto a matching tamer + matching digimon for a memory <c>cost</c>, each side carrying its own predicate and
/// selection message.</summary>
public sealed class BurstDigivolutionCondition
{
    public BurstDigivolutionCondition(Func<Permanent, bool> tamerCondition, string selectTamerMessage, Func<Permanent, bool> digimonCondition, string selectDigimonMessage, int cost)
    {
        this.tamerCondition = tamerCondition;
        this.selectTamerMessage = selectTamerMessage;
        this.digimonCondition = digimonCondition;
        this.selectDigimonMessage = selectDigimonMessage;
        this.cost = cost;
    }

    public Func<Permanent, bool> tamerCondition { get; private set; }
    public string selectTamerMessage { get; private set; }
    public Func<Permanent, bool> digimonCondition { get; private set; }
    public string selectDigimonMessage { get; private set; }

    public int cost { get; private set; } = 0;
}

