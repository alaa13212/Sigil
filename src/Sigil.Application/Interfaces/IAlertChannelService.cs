using Sigil.Application.Models.Alerts;

namespace Sigil.Application.Interfaces;

public interface IAlertChannelService
{
    Task<List<AlertChannelResponse>> GetAllChannelsAsync();
    Task<AlertChannelResponse> CreateChannelAsync(CreateAlertChannelRequest request);
    Task<AlertChannelResponse?> UpdateChannelAsync(int channelId, UpdateAlertChannelRequest request);
    Task<bool> DeleteChannelAsync(int channelId);
}
