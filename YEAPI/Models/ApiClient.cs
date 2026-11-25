using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YEApiClients")]
    public class ApiClient
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string ClientId { get; set; } = "";

        [Required]
        [StringLength(200)]
        public string ClientSecret { get; set; } = "";

        public bool Active { get; set; } = true;
    }
}