using System.IO.Compression;
using System.Text;
using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Services;

internal class GzipCompressionService  : ICompressionService
{
    public bool IsCompressed(byte[] data)
    {
        return data is [0x1F, 0x8B, ..];
    }

    public byte[] CompressString(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return output.ToArray();
    }

    public string DecompressToString(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}