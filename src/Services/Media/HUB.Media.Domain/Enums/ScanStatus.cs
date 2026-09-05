namespace HUB.Media.Domain.Enums;

/// <summary>Virus-scan lifecycle of an uploaded object.</summary>
public enum ScanStatus
{
    /// <summary>Uploaded, not yet scanned — download blocked.</summary>
    Pending = 0,
    /// <summary>Scanned clean — safe to download.</summary>
    Clean = 1,
    /// <summary>Flagged malicious — download blocked.</summary>
    Infected = 2,
}
