using GrowthTracker.Domain.Common;

namespace GrowthTracker.Domain.PlanLogs;

/// <summary>Append-only XP ledger: entries are never edited or removed (BR-07).</summary>
public sealed class XpLedgerEntry : Entity
{
    private XpLedgerEntry() { }

    public XpLedgerEntry(Guid planLogId, Guid areaId, decimal amount, string reason)
    {
        PlanLogId = planLogId;
        AreaId = areaId;
        Amount = amount;
        Reason = reason;
    }

    public Guid PlanLogId { get; private set; }
    public Guid AreaId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = "";
}
