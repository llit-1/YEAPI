using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace YEAPI.Models
{
    [Table("YEStop")]
    public class YEStop
    {
        [Key]
        public int ID { get; set; }

        [Column("Location")]
        public Guid? LocationGUID { get; set; }

        public int Item { get; set; }
        [Column("Pack")]
        public int PackId { get; set; }
        
        public DateTime Begin { get; set; }

        public DateTime End { get; set; }
      

        [ForeignKey(nameof(LocationGUID))]
        public virtual Location? Location { get; set; }

        [ForeignKey(nameof(Item))]
        public virtual YEItem? YEItem { get; set; }
        public int? RkItemStatus { get; set; }
        [ForeignKey(nameof(PackId))]
        [JsonIgnore]
        public virtual YEStopPack? YEStopPack { get; set; }


    }
}
