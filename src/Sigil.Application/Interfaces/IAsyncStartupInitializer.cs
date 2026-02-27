namespace Sigil.Application.Interfaces;

public interface IAsyncStartupInitializer
{
    Task InitializeAsync();
}