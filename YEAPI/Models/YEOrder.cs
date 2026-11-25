using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YEOrder")]
    public class YEOrder
    {
        public YEOrder()
        {
            Items = new HashSet<YEOrderItem>();
        }

        [Key]
        [StringLength(20)]
        public string EatsId { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string Platform { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string Discriminator { get; set; } = null!;

        [Column("Location")]
        public Guid LocationGUID { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = null!;
        public int? Persons { get; set; }
        public string? Comment { get; set; } = null!;

        public DateTime Created { get; set; }

        [ForeignKey(nameof(LocationGUID))]
        public virtual Location Location { get; set; } = null!;
        public virtual ICollection<YEOrderItem> Items { get; set; }
    }
}
