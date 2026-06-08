using MessagePack;
using System.Collections.Generic;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class OwnBidsDto
    {
    	[Key(0)] public List<OwnBidDto> OwnBids { get; set; }

    	public OwnBidsDto(List<OwnBidDto> ownBids)
    	{
    		OwnBids = ownBids;
    	}
    }
}