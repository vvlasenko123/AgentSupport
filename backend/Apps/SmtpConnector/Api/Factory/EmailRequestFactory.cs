using Infrastructure.Broker.Kafka.Contracts;
using MimeKit;

namespace SmtpConnector.Api.Factory;

public static class EmailRequestFactory
{
    public static EmailReceivedRpcRequest Build(byte[] rawBytes)
    {
        MimeMessage mimeMessage;

        try
        {
            using var ms = new MemoryStream(rawBytes);
            mimeMessage = MimeMessage.Load(ms);
        }
        catch
        {
            throw new InvalidOperationException("Не удалось разобрать письмо");
        }

        var request = new EmailReceivedRpcRequest
        {
            MessageId = string.IsNullOrWhiteSpace(mimeMessage.MessageId)
                ? Guid.NewGuid().ToString("N")
                : mimeMessage.MessageId,
            From = mimeMessage.From?.ToString(),
            Subject = mimeMessage.Subject,
            RawMimeBase64 = Convert.ToBase64String(rawBytes),
        };

        foreach (var mailbox in mimeMessage.To.Mailboxes)
        {
            if (string.IsNullOrWhiteSpace(mailbox.Address) is false)
            {
                request.To.Add(mailbox.Address);
            }
        }

        return request;
    }
}