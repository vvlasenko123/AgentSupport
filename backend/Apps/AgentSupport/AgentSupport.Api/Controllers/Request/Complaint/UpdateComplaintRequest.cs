using System.ComponentModel.DataAnnotations;

namespace AgentSupport.Api.Controllers.Request.Complaint;

/// <summary>
/// Запрос на обновление жалобы
/// </summary>
public sealed class UpdateComplaintRequest
{
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
    [Phone]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Адрес электронной почты отправителя
    /// </summary>
    [EmailAddress]
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
    /// Суть вопроса (выжимка из письма)
    /// </summary>
    public string IssueSummary { get; set; } = string.Empty;

    /// <summary>
    /// Статус жалобы
    /// </summary>
    public string Status { get; set; } = "open";
}
