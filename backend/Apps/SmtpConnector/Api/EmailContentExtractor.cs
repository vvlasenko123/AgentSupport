using System.Text;

namespace SmtpConnector.Api;

internal static class EmailContentExtractor
{
    public static string ExtractPlainText(byte[] rawMessageBytes)
    {
        if (rawMessageBytes is null || rawMessageBytes.Length == 0)
        {
            return string.Empty;
        }

        var raw = Encoding.Latin1.GetString(rawMessageBytes);

        SplitHeadersBody(raw, out var headers, out var body);

        var text = ExtractFromEntity(headers, body);

        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    private static string ExtractFromEntity(string headers, string body)
    {
        var contentType = GetHeaderValue(headers, "Content-Type");

        if (IsMultipart(contentType))
        {
            if (TryGetBoundary(contentType, out var boundary))
            {
                return ExtractFromMultipart(body, boundary);
            }

            return string.Empty;
        }

        var transferEncoding = GetHeaderValue(headers, "Content-Transfer-Encoding");
        var charset = TryGetCharset(contentType);

        return DecodeText(body, transferEncoding, charset);
    }

    private static string ExtractFromMultipart(string body, string boundary)
    {
        var marker = "--" + boundary;

        var parts = body.Split(marker, StringSplitOptions.RemoveEmptyEntries);

        string? htmlFallback = null;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.Equals("--", StringComparison.Ordinal))
            {
                continue;
            }

            SplitHeadersBody(trimmed, out var partHeaders, out var partBody);

            var contentType = GetHeaderValue(partHeaders, "Content-Type");

            if (IsMultipart(contentType))
            {
                if (TryGetBoundary(contentType, out var nestedBoundary))
                {
                    var nested = ExtractFromMultipart(partBody, nestedBoundary);

                    if (string.IsNullOrWhiteSpace(nested) is false)
                    {
                        return nested;
                    }
                }

                continue;
            }

            var transferEncoding = GetHeaderValue(partHeaders, "Content-Transfer-Encoding");
            var charset = TryGetCharset(contentType);

            if (IsTextPlain(contentType))
            {
                var decoded = DecodeText(partBody, transferEncoding, charset);

                if (string.IsNullOrWhiteSpace(decoded) is false)
                {
                    return decoded;
                }

                continue;
            }

            if (IsTextHtml(contentType))
            {
                var decoded = DecodeText(partBody, transferEncoding, charset);

                if (string.IsNullOrWhiteSpace(decoded) is false && htmlFallback is null)
                {
                    htmlFallback = decoded;
                }
            }
        }

        return htmlFallback ?? string.Empty;
    }

    private static bool IsMultipart(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryGetBoundary(string? contentType, out string boundary)
    {
        boundary = string.Empty;

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var idx = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);

        if (idx < 0)
        {
            return false;
        }

        var value = contentType.Substring(idx + "boundary=".Length).Trim();

        if (value.StartsWith("\"", StringComparison.Ordinal))
        {
            var end = value.IndexOf('"', 1);

            if (end > 1)
            {
                boundary = value.Substring(1, end - 1);
                return string.IsNullOrWhiteSpace(boundary) is false;
            }
        }

        var semi = value.IndexOf(';', StringComparison.Ordinal);

        boundary = semi >= 0 ? value.Substring(0, semi).Trim() : value.Trim();

        return string.IsNullOrWhiteSpace(boundary) is false;
    }

    private static bool IsTextPlain(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.IndexOf("text/plain", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTextHtml(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SplitHeadersBody(string raw, out string headers, out string body)
    {
        var idx = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);

        if (idx >= 0)
        {
            headers = UnfoldHeaders(raw.Substring(0, idx));
            body = raw.Substring(idx + 4);
            return;
        }

        idx = raw.IndexOf("\n\n", StringComparison.Ordinal);

        if (idx >= 0)
        {
            headers = UnfoldHeaders(raw.Substring(0, idx));
            body = raw.Substring(idx + 2);
            return;
        }

        headers = string.Empty;
        body = raw;
    }

    private static string UnfoldHeaders(string headers)
    {
        var sb = new StringBuilder(headers.Length);

        for (var i = 0; i < headers.Length; i++)
        {
            var ch = headers[i];

            if (ch == '\r')
            {
                continue;
            }

            if (ch == '\n')
            {
                var next = i + 1 < headers.Length ? headers[i + 1] : '\0';

                if (next == ' ' || next == '\t')
                {
                    sb.Append(' ');
                    continue;
                }

                sb.Append('\n');
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string? GetHeaderValue(string headers, string name)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            return null;
        }

        var lines = headers.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();

            if (trimmed.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase) is false)
            {
                continue;
            }

            return trimmed.Substring(name.Length + 1).Trim();
        }

        return null;
    }

    private static string? TryGetCharset(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var idx = contentType.IndexOf("charset=", StringComparison.OrdinalIgnoreCase);

        if (idx < 0)
        {
            return null;
        }

        var value = contentType.Substring(idx + "charset=".Length).Trim();

        if (value.StartsWith("\"", StringComparison.Ordinal))
        {
            var end = value.IndexOf('"', 1);

            if (end > 1)
            {
                return value.Substring(1, end - 1);
            }
        }

        var semi = value.IndexOf(';', StringComparison.Ordinal);

        return semi >= 0 ? value.Substring(0, semi).Trim() : value.Trim();
    }

    private static string DecodeText(string text, string? transferEncoding, string? charset)
    {
        var encoding = GetEncoding(charset);

        if (string.IsNullOrWhiteSpace(transferEncoding))
        {
            return encoding.GetString(Encoding.Latin1.GetBytes(text));
        }

        if (transferEncoding.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            var base64 = RemoveWhitespace(text);

            try
            {
                var bytes = Convert.FromBase64String(base64);
                return encoding.GetString(bytes);
            }
            catch
            {
                return encoding.GetString(Encoding.Latin1.GetBytes(text));
            }
        }

        if (transferEncoding.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = DecodeQuotedPrintable(text);
            return encoding.GetString(bytes);
        }

        return encoding.GetString(Encoding.Latin1.GetBytes(text));
    }

    private static Encoding GetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static string RemoveWhitespace(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t')
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static byte[] DecodeQuotedPrintable(string input)
    {
        using var ms = new MemoryStream();

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (ch != '=')
            {
                ms.WriteByte((byte)ch);
                continue;
            }

            if (i + 1 < input.Length && input[i + 1] == '\n')
            {
                i += 1;
                continue;
            }

            if (i + 2 < input.Length && input[i + 1] == '\r' && input[i + 2] == '\n')
            {
                i += 2;
                continue;
            }

            if (i + 2 >= input.Length)
            {
                break;
            }

            var hi = FromHex(input[i + 1]);
            var lo = FromHex(input[i + 2]);

            if (hi < 0 || lo < 0)
            {
                ms.WriteByte((byte)'=');
                continue;
            }

            ms.WriteByte((byte)((hi << 4) | lo));
            i += 2;
        }

        return ms.ToArray();
    }

    private static int FromHex(char ch)
    {
        if (ch >= '0' && ch <= '9')
        {
            return ch - '0';
        }

        if (ch >= 'A' && ch <= 'F')
        {
            return ch - 'A' + 10;
        }

        if (ch >= 'a' && ch <= 'f')
        {
            return ch - 'a' + 10;
        }

        return -1;
    }
}