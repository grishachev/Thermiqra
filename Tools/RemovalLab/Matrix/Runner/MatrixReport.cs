namespace Thermiqra.RemovalLab;

internal sealed record Check(string Description, bool Passed);
internal sealed class CaseResult(string id)
{
    public string Id { get; set; } = id;
    public string Status { get; set; } = "NotRun";
    public string Detail { get; set; } = "";
    public string BaselineId { get; set; } = "";
    public List<Check> Checks { get; set; } = new();
    public void Require(bool passed, string description)
    { Checks.Add(new(description, passed)); if (!passed) throw new InvalidOperationException(description); }
}
internal sealed class MatrixReport
{
    public string Status { get; set; } = "NotRun";
    public string Environment { get; set; } = "";
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedUtc { get; set; }
    public List<CaseResult> Cases { get; set; } = new[] { "Normal", "PartialError", "Error", "Cancel", "Helper", "Locked", "Denied", "Shared", "Reparse", "Restore", "Collision", "MsiUser", "MsiMachine", "MsiCancel", "MsiError", "MsiReboot" }.Select(id => new CaseResult(id)).ToList();
}
