using AutoMapper;
using Aucy.Contracts.OnionClient;
using Aucy.Domain.Entities;

namespace Aucy.Application.Mappings
{
    public class ItemConfigMapping : Profile
    {
        public ItemConfigMapping()
        {
            CreateMap<ItemConfigDto, ItemConfig>()
                .ConstructUsing(src => ItemConfig.Create(
                    id: src.Id,
                    name: src.Name,
                    isUnary: src.IsUnary,
                    icon: src.Icon,
                    grade: src.Grade,
                    rarity: src.Rarity,
                    weight: src.Weight,
                    baseType: src.BaseType,
                    extra: src.Extra,
                    categoryPath: src.CategoryPath
                ));

            CreateMap<ItemConfig, ItemConfigDto>()
                .ConstructUsing(src => new ItemConfigDto(
                    id: src.Id,
                    name: src.Name,
                    isUnary: src.IsUnary,
                    icon: src.Icon,
                    grade: src.Grade,
                    rarity: src.Rarity,
                    weight: src.Weight,
                    baseType: src.BaseType,
                    extra: src.Extra,
                    categoryPath: src.CategoryPath
                ));
        }
    }
}