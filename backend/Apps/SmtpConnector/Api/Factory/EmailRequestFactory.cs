using Infrastructure.Broker.Kafka.Contracts;
using MimeKit;

namespace SmtpConnector.Api.Factory;

public static class EmailRequestFactory
{
    public static EmailReceivedRpcRequest Build(byte[] rawMime)
    {
        if (rawMime is null)
        {
            throw new ArgumentNullException(nameof(rawMime));
        }

        using var ms = new MemoryStream(rawMime);
        var mime = MimeMessage.Load(ms);

        var messageId = mime.MessageId?.Trim();

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("Не задан messageId");
        }

        var mailbox = mime.From.Mailboxes.FirstOrDefault();

        var fromName = mailbox?.Name?.Trim() ?? string.Empty;
        var fromEmail = mailbox?.Address?.Trim() ?? string.Empty;

        var subject = mime.Subject?.Trim() ?? string.Empty;

        var sentAtUtc = mime.Date != DateTimeOffset.MinValue
            ? mime.Date.UtcDateTime
            : DateTime.UtcNow;

        var content = mime.TextBody?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            content = mime.HtmlBody?.Trim() ?? string.Empty;
        }

        content = CleanContent(content);

        return new EmailReceivedRpcRequest
        {
            MessageId = messageId,
            FromName = fromName,
            FromEmail = fromEmail,
            Subject = subject,
            SentAtUtc = sentAtUtc,
            Content = content,
        };
    }

    private static string CleanContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        content = NormalizeNewLines(content).Trim();

        content = CutBySignatureDelimiter(content);
        content = CutByFooterMarkers(content);

        return content.Trim();
    }

    private static string NormalizeNewLines(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static string CutBySignatureDelimiter(string content)
    {
        var index = FindCutIndexNearEnd(content, "\n-- \n");
        if (index >= 0)
        {
            return content.Substring(0, index).Trim();
        }

        index = FindCutIndexNearEnd(content, "\n--\n");
        if (index >= 0)
        {
            return content.Substring(0, index).Trim();
        }

        return content;
    }

    private static string CutByFooterMarkers(string content)
    {
        var tailLimit = Math.Min(content.Length, 1200);
        var tailStart = content.Length - tailLimit;
        var tail = content.Substring(tailStart);

        var lines = NormalizeNewLines(tail).Split('\n');

        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsFooterLine(line))
            {
                var prefix = string.Join("\n", lines.Take(i)).TrimEnd();
                return (content.Substring(0, tailStart) + prefix).Trim();
            }
        }

        return content;
    }

    private static bool IsFooterLine(string line)
    {
        if (line.StartsWith("отправлено", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (line.StartsWith("sent from", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (line.StartsWith("get outlook", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (line.Contains("мобильной почты", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (line.Contains("почты mail", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (line.Contains("gmail mobile", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static int FindCutIndexNearEnd(string content, string marker)
    {
        var index = content.LastIndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
        {
            return -1;
        }

        var tailLength = content.Length - index;

        if (tailLength > 1200)
        {
            return -1;
        }

        return index;
    }
}