namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// One image a tool-call update carried (D-27). A refused payload is a placeholder: <see cref="DataBase64"/> is empty
/// and <see cref="Description"/> is kept.
/// </summary>
/// <param name="MimeType">The declared media type.</param>
/// <param name="DataBase64">The base64 payload, or empty when the payload was refused.</param>
/// <param name="Description">The optional description the update carried, used as alt text.</param>
public sealed record ToolUpdateImage(string MimeType, string DataBase64, string? Description);
