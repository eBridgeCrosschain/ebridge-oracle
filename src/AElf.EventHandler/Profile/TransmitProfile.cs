using AElf.EventHandler.Dto;
using AutoMapper;

namespace AElf.EventHandler;

public class TransmitProfile : Profile
{
    public TransmitProfile()
    {
        CreateMap<TransmitArgs, TransmitCheckArgs>().ReverseMap();
    }
}