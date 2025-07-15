using AutoMapper;
using Finolyzer.Entities.Books;
using Finolyzer.Services.Dtos.Books;

namespace Finolyzer.ObjectMapping;

public class FinolyzerAutoMapperProfile : Profile
{
    public FinolyzerAutoMapperProfile()
    {
        CreateMap<Book, BookDto>();
        CreateMap<CreateUpdateBookDto, Book>();
        CreateMap<BookDto, CreateUpdateBookDto>();
        /* Create your AutoMapper object mappings here */
    }
}
