using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ToolboxWeb.Web.Infrastructure.Files;

/// <summary>How a text file is stored, so it can be written back the same way.</summary>
/// <param name="Encoding">Web name of the encoding: "utf-8", "utf-16", "utf-16BE" or a Windows code page such as "windows-1258".</param>
/// <param name="Bom">Whether the file starts with a byte order mark.</param>
/// <param name="LineEnding">"CRLF", "LF" or "CR"; a file without a line break gets the machine's usual ending.</param>
public sealed record TextFormat(string Encoding, bool Bom, string LineEnding);

/// <summary>
/// Reads and writes text files without changing how they are stored: the encoding (BOM,
/// UTF-8, UTF-16, or the machine's ANSI code page for older files) and the line ending.
/// The editor works with "\n" only; <see cref="Encode"/> puts the file's own ending back.
/// </summary>
public static class TextFileCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static TextFileCodec()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>A file is binary when its first 8 KB hold a NUL byte (UTF-16 files are recognised by their BOM first).</summary>
    public static bool LooksBinary(ReadOnlySpan<byte> head)
    {
        if (StartsWithUtf16Bom(head))
        {
            return false;
        }

        var sample = head.Length > 8192 ? head[..8192] : head;
        return sample.IndexOf((byte)0) >= 0;
    }

    public static (string Text, TextFormat Format) Decode(byte[] bytes)
    {
        string text;
        string name;
        var bom = false;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = StrictUtf8.GetString(bytes, 3, bytes.Length - 3);
            name = "utf-8";
            bom = true;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            name = "utf-16";
            bom = true;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            name = "utf-16BE";
            bom = true;
        }
        else
        {
            try
            {
                text = StrictUtf8.GetString(bytes);
                name = "utf-8";
            }
            catch (DecoderFallbackException)
            {
                var legacy = LegacyEncoding();
                text = legacy.GetString(bytes);
                name = legacy.WebName;
            }
        }

        return (text, new TextFormat(name, bom, DetectLineEnding(text)));
    }

    /// <summary>Bytes for <paramref name="text"/> (any line endings) stored as <paramref name="format"/>.</summary>
    public static byte[] Encode(string text, TextFormat format)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var withEndings = format.LineEnding switch
        {
            "CRLF" => normalized.Replace("\n", "\r\n", StringComparison.Ordinal),
            "CR" => normalized.Replace('\n', '\r'),
            _ => normalized
        };

        var encoding = Resolve(format.Encoding);
        var body = encoding.GetBytes(withEndings);
        if (!format.Bom)
        {
            return body;
        }

        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0)
        {
            return body;
        }

        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    /// <summary>Whether <paramref name="text"/> survives a round trip through the encoding (a legacy code page cannot hold every character).</summary>
    public static bool CanEncode(string text, TextFormat format)
    {
        if (format.Encoding.StartsWith("utf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var encoding = (Encoding)Resolve(format.Encoding).Clone();
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        try
        {
            encoding.GetByteCount(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Short content hash, used to notice that a file changed since it was read.</summary>
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..24].ToLowerInvariant();

    public static string DetectLineEnding(string text)
    {
        int crlf = 0, lf = 0, cr = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (text[i] == '\n')
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0 && cr == 0)
        {
            return OperatingSystem.IsWindows() ? "CRLF" : "LF";
        }

        return crlf >= lf && crlf >= cr ? "CRLF" : lf >= cr ? "LF" : "CR";
    }

    private static Encoding Resolve(string name) => name switch
    {
        "utf-8" => new UTF8Encoding(false),
        "utf-16" => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
        "utf-16BE" => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
        _ => Encoding.GetEncoding(name)
    };

    private static Encoding LegacyEncoding()
    {
        var codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
        try
        {
            return Encoding.GetEncoding(codePage is 0 or 65001 ? 1252 : codePage);
        }
        catch (ArgumentException)
        {
            return Encoding.GetEncoding(1252);
        }
    }

    private static bool StartsWithUtf16Bom(ReadOnlySpan<byte> head) =>
        head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF));
}
