using Finolyzer.Entities;
using Finolyzer.Entities.Books;
using System;
using System.ComponentModel.DataAnnotations;

namespace Finolyzer.Services.Dtos.CostSummaryRequests;

public class CreateUpdateCostSummaryRequestDto
{
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public int PortfolioId { get; set; }
    
    public int ApplicationSystemId { get; set; }
    [Required]
    public TimlyRequestType TimlyRequestType { get; set; } = TimlyRequestType.Monthly;

    [Required]
    [DataType(DataType.Date)]
    public DateTime BeforeDate { get; set; } = DateTime.Now;

    
}