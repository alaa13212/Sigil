namespace Sigil.Application.Interfaces;

public interface IHashGenerator
{
    string ComputeHash(string value);
}