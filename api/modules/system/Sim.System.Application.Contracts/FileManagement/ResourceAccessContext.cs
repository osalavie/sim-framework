namespace Sim.System.FileManagement;

/// <summary>
/// Request identity context used for private-resource auditing and share-link quotas.
/// Raw fingerprints are used only in memory; repositories store hashes.
/// </summary>
public sealed record ResourceAccessContext(
    string? VisitorFingerprint = null,
    string? ClientIp = null,
    string? UserAgent = null);
