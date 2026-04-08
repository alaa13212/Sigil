using Microsoft.AspNetCore.Http;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class RemoteIpProviderEnricher(IHttpContextAccessor httpContextAccessor) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
    {
        if (parsedEvent.User is { IpAddress: "{{auto}}" })
        {
            string? userIpAddress = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            // Stryker disable once Conditional : IPAddress.ToString() never returns "", so IsNullOrEmpty only triggers on null — the false-branch mutation produces identical runtime behavior
            parsedEvent.User.IpAddress = userIpAddress.IsNullOrEmpty() ? null : userIpAddress;
        }
    }
}