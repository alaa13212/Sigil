namespace Sigil.Application.Interfaces;

public interface ICompressionService
{
    bool IsCompressed(byte[] data);
    byte[] CompressString(string input);
    string DecompressToString(byte[] compressedData);
}