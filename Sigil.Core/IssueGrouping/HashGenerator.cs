using System.Security.Cryptography;
using System.Text;

namespace Sigil.Core.IssueGrouping;

public class HashGenerator : IHashGenerator
{
    public string ComputeHash(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}