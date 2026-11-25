using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.ServerSentEvents;

namespace YEAPI.Models
{
    [Table("YEGroup")]
    public class YEGroup
    {
        public YEGroup()
        {
            Items = new List<YEItem>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string YaName { get; set; } = null!;

        [Required]

        public List<YEItem> Items { get; set; }

        [Required]
        public DateTime ImgUpdated { get; set; }

        [Required]
        public int Actual { get; set; }
        public byte[] Image { get; set; } = null!;
    }
}
