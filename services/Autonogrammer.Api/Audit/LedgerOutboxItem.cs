namespace Autonogrammer.Api.Audit;

/// <summary>One undelivered outbox row and the event it notarises.</summary>
/// <param name="Id">The outbox row id.</param>
/// <param name="Attempts">How many sends have already failed.</param>
/// <param name="Audit">The closed event.</param>
internal sealed record LedgerOutboxItem(long Id, int Attempts, AuditLedgerEvent Audit);
