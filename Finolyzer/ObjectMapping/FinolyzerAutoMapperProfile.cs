using AutoMapper;
using Finolyzer.Entities.Books;
using Finolyzer.Services.Dtos.Books;

namespace Finolyzer.ObjectMapping;

public class FinolyzerAutoMapperProfile : Profile
{
    public FinolyzerAutoMapperProfile()
    {
        CreateMap<Book, CostSummaryRequestDto>();
        CreateMap<CreateUpdateCostSummaryRequestDto, Book>();
        CreateMap<CostSummaryRequestDto, CreateUpdateCostSummaryRequestDto>();
        /* Create your AutoMapper object mappings here */
    }
}
