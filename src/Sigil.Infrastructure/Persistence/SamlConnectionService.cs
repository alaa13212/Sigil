using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Saml;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Interfaces;

namespace Sigil.Infrastructure.Persistence;

internal class SamlConnectionService(
    SigilDbContext dbContext,
    IDateTime dateTime,
    IAppConfigService configService,
    IHttpClientFactory httpClientFactory)
    : ISamlConnectionService, ISamlRuntimeConfigurationService
{
    public Task<SamlEndpointsResponse> GetEndpointsAsync()
    {
        var hostUrl = configService.HostUrl?.TrimEnd('/');
        var origin = string.IsNullOrWhiteSpace(hostUrl) ? null : hostUrl;

        return Task.FromResult(new SamlEndpointsResponse(
            origin is null ? SamlDefaults.DefaultEntityId : origin + SamlDefaults.ModulePath,
            origin is null ? SamlDefaults.ModulePath : origin + SamlDefaults.ModulePath,
            origin is null ? SamlDefaults.ModulePath + "/Acs" : origin + SamlDefaults.ModulePath + "/Acs"));
    }
    public async Task<List<SamlConnectionResponse>> GetConnectionsAsync()
    {
        var connections = await dbContext.SamlConnections
            .Include(c => c.GroupMappings)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return connections.Select(ToResponse).ToList();
    }

    public async Task<List<SamlLoginOptionResponse>> GetEnabledConnectionsAsync()
    {
        return await dbContext.SamlConnections
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.Name)
            .Select(c => new SamlLoginOptionResponse(c.Id, c.Name, c.DisplayName))
            .ToListAsync();
    }

    public async Task<List<SamlConnectionRuntimeConfig>> GetRuntimeConfigurationsAsync()
    {
        return await dbContext.SamlConnections
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.Name)
            .Select(c => new SamlConnectionRuntimeConfig(c.Id, c.EntityId, c.MetadataXml))
            .ToListAsync();
    }

    public async Task<SamlConnectionResponse?> GetConnectionAsync(Guid id)
    {
        var connection = await dbContext.SamlConnections
            .Include(c => c.GroupMappings)
            .FirstOrDefaultAsync(c => c.Id == id);

        return connection is null ? null : ToResponse(connection);
    }

    public async Task<SamlConnectionResponse> CreateConnectionAsync(SamlConnectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var connection = new SamlConnection
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            DisplayName = request.DisplayName?.Trim(),
            EntityId = request.EntityId.Trim(),
            MetadataUrl = request.MetadataUrl?.Trim(),
            IsEnabled = request.IsEnabled,
            EmailClaimType = Normalize(request.EmailClaimType, "email"),
            NameClaimType = Normalize(request.NameClaimType, "name"),
            GroupsClaimType = Normalize(request.GroupsClaimType, "groups"),
            DefaultTeamName = request.DefaultTeamName?.Trim(),
            CreatedAt = dateTime.UtcNow,
            UpdatedAt = dateTime.UtcNow
        };

        AddGroupMappings(connection, request.GroupMappings);

        dbContext.SamlConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        return ToResponse(connection);
    }

    public async Task<SamlConnectionResponse?> UpdateConnectionAsync(Guid id, SamlConnectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var connection = await dbContext.SamlConnections
            .AsTracking()
            .Include(c => c.GroupMappings)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (connection is null) return null;

        connection.Name = request.Name.Trim();
        connection.DisplayName = request.DisplayName?.Trim();
        connection.EntityId = request.EntityId.Trim();
        connection.MetadataUrl = request.MetadataUrl?.Trim();
        connection.IsEnabled = request.IsEnabled;
        connection.EmailClaimType = Normalize(request.EmailClaimType, "email");
        connection.NameClaimType = Normalize(request.NameClaimType, "name");
        connection.GroupsClaimType = Normalize(request.GroupsClaimType, "groups");
        connection.DefaultTeamName = request.DefaultTeamName?.Trim();
        connection.UpdatedAt = dateTime.UtcNow;

        connection.GroupMappings.Clear();
        AddGroupMappings(connection, request.GroupMappings);

        await dbContext.SaveChangesAsync();

        return ToResponse(connection);
    }

    public async Task<bool> DeleteConnectionAsync(Guid id)
    {
        var connection = await dbContext.SamlConnections
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (connection is null) return false;

        dbContext.SamlConnections.Remove(connection);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetMetadataAsync(Guid id, string? metadataXml, DateTime? fetchedAt, string? error)
    {
        var connection = await dbContext.SamlConnections
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (connection is null) return false;

        if (metadataXml is not null)
        {
            connection.MetadataXml = metadataXml;
            connection.MetadataFetchedAt = fetchedAt;
            connection.MetadataError = null;
        }
        else if (error is not null)
        {
            connection.MetadataError = error;
        }

        connection.UpdatedAt = dateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RefreshMetadataAsync(Guid id)
    {
        var connection = await GetConnectionAsync(id);
        if (connection is null) return false;
        if (string.IsNullOrWhiteSpace(connection.MetadataUrl)) return false;

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var xml = await client.GetStringAsync(connection.MetadataUrl);

            await SetMetadataAsync(id, xml, dateTime.UtcNow, null);
            return true;
        }
        catch (Exception ex)
        {
            await SetMetadataAsync(id, null, null, ex.Message);
            return false;
        }
    }

    public async Task<List<SamlPendingUserResponse>> GetPendingUsersAsync()
    {
        return await dbContext.Users
            .Where(u => u.SamlStatus == SamlUserStatus.Pending)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new SamlPendingUserResponse(
                u.Id,
                u.Email ?? "",
                u.DisplayName,
                u.SamlConnection != null ? u.SamlConnection.Name : null,
                u.CreatedAt))
            .ToListAsync();
    }

    public async Task<bool> ApproveUserAsync(Guid userId)
    {
        return await SetUserStatusAsync(userId, SamlUserStatus.Approved);
    }

    public async Task<bool> RejectUserAsync(Guid userId)
    {
        return await SetUserStatusAsync(userId, SamlUserStatus.Rejected);
    }

    private async Task<bool> SetUserStatusAsync(Guid userId, SamlUserStatus status)
    {
        var user = await dbContext.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return false;

        user.SamlStatus = status;
        await dbContext.SaveChangesAsync();
        return true;
    }

    private static void AddGroupMappings(SamlConnection connection, List<SamlGroupMappingRequest>? mappings)
    {
        if (mappings is null) return;

        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.GroupName)) continue;

            connection.GroupMappings.Add(new SamlGroupMapping
            {
                GroupName = mapping.GroupName.Trim(),
                TeamName = string.IsNullOrWhiteSpace(mapping.TeamName) ? null : mapping.TeamName.Trim(),
                Role = mapping.Role,
                GrantsGlobalAdmin = mapping.GrantsGlobalAdmin
            });
        }
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static SamlConnectionResponse ToResponse(SamlConnection connection) =>
        new(
            connection.Id,
            connection.Name,
            connection.EntityId,
            connection.DisplayName,
            connection.MetadataUrl,
            !string.IsNullOrWhiteSpace(connection.MetadataXml),
            connection.MetadataFetchedAt,
            connection.MetadataError,
            connection.IsEnabled,
            connection.EmailClaimType,
            connection.NameClaimType,
            connection.GroupsClaimType,
            connection.DefaultTeamName,
            connection.GroupMappings
                .OrderBy(m => m.GroupName)
                .Select(m => new SamlGroupMappingResponse(m.Id, m.GroupName, m.TeamName, m.Role, m.GrantsGlobalAdmin))
                .ToList(),
            connection.CreatedAt,
            connection.UpdatedAt);
}
