using MessagePack;
using System;
using System.Collections.Generic;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class ItemConfigDto
    {
        [Key(0)] public Guid Id { get; set; }
        [Key(1)] public string Name { get; set; }
        [Key(2)] public bool IsUnary { get; set; }
        [Key(3)] public string Icon { get; set; }
        [Key(4)] public int Grade { get; set; }
        [Key(5)] public int Rarity { get; set; }
        [Key(6)] public float Weight { get; set; }
        [Key(7)] public string BaseType { get; set; }
        [Key(8)] public List<string> Extra { get; set; }
        [Key(9)] public string CategoryPath { get; set; }
        [Key(10)] public long MinPrice { get; set; }
        [Key(11)] public long MaxPrice { get; set; }
        [Key(12)] public int MaxSellQuantity { get; set; }
        [Key(13)] public int MaxBuyQuantity { get; set; }

        public ItemConfigDto() { }

        public ItemConfigDto(Guid id,
            string name,
            bool isUnary,
            string icon,
            int grade,
            int rarity,
            float weight,
            string baseType,
            List<string> extra,
            string categoryPath,
            long minPrice,
            long maxPrice,
            int maxSellQuantity,
            int maxBuyQuantity)
        {
            Id = id;
            Name = name;
            IsUnary = isUnary;
            Icon = icon;
            Grade = grade;
            Rarity = rarity;
            Weight = weight;
            BaseType = baseType;
            Extra = extra;
            CategoryPath = categoryPath;
            MinPrice = minPrice;
            MaxPrice = maxPrice;
            MaxSellQuantity = maxSellQuantity;
            MaxBuyQuantity = maxBuyQuantity;
        }
    }
}
