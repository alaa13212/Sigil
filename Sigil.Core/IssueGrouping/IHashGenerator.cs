namespace Sigil.Core.IssueGrouping;

public interface IHashGenerator
{
    string ComputeHash(string value);
}