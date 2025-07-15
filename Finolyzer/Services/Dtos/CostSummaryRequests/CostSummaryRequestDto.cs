using System;
using Volo.Abp.Application.Dtos;
using Finolyzer.Entities.Books;

namespace Finolyzer.Services.Dtos.CostSummaryRequests;

public class CostSummaryRequestDto : AuditedEntityDto<int>
{
    public string Name { get; set; }

    public BookType Type { get; set; }

    public DateTime PublishDate { get; set; }

    public float Price { get; set; }
}