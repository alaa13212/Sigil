using System.Text;

namespace Sigil.Domain.Extensions;

public static class StreamExtensions
{
    public static async Task<string> ReadAsStringAsync(this Stream stream, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024);
        return await reader.ReadToEndAsync();
    }
}