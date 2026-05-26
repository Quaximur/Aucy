using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public  class LotsWithItemConfigsDto
    {
        [Key(0)] public List<LotWithItemConfigDto> Lots { get; set; }

        public LotsWithItemConfigsDto(List<LotWithItemConfigDto> lots)
        {
            Lots = lots;
        }
    }
}
