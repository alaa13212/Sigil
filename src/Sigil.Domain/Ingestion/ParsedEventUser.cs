using Sigil.Domain.Extensions;

namespace Sigil.Domain.Ingestion;

public class ParsedEventUser
{
    public string? UniqueIdentifier { get; set; }
    public string? Id { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? IpAddress { get; set; }
    
    public Dictionary<string, string>? Data { get; set; } = new();
    
    
    public static ParsedEventUser Merge(ParsedEventUser user1, ParsedEventUser user2)
    {
        if(user1.Id.IsNullOrEmpty() && !user2.Id.IsNullOrEmpty())
            user1.Id = user2.Id;
        if(user1.Email.IsNullOrEmpty() && !user2.Email.IsNullOrEmpty())
            user1.Email = user2.Email;
        if(user1.Username.IsNullOrEmpty() && !user2.Username.IsNullOrEmpty())
            user1.Username = user2.Username;
        if(user1.IpAddress.IsNullOrEmpty() && !user2.IpAddress.IsNullOrEmpty())
            user1.IpAddress = user2.IpAddress;
        
        if(user2.Data != null)
        {
            if(user1.Data == null)
            {
                user1.Data = user2.Data;
            }
            else
            {
                foreach (KeyValuePair<string, string> keyValuePair in user2.Data)
                {
                    if (!user1.Data.ContainsKey(keyValuePair.Key))
                        user1.Data.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
        }
        
        return user1;
    }
}