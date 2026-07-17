namespace RushOrder.Application.Experiments;

// Deterministic FNV-1a hash -> bucket 0-99. The same device fingerprint always
// lands in the same bucket, so a given customer keeps seeing the same variant
// across visits/sessions without persisting an assignment row.
public static class ExperimentBucketing
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    public static int ComputeBucket(string deviceFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFingerprint);

        unchecked
        {
            var hash = OffsetBasis;
            foreach (var c in deviceFingerprint)
            {
                hash ^= c;
                hash *= Prime;
            }
            return (int)(hash % 100);
        }
    }
}
