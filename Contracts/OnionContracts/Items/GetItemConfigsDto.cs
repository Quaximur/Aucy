using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class GetItemConfigsDto
    {
        [Key(0)] public string CategoryPathFilter { get; set; }
        [Key(1)] public string NameFilter { get; set; }
        [Key(2)] public int? Rarity { get; set; }
        [Key(3)] public int? MinGrade { get; set; }
        [Key(4)] public int? MaxGrade { get; set; }
        [Key(5)] public int Page { get; set; }
        [Key(6)] public int PageSize { get; set; }

        public GetItemConfigsDto(string categoryPathFilter, 
            string nameFilter, 
            int? rarity, 
            int? minGrade, 
            int? maxGrade, 
            int page, 
            int pageSize)
        {
            CategoryPathFilter = categoryPathFilter;
            NameFilter = nameFilter;
            Rarity = rarity;
            MinGrade = minGrade;
            MaxGrade = maxGrade;
            Page = page;
            PageSize = pageSize;
        }
    }
}
