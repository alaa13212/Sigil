using Sigil.Domain.Entities;

namespace Sigil.Domain.Interfaces;

public interface IMessageNormalizer
{
    string NormalizeMessage(IReadOnlyCollection<TextNormalizationRule> rules, string message);
}