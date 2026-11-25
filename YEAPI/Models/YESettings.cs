using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YEAPI.Models
{
    [Table("YESettings")]
    public class YESettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime MenuLastChange { get; set; }

        [Required]
        [StringLength(500)]
        public string YandexHost { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string YandexClientId { get; set; } = null!;

        [Required]
        [StringLength(500)]
        public string YandexClientSecret { get; set; } = null!;

        [StringLength(2000)]
        public string? YandexAccessToken { get; set; }
        public DateTime? YandexAccessTokenExpires { get; set; }
        public string? RK7Server { get; set; }
        public int? RK7ServerPort { get; set; }
        public int? RK7CashPort { get; set; }
        public string? RK7Login { get; set; }
        public string? RK7Pass { get; set; }
        public string? CashPath { get; set; }

        // ФГИС Росаккредитации (pub.fsa.gov.ru)
        [StringLength(500)]
        public string? FsaHost { get; set; }

        [StringLength(50)]
        public string? FsaLogin { get; set; }

        [StringLength(50)]
        public string? FsaPassword { get; set; }

        [StringLength(2000)]
        public string? FsaAccessToken { get; set; }
        public DateTime? FsaAccessTokenExpires { get; set; }
    }
}