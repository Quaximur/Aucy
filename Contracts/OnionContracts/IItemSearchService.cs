using MagicOnion;

namespace Aucy.Contracts.OnionContracts
{
    public interface IItemSearchService : IService<IItemSearchService>
    {
        public UnaryResult<ItemConfigWithRawListing> GetItemConfig(GetItemConfigDto request);

        /// <summary>
        /// Get item config with additional info about bids and lots. 
        /// For example, to show user ids.
        /// </summary>
        public UnaryResult<ItemConfigWithRichListing> GetRichItemConfig(GetItemConfigDto request);
        public UnaryResult<ItemConfigsDto> GetItemConfigs(GetItemConfigsDto request);
    }
}
