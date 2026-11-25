using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace YEAPI.Models
{
    [Table("Locations")]
    public class Location
    {
        public Location()
        {
            Stops = new HashSet<YEStop>();
        }

        [Key]
        public Guid GUID { get; set; }

        [Required]
        [StringLength(50)]
        public string NAME { get; set; } = null!;

        public Guid? LOCATIONTYPEGUID { get; set; }

        public int? RKCODE { get; set; }

        public double? LATITUDE { get; set; }

        public double? LONGITUDE { get; set; }

        public int? ACTUAL { get; set; }
        public int Delivery { get; set; }
        public virtual ICollection<YEStop> Stops { get; set; }
    }
}
