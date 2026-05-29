using MessagePack;
using System.Collections.Generic;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public  class OwnLotsDto
    {
        [Key(0)] public List<OwnLotDto> Lots { get; set; }

        public OwnLotsDto(List<OwnLotDto> lots)
        {
            Lots = lots;
        }
    }
}
