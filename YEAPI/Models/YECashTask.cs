using System.ComponentModel.DataAnnotations;

namespace YEAPI.Models;

public class YECashTask
{
    public int Id { get; set; }

    public int CashId { get; set; }

    [MaxLength(20)]
    public string Type { get; set; } = null!;

    [MaxLength(20)]
    public string Stage { get; set; } = null!;

    public int? CashVersionId { get; set; }

    public DateTime StageChanged { get; set; }
}