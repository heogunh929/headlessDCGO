namespace HeadlessDCGO.Engine.Headless.Runtime;

// (M1) 경계 누수 정리 — 설계 §4: 도메인 객체 → 텐서 인코딩은 어댑터의 몫이므로 DcgoMatch의
// Encode* 편의 메서드를 엔진 파사드에서 이 어셈블리의 확장 메서드로 이동(호출 문법 동일).
public static class DcgoMatchEncodingExtensions
{
    public static EncodedObservation EncodeObservation(
        this DcgoMatch match,
        ObservationEncodingOptions? options = null)
    {
        return new ObservationEncoder(options).Encode(match.GetObservation());
    }

    public static EncodedActionMask EncodeActionMask(
        this DcgoMatch match,
        ActionEncodingOptions? options = null)
    {
        return new ActionEncoder(options).Encode(match.GetActionMask());
    }

    // G3.5-RL-A3: fixed factored action mask where each concrete legal action (per card / target /
    // choice candidate) occupies a distinct index.
    public static FactoredActionMask EncodeFactoredActionMask(
        this DcgoMatch match,
        FactoredActionSchema? schema = null)
    {
        return FactoredActionEncoder.Encode(
            match.GetActionMask().LegalActions,
            FactoredPositionContext.FromContext(match.Context),
            schema);
    }
}
