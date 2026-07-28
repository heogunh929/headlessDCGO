namespace HeadlessDCGO.Engine.Headless.Services;

/// <summary>Substrate home for zone-move / instance metadata keys that outlive the retired
/// <c>MatchStateMutationSink</c>. Only keys with a LIVE reader are re-homed here; the sink-era
/// batch-dedup / cause-effect keys (AddHand/Discard/AddSecurity/SecurityLoss batch + cause + revealTrash)
/// were retired together with their consumer (the never-built <c>WindowResolverWiring
/// .CollectActivatedBridgeTriggers</c> activated-bridge collapse), so they are NOT re-homed.</summary>
public static class ZoneMoveMetadataKeys
{
    /// <summary>Instance-metadata flag stamped when a card enters the field this turn (the AS-IS
    /// "entered this turn" bit). Live readers: the digivolution inheritance carry (mirror
    /// <c>Permanent</c>) and the enter-this-turn scan in <c>CardEffectCommons</c>; written on field
    /// entry (<c>CardObjectController.CreateNewPermanent</c>) and cleared at turn boundary
    /// (<c>TurnFlowPump</c>). String value is byte-identical to the retired sink constant
    /// (<c>"enteredThisTurn"</c>) so the literal-string readers in <c>Permanent</c> keep matching.</summary>
    public const string EnteredThisTurnKey = "enteredThisTurn";
}
