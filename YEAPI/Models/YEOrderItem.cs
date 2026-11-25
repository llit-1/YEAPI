using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YEOrderItem")]
    public class YEOrderItem
    {
        public string Order { get; set; } = null!;

        public int Item { get; set; }

        public int Quantity { get; set; }

        [ForeignKey(nameof(Order))]
        public virtual YEOrder YEOrder { get; set; } = null!;

        [ForeignKey(nameof(Item))]
        public virtual YEItem YEItem { get; set; } = null!;
    }
}
