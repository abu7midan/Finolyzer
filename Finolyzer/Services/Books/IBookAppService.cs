using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Finolyzer.Services.Dtos.Books;
using Finolyzer.Entities.Books;

namespace Finolyzer.Services.Books;

public interface IBookAppService :
    ICrudAppService< //Defines CRUD methods
        BookDto, //Used to show books
        Guid, //Primary key of the book entity
        PagedAndSortedResultRequestDto, //Used for paging/sorting
        CreateUpdateBookDto> //Used to create/update a book
{

}