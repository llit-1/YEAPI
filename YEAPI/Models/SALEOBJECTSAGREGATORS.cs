using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("SALEOBJECTSAGREGATORS")]
    public class SALEOBJECTSAGREGATORS
    {
        [Key]
        public int ID { get; set; }

        public int? MIDSERVER { get; set; }

        public int CODE { get; set; }

        public decimal SumWithoutDiscount { get; set; }

        public decimal SumWithDiscount { get; set; }

        public int Quantity { get; set; }

        public DateTime Date { get; set; }

        public int OrderType { get; set; }

        [StringLength(20)]
        public string OrderNumber { get; set; } = null!;

        public int CURRENCY { get; set; }

        public int? RESTAURANT { get; set; }

        public int DELETED { get; set; }

        // Код причины отмены заказа (cancel_reason.code от Яндекса)
        [StringLength(50)]
        public string? CancelCode { get; set; }

        public int Hour { get; set; }

        public int Time { get; set; }
    }
}
