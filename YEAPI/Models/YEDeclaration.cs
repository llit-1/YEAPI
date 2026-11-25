using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace YEAPI.Models
{
    [Table("YEDeclarations")]
    public class YEDeclaration
    {
        public YEDeclaration()
        {
            Items = new List<YEItem>();
        }

        [Key]
        public int Id { get; set; }

        // Номер декларации (number)
        [Required]
        [StringLength(100)]
        public string DocNumber { get; set; } = null!;

        // idDeclaration во ФГИС — для ссылки на карточку
        public int? FsaId { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DateFrom { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DateTo { get; set; }

        [Required]
        [StringLength(500)]
        public string ManufacturerName { get; set; } = null!;

        [Required]
        [StringLength(1000)]
        public string LegalAddress { get; set; } = null!;

        [StringLength(1000)]
        public string? ProductionAddress { get; set; }

        public string? ProductName { get; set; }

        // Все коды ТН ВЭД декларации через ";"
        [StringLength(100)]
        public string? TnvedCodes { get; set; }

        [JsonIgnore]
        public virtual List<YEItem> Items { get; set; }
    }
}
