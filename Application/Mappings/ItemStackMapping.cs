using Aucy.Contracts.OnionClient;
using Aucy.Domain.Entities;
using AutoMapper;

namespace Aucy.Application.Mappings;

public class ItemStackMapping : Profile
{
    public ItemStackMapping()
    {
        CreateMap<ItemStack, ItemStackDto>()
            .ConstructUsing(src => new ItemStackDto(
                itemConfigId: src.ConfigId,
                itemConfig: null,
                quantity: src.Quantity
            )).ForMember(dest => dest.ItemConfig, opt =>
            {
                opt.Condition(src => src.Config != null && src.Config.IsUnary);
                opt.MapFrom(src => src.Config);
            });
    }
}
