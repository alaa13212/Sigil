namespace Sigil.Domain.Interfaces;

public interface IMessageNormalizer
{
    string NormalizeMessage(string message);
}