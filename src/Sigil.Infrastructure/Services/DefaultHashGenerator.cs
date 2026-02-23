using System.Security.Cryptography;
using System.Text;
using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Services;

internal class DefaultHashGenerator  : IHashGenerator
{
    public string ComputeHash(string value)
    {
        // Calculate the maximum bytes needed for UTF-8 encoding
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
        
        // Use stackalloc for input bytes if reasonable size, otherwise rent from pool
        Span<byte> inputBytes = maxByteCount <= 1024 
            ? stackalloc byte[maxByteCount] 
            : new byte[maxByteCount];
        
        // Get actual byte count and slice the span
        int actualByteCount = Encoding.UTF8.GetBytes(value, inputBytes);
        inputBytes = inputBytes[..actualByteCount];
        
        // Use stackalloc for hash output
        Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
        
        // Compute hash directly into the span
        SHA256.HashData(inputBytes, hashBytes);
        
        // Convert to hex string
        return Convert.ToHexString(hashBytes);
    }
}