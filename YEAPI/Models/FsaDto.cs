using System.Text.Json.Serialization;

namespace YEAPI.Models
{
    // ==============================================================
    // ФГИС Росаккредитации: поиск деклараций
    // POST /api/v1/rds/common/declarations/get
    // ==============================================================

    public class FsaDeclarationSearchResponse
    {
        [JsonPropertyName("items")]
        public List<FsaDeclarationSearchItem> Items { get; set; } = new();
    }

    public class FsaDeclarationSearchItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("number")]
        public string? Number { get; set; }
    }

    // ==============================================================
    // Карточка декларации
    // GET /api/v1/rds/common/declarations/{id}
    // ==============================================================

    public class FsaDeclaration
    {
        [JsonPropertyName("idDeclaration")]
        public int IdDeclaration { get; set; }

        [JsonPropertyName("number")]
        public string? Number { get; set; }

        [JsonPropertyName("declRegDate")]
        public DateTime? DeclRegDate { get; set; }

        [JsonPropertyName("declEndDate")]
        public DateTime? DeclEndDate { get; set; }

        [JsonPropertyName("idStatus")]
        public int? IdStatus { get; set; }

        [JsonPropertyName("manufacturer")]
        public FsaLegalSubject? Manufacturer { get; set; }

        [JsonPropertyName("manufacturerFilials")]
        public List<FsaFilial> ManufacturerFilials { get; set; } = new();

        [JsonPropertyName("product")]
        public FsaProduct? Product { get; set; }
    }

    public class FsaLegalSubject
    {
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("addresses")]
        public List<FsaAddress> Addresses { get; set; } = new();
    }

    public class FsaFilial
    {
        [JsonPropertyName("addresses")]
        public List<FsaAddress> Addresses { get; set; } = new();
    }

    public class FsaAddress
    {
        // 1 — место нахождения (юридический адрес),
        // 3 — место осуществления деятельности (производство)
        [JsonPropertyName("idAddrType")]
        public int? IdAddrType { get; set; }

        [JsonPropertyName("fullAddress")]
        public string? FullAddress { get; set; }
    }

    public class FsaProduct
    {
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("identifications")]
        public List<FsaProductIdentification> Identifications { get; set; } = new();
    }

    public class FsaProductIdentification
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // id записей справочника ТН ВЭД (не сами коды)
        [JsonPropertyName("idTnveds")]
        public List<int> IdTnveds { get; set; } = new();
    }

    // ==============================================================
    // Справочник ТН ВЭД
    // POST /nsi/api/multi
    // ==============================================================

    public class FsaNsiMultiResponse
    {
        [JsonPropertyName("tnved")]
        public List<FsaTnved> Tnved { get; set; } = new();
    }

    public class FsaTnved
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    // ==============================================================
    // Ответ нашего API: POST /ItemManager/declarations/lookup
    // ==============================================================

    public class DeclarationLookupRequest
    {
        public string DocNumber { get; set; } = null!;
    }

    public class DeclarationLookupResponse
    {
        public string DocNumber { get; set; } = null!;

        public int FsaId { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public string? ManufacturerName { get; set; }

        public string? LegalAddress { get; set; }

        public string? ProductionAddress { get; set; }

        public string? ProductName { get; set; }

        public List<DeclarationTnvedDto> TnvedCodes { get; set; } = new();
    }

    public class DeclarationTnvedDto
    {
        public string Code { get; set; } = null!;

        public string? Name { get; set; }
    }
}
