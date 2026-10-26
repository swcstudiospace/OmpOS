using System.Security.Cryptography;
using System.Text;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// One message the operator sent while a run was active, as the Steering history shows it. The text is capped for display; the length
/// and a short hash of the full text are kept so a recorder can describe the message without ever receiving the text (the Substrate
/// record carries the hash prefix, never the message).
/// </summary>
public sealed class SteeringEntry : ObservableModel
{
    private const int HashPrefixHexChars = 12;

    private SteeringRecordStatus _recordStatus;
    private string _timeText = string.Empty;

    internal SteeringEntry(
        string id,
        SteeringMode mode,
        string text,
        DateTimeOffset at,
        SteeringDeliveryStatus deliveryStatus,
        SteeringRecordStatus recordStatus,
        DateTimeOffset now,
        string? failureReason = null)
    {
        Id = id;
        Mode = mode;
        Text = PanelText.Plain(text, SteeringInputModel.MaxTextChars);
        Length = text.Length;
        TextSha256Prefix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..HashPrefixHexChars];
        At = at;
        DeliveryStatus = deliveryStatus;
        FailureReason = failureReason is null ? null : PanelText.Plain(failureReason, SteeringInputModel.MaxTextChars);
        _recordStatus = recordStatus;
        _timeText = RelativeTimeText.Format(at, now);
    }

    /// <summary>Gets the entry identifier, unique within its session.</summary>
    public string Id { get; }

    /// <summary>Gets how the message was sent.</summary>
    public SteeringMode Mode { get; }

    /// <summary>Gets <c>Steer</c> or <c>Follow-up</c>.</summary>
    public string KindText => Mode == SteeringMode.Steer ? AgentPanelCopy.KindSteer : AgentPanelCopy.KindFollowUp;

    /// <summary>Gets the message text as plain text capped at <see cref="SteeringInputModel.MaxTextChars"/> characters.</summary>
    public string Text { get; }

    /// <summary>Gets the length in characters of the message as sent.</summary>
    public int Length { get; }

    /// <summary>Gets the first 12 hex characters of the SHA-256 of the UTF-8 message as sent.</summary>
    public string TextSha256Prefix { get; }

    /// <summary>Gets the time the message was accepted.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Gets what the input channel did with the message.</summary>
    public SteeringDeliveryStatus DeliveryStatus { get; }

    /// <summary>Gets the plain reason the message was not delivered, or null unless <see cref="DeliveryStatus"/> is <see cref="SteeringDeliveryStatus.Failed"/>.</summary>
    public string? FailureReason { get; }

    /// <summary>Gets the delivery status in words. A failed delivery carries its reason.</summary>
    public string DeliveryText => DeliveryStatus switch
    {
        SteeringDeliveryStatus.Delivered => AgentPanelCopy.DeliveryDelivered,
        SteeringDeliveryStatus.HeldUntilTurnEnds => AgentPanelCopy.DeliveryHeld,
        SteeringDeliveryStatus.SentNotAcknowledged => AgentPanelCopy.DeliverySentNotAcknowledged,
        SteeringDeliveryStatus.Failed => AgentPanelCopy.DeliveryFailed(FailureReason),
        _ => AgentPanelCopy.DeliveryQueued,
    };

    /// <summary>Gets whether the message was recorded in Substrate.</summary>
    public SteeringRecordStatus RecordStatus => _recordStatus;

    /// <summary>Gets the record status in words.</summary>
    public string RecordText => _recordStatus switch
    {
        SteeringRecordStatus.Recorded => AgentPanelCopy.RecordRecorded,
        SteeringRecordStatus.NotRecorded => AgentPanelCopy.RecordNotRecorded,
        SteeringRecordStatus.RecordFailed => AgentPanelCopy.RecordFailed,
        _ => AgentPanelCopy.RecordInFlight,
    };

    /// <summary>Gets the relative time of the message, for example <c>2 min ago</c>.</summary>
    public string TimeText => _timeText;

    /// <summary>Gets the first line of the entry: <c>{Steer or Follow-up} · {relative time}</c>.</summary>
    public string HeaderText => $"{KindText} \u00B7 {_timeText}";

    /// <summary>Gets the status line: the delivery status and the record status separated by <c> · </c>. A message that was not delivered is not recorded, so its line is the delivery text only.</summary>
    public string StatusLine => DeliveryStatus == SteeringDeliveryStatus.Failed ? DeliveryText : $"{DeliveryText} \u00B7 {RecordText}";

    internal void SetRecordStatus(SteeringRecordStatus status)
    {
        if (SetProperty(ref _recordStatus, status, nameof(RecordStatus)))
        {
            NotifyPropertyChanged(nameof(RecordText));
            NotifyPropertyChanged(nameof(StatusLine));
        }
    }

    internal void RefreshTime(DateTimeOffset now)
    {
        if (SetProperty(ref _timeText, RelativeTimeText.Format(At, now), nameof(TimeText)))
        {
            NotifyPropertyChanged(nameof(HeaderText));
        }
    }
}
