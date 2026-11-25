using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YEHttpsLog")]
    public class YEHttpsLog
    {
        [Key]        
        public int ID { get; set; }

        [Required]
        [StringLength(10)]
        public string Type { get; set; } = null!;

        public string? ClientId { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [Required]
        [StringLength(200)]
        public string Url { get; set; } = null!;

        [Required]
        public string Body { get; set; } = null!;

        public DateTime DatetimeBegin { get; set; }

        public DateTime DatetimeEnd { get; set; }

        public int StatusCode { get; set; }

        public string? ErrorText { get; set; }

        [StringLength(20)]
        public string? Order { get; set; }
    }
}
