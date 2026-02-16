using Microsoft.AspNetCore.Http;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class RemoteIpProviderEnricher(IHttpContextAccessor httpContextAccessor) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent)
    {
        if (parsedEvent.User is { IpAddress: "{{auto}}" })
        {
            string? userIpAddress = httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
            parsedEvent.User.IpAddress = userIpAddress.IsNullOrEmpty() ? null : userIpAddress;
        }
    }
}