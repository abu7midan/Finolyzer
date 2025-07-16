using Finolyzer.Entities;
using Finolyzer.Entities.Books;
using System;
using System.ComponentModel.DataAnnotations;

namespace Finolyzer.Services.Dtos.CostSummaryRequests;
public enum CalculationForType
{
    Portfolio=0,
    System=1
}
public class CreateUpdateCostSummaryRequestDto
{
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    [Required]
    public int EntityId { get; set; }
    [Required]
    public TimlyRequestType TimlyRequestType { get; set; } 
    [Required]
    public CalculationForType CalculationFor { get; set; }
    [Required]
    [DataType(DataType.Date)]
    public DateTime CalculationBeforeDate { get; set; } = DateTime.Now;
    public bool IncludeSharedService { get; set; } = true;

}