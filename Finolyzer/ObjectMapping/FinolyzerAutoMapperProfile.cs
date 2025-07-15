using AutoMapper;
using Finolyzer.Entities;
using Finolyzer.Entities.Books;
using Finolyzer.Services.Dtos.Books;
using Finolyzer.Services.Dtos.CostSummaryRequests;

namespace Finolyzer.ObjectMapping;

public class FinolyzerAutoMapperProfile : Profile
{
    public FinolyzerAutoMapperProfile()
    {
        CreateMap<Book, BookDto>();
        CreateMap<CreateUpdateBookDto, Book>();
        CreateMap<BookDto, CreateUpdateBookDto>();


        CreateMap<CostSummaryRequest, CostSummaryRequestDto>();
        CreateMap<CostSummaryRequestDto, CostSummaryRequest>();
        CreateMap<CostSummaryRequestDto, CreateUpdateCostSummaryRequestDto>();
        /* Create your AutoMapper object mappings here */
    }
}
