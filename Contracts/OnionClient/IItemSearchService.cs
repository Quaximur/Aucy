using MagicOnion;

namespace Aucy.Contracts.OnionClient
{
    public interface IItemSearchService : IService<IItemSearchService>
    {
        /// <summary>
        /// Returns paginated collection of ItemConfigs available for purchasing or selling
        /// </summary>
        public UnaryResult<ItemConfigsDto> GetItemConfigs(GetItemConfigsDto request);
    }
}
