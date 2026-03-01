namespace Infrastructure.Broker.Kafka.Contracts;

/// <summary>
/// Данные email
/// </summary>
public sealed class EmailReceivedRpcRequest
{
    /// <summary>
    /// Айди сообщения
    /// </summary>
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя отправителя
    /// </summary>
    public string FromName { get; set; } = string.Empty;
    
    /// <summary>
    /// Почта отправителя
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Тема
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Время отправки
    /// </summary>
    public DateTime SentAtUtc { get; set; }
    
    /// <summary>
    /// Содержимое сообщения
    /// </summary>
    public string Content { get; set; } = string.Empty;
}