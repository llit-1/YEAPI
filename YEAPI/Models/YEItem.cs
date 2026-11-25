using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace YEAPI.Models
{
    [Table("YEItem")]
    public class YEItem
    {
        public YEItem()
        {
            Stops = new List<YEStop>();
        }

        [Key]
        public int Rkcode { get; set; }

        public int YEGroup { get; set; }

        [Required]
        [StringLength(100)]
        public string RkName { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string YEName { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        public decimal Price { get; set; }

        public int Measure { get; set; }

        [Required]
        [StringLength(10)]
        public string MeasureUnit { get; set; } = null!;

        [JsonIgnore]
        public string? ImageHash { get; set; } = null!;

        [Required]
        public int Actual { get; set; }

        [Required]
        public byte[] Image { get; set; } = null!;

        // Декларация о соответствии (YEDeclarations.Id)
        public int? Declaration { get; set; }

        // Код ТН ВЭД блюда — выбирается из TnvedCodes декларации
        [StringLength(10)]
        public string? TnvedCode { get; set; }

        [ForeignKey(nameof(YEGroup))]
        public virtual YEGroup? Group { get; set; } = null!;

        [ForeignKey(nameof(Declaration))]
        public virtual YEDeclaration? YEDeclaration { get; set; }
        public virtual List<YEStop>? Stops { get; set; }
    }

}
