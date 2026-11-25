using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
        [Table("YEStopPack")]
        public class YEStopPack
        {
        public YEStopPack()
        {
            Stops = new List<YEStop>();
        }


        [Key]
        public int Id { get; set; }

            [Column("Item")]
            public int? ItemId { get; set; }

            public string? ItemName { get; set; }  

            [Column("Location")]
            public Guid? LocationGUID { get; set; }
            public string? LocationName { get; set; }

            [Required]
            public DateTime Begin { get; set; }

            [Required]
            public DateTime End { get; set; }

            [Required]
            [StringLength(50)]
            public string User { get; set; } = null!;
        public int PermissionLevel { get; set; }
        public List<YEStop> Stops { get; set; }

        public List<Guid> GetDistinctLocations()
        {
            return Stops
                .Where(x => x.LocationGUID.HasValue)
                .Select(x => x.LocationGUID!.Value)
                .Distinct()
                .ToList();
        }
    }
}
