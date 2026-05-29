using MessagePack;
using System;
using System.Collections.Generic;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class ItemConfigDto
    {
        [Key(0)] public Guid Id { get; set; }
        [Key(1)] public string Name { get; set; }
        [Key(2)] public bool IsUnary { get; set; }
        [Key(3)] public string? Icon { get; set; }
        [Key(4)] public int? Grade { get; set; }
        [Key(5)] public int? Rarity { get; set; }
        [Key(6)] public string BaseType { get; set; }
        [Key(7)] public List<string> Extra { get; set; }
        [Key(8)] public string CategoryPath { get; set; }

        public ItemConfigDto(Guid id, 
            string name, 
            bool isUnary, 
            string? icon, 
            int? grade, 
            int? rarity,
            string baseType, 
            List<string> extra, 
            string categoryPath)
        {
            Id = id;
            Name = name;
            IsUnary = isUnary;
            Icon = icon;
            Grade = grade;
            Rarity = rarity;
            BaseType = baseType;
            Extra = extra;
            CategoryPath = categoryPath;
        }
    }
}
