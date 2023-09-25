namespace OFM.Infrastructure.WebAPI.Caching;

public interface IDistributedCacheFactory
{
    IDistributedCache<T> GetCache<T>();
}
