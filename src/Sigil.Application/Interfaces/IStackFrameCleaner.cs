using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

public interface IStackFrameCleaner
{
    Platform Platform { get; }
    string CleanMethodName(string methodName);
}
