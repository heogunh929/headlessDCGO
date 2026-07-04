namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Text;

// TODO: Replace this coarse fingerprint with a versioned parity hash after full rule state is ported.
public sealed record HeadlessEpisodeFingerprint(
    string ScenarioName,
    int StepCount,
    bool IsTerminal,
    HeadlessEpisodeStopReason StopReason,
    double TotalReward,
    string Value)
{
    public static HeadlessEpisodeFingerprint FromEpisode(HeadlessEpisodeResult episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        StableEpisodeHasher hasher = new();
        RlEpisodeSampleBatch batch = episode.ToSampleBatch();

        hasher.AddString(episode.ScenarioName);
        hasher.AddInt(episode.StepCount);
        hasher.AddBool(episode.IsTerminal);
        hasher.AddString(episode.StopReason.ToString());
        hasher.AddDouble(episode.TotalReward);
        hasher.AddBool(episode.FinalState.Result?.IsDraw == true);
        hasher.AddBool(episode.FinalState.Result?.IsSurrender == true);
        hasher.AddInt(episode.FinalState.Result?.WinnerId?.Value ?? -1);
        hasher.AddString(episode.FinalState.Result?.Reason ?? string.Empty);
        hasher.AddStrings(batch.Schema.ObservationFeatureNames);
        hasher.AddStrings(batch.Schema.ActionSlotNames);

        foreach (RlTransitionSample sample in batch.Samples)
        {
            AddSample(hasher, sample);
        }

        return new HeadlessEpisodeFingerprint(
            episode.ScenarioName,
            episode.StepCount,
            episode.IsTerminal,
            episode.StopReason,
            episode.TotalReward,
            hasher.ToHexString());
    }

    private static void AddSample(
        StableEpisodeHasher hasher,
        RlTransitionSample sample)
    {
        hasher.AddInt(sample.StepIndex);
        hasher.AddString(sample.ActionId);
        hasher.AddInt(sample.PlayerId);
        hasher.AddString(sample.ActionType);
        hasher.AddInt(sample.ActionIndex);
        hasher.AddString(sample.EncodedActionKey);
        hasher.AddDouble(sample.Reward);
        hasher.AddDouble(sample.Discount);
        hasher.AddBool(sample.IsTerminal);
        hasher.AddBool(sample.ActionProcessed);
        hasher.AddBool(sample.ActionRejected);
        hasher.AddDoubles(sample.Observation);
        hasher.AddDoubles(sample.ActionMask);
        hasher.AddDoubles(sample.ActionCounts);
        hasher.AddDoubles(sample.NextObservation);
        hasher.AddDoubles(sample.NextActionMask);
        hasher.AddDoubles(sample.NextActionCounts);
    }

    private sealed class StableEpisodeHasher
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private ulong _hash = OffsetBasis;

        public void AddBool(bool value)
        {
            AddByte(value ? (byte)1 : (byte)0);
            AddByte(0xff);
        }

        public void AddInt(int value)
        {
            AddLong(value);
        }

        public void AddLong(long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                for (int index = 0; index < 8; index++)
                {
                    AddByte((byte)(unsignedValue >> (index * 8)));
                }
            }

            AddByte(0xfe);
        }

        public void AddDouble(double value)
        {
            AddLong(BitConverter.DoubleToInt64Bits(value));
        }

        public void AddDoubles(IReadOnlyList<double> values)
        {
            AddInt(values.Count);
            foreach (double value in values)
            {
                AddDouble(value);
            }
        }

        public void AddStrings(IReadOnlyList<string> values)
        {
            AddInt(values.Count);
            foreach (string value in values)
            {
                AddString(value);
            }
        }

        public void AddString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            AddInt(bytes.Length);
            AddBytes(bytes);
            AddByte(0xfd);
        }

        public string ToHexString()
        {
            return _hash.ToString("x16");
        }

        private void AddBytes(IReadOnlyList<byte> bytes)
        {
            foreach (byte value in bytes)
            {
                AddByte(value);
            }
        }

        private void AddByte(byte value)
        {
            unchecked
            {
                _hash ^= value;
                _hash *= Prime;
            }
        }
    }
}
