namespace AgentSupport.Application.UseCases.Complaints.Command;

/// <summary>
/// Команда создания жалобы
/// </summary>
public sealed class CreateComplaintCommand
{
    /// <summary>
    /// Дата и время поступления
    /// </summary>
    public DateTime? SubmissionDate { get; set; }

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

    /// <summary>
    /// Статус жалобы
    /// </summary>
    public string Status { get; set; } = "open";

    /// <summary>
    /// Категория обращения
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Предложенный ответ от ML
    /// </summary>
    public string? SuggestedAnswer { get; set; }
}