using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YEServicesLog")]
    public class YEServicesLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ServiceName { get; set; } = null!;

        public DateTime Ending { get; set; }

        [Required]
        [StringLength(50)]
        public string Result { get; set; } = null!;
    }
}