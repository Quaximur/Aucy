using MagicOnion;
using System;
using System.Threading.Tasks;

namespace Aucy.Contracts.OnionClient
{
    public interface IItemListingService : IService<IItemListingService>
    {
        //public ItemConfigWithRawListingDto GetItemConfigWithRawListing(Guid itemConfigId);
        public Task<ServerStreamingResult<RawItemListingDto>> GetItemListingStream(Guid itemConfigId);

        /// <summary>
        /// Get item config with additional info about bids and lots. 
        /// For example, to show user ids.
        /// </summary>
        //public ItemConfigWithRichListingDto GetItemConfigWithRichListing(Guid itemConfigId);
    }
}
