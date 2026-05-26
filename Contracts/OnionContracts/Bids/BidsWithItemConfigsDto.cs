using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class BidsWithItemConfigsDto
    {
    	[Key(0)] public List<BidWithItemConfigDto> BidsWithItemConfigs { get; set; }

    	public BidsWithItemConfigsDto(List<BidWithItemConfigDto> bidsWithLots)
    	{
    		BidsWithItemConfigs = bidsWithLots;
    	}
    }
}