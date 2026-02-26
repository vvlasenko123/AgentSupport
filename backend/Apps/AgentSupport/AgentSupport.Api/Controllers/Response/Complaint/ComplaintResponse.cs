namespace AgentSupport.Api.Controllers.Response.Complaint;

/// <summary>
/// Ответ с данными жалобы
/// </summary>
public sealed class ComplaintResponse
{
    /// <summary>
    /// Айди
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Дата и время поступления
    /// </summary>
    public DateTime SubmissionDate { get; set; }

    /// <summary>
    /// ФИО
    /// </summary>
    public string Fio { get; set; } = string.Empty;

    /// <summary>
    /// Название объекта или компании
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Номер телефона
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Адрес электронной почты отправителя
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Заводские номера, указанные в письме
    /// </summary>
    public string[] SerialNumbers { get; set; } = [];

    /// <summary>
    /// Тип устройства или прибора
    /// </summary>
    public string? DeviceType { get; set; }

    /// <summary>
    /// Эмоциональный окрас письма
    /// </summary>
    public string? EmotionalTone { get; set; }

    /// <summary>
    /// Суть вопроса, выжимка из письма
    /// </summary>
    public string IssueSummary { get; set; } = string.Empty;
}