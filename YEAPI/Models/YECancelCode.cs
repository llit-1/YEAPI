using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    // Код отмены Яндекса → значение DELETED в SALEOBJECTSAGREGATORS.
    // Справочник ведётся вручную в БД, из кода только читается.
    [Table("YECancelCodes")]
    public class YECancelCode
    {
        [Key]
        [StringLength(100)]
        public string Code { get; set; } = null!;

        public int Deleted { get; set; }
    }
}
