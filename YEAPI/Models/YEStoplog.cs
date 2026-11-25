using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YEStoplog")]
    public class YEStoplog
    {
        [Key]
        public int ID { get; set; }

        [Column("Location")]
        public Guid? LocationGUID { get; set; }

        public int Item { get; set; }

        public DateTime Begin { get; set; }

        public DateTime End { get; set; }

        public int PermissionLevel { get; set; }

        [Required]
        [StringLength(100)]
        public string User { get; set; } = null!;

        public DateTime Created { get; set; }

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = null!;
    }
}
