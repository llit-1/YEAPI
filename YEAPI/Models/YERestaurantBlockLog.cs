using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YERestaurantBlockLogs")]
    public class YERestaurantBlockLog
    {
        // blockId из Яндекса
        [Key]
        [StringLength(20)]
        public string Id { get; set; } = null!;

        // vendorId из Яндекса
        [Column("Location")]
        public Guid LocationGUID { get; set; }

        // source из Яндекса
        [StringLength(50)]
        public string? Initiator { get; set; }

        public DateTime Begin { get; set; }

        public DateTime? End { get; set; }
    }
}
