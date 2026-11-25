using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YECashClient")]
    public class YECashClient
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Column("Name")]
        public string Name { get; set; } = null!;

        [Required]
        [Column("Location")]
        public Guid LocationGUID { get; set; }

        [Required]
        [Column("Ip")]
        public string Ip { get; set; } = null!;
        public int? Midserver { get; set; }
        public int? CashVersion { get; set; }
        public DateTime? LastConnection { get; set; }
        [ForeignKey(nameof(LocationGUID))]
        public Location Location { get; set; } = null!;
    }
}
