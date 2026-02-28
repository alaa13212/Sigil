using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Alerts;
using Sigil.Domain;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class AlertChannelService(SigilDbContext dbContext, IDateTime dateTime) : IAlertChannelService
{
    public async Task<List<AlertChannelResponse>> GetAllChannelsAsync()
    {
        var channels = await dbContext.AlertChannels
            .OrderBy(c => c.Name)
            .ToListAsync();

        return channels.Select(ToResponse).ToList();
    }

    public async Task<AlertChannelResponse> CreateChannelAsync(CreateAlertChannelRequest request)
    {
        var channel = new AlertChannel
        {
            Name = request.Name,
            Type = request.Type,
            Config = request.Config,
            CreatedAt = dateTime.UtcNow
        };

        dbContext.AlertChannels.Add(channel);
        await dbContext.SaveChangesAsync();
        return ToResponse(channel);
    }

    public async Task<AlertChannelResponse?> UpdateChannelAsync(int channelId, UpdateAlertChannelRequest request)
    {
        var channel = await dbContext.AlertChannels.AsTracking().FirstOrDefaultAsync(c => c.Id == channelId);
        if (channel is null) return null;

        channel.Name = request.Name;
        channel.Type = request.Type;
        channel.Config = request.Config;

        await dbContext.SaveChangesAsync();
        return ToResponse(channel);
    }

    public async Task<bool> DeleteChannelAsync(int channelId)
    {
        var inUse = await dbContext.AlertRules.AnyAsync(r => r.AlertChannelId == channelId);
        if (inUse) return false;

        var deleted = await dbContext.AlertChannels.Where(c => c.Id == channelId).ExecuteDeleteAsync();
        return deleted > 0;
    }

    private static AlertChannelResponse ToResponse(AlertChannel c) => new(
        c.Id, c.Name, c.Type, c.Config, c.CreatedAt);
}
