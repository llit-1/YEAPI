using System.ComponentModel.DataAnnotations;

namespace YEAPI.Models;

public class YECashVersion
{
    public int Id { get; set; }

    public byte[] File { get; set; } = null!;

    [MaxLength(50)]
    public string Version { get; set; } = null!;

    public int Actual { get; set; }
}