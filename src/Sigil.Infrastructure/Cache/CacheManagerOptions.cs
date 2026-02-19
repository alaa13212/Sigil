using Sigil.Application.Interfaces;

namespace Sigil.infrastructure.Cache;

internal class CacheManagerOptions
{
    public Dictionary<string, CategoryOptions> Categories { get; set; } = new();

    public void Add<T>(Action<CategoryOptions> configure) where T : ICacheService 
    {
        var categoryOptions = new CategoryOptions();
        configure(categoryOptions);
        Categories.Add(T.CategoryName, categoryOptions);
    }
    
    
    public void Add<T>(long sizeLimit, TimeSpan slidingExpiration) where T : ICacheService 
    {
        Add<T>(options =>
        {
            options.SizeLimit = sizeLimit;
            options.SlidingExpiration = slidingExpiration;
        });
    }
    
    public class CategoryOptions
    {
        public long? SizeLimit { get; set; }
        public TimeSpan? AbsoluteExpiration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan? SlidingExpiration { get; set; }
        public TimeSpan? ExpirationScanFrequency { get; set; }
        public bool CompactOnMemoryPressure { get; set; } = true;
    }

}