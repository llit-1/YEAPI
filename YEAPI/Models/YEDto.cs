using System.Text.Json.Serialization;

namespace YEAPI.Models
{
    public class MenuCompositionDto
    {
        public List<MenuCategoryDto> categories { get; set; } = new();

        public List<MenuItemDto> items { get; set; } = new();

        public DateTime lastChange { get; set; }
    }

    public class MenuCategoryDto
    {
        public string id { get; set; } = null!;

        public string name { get; set; } = null!;

        public string? parentId { get; set; }

        public int sortOrder { get; set; } = 100;
        public List<MenuCategoryImageDto>? images { get; set; }
    }

    public class MenuItemDto
    {
        public string categoryId { get; set; } = null!;

        public string id { get; set; } = null!;

        public int measure { get; set; }

        public string measureUnit { get; set; } = null!;

        public string name { get; set; } = null!;

        public decimal price { get; set; }

        public string? description { get; set; }

        public bool isCatchweight { get; set; } = false;

        public int sortOrder { get; set; } = 100;
        public List<MenuItemImageDto>? images { get; set; }

        // Товар не требует маркировки (Честный знак) — gtin не передаём
        public bool needMarking { get; set; } = false;

        // Юридическая информация (декларация о соответствии).
        // Нет декларации — поле не передаём.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MenuItemLegalInfoDto? legalInfo { get; set; }
    }

    // ==============================================================
    // legalInfo позиции меню
    // ==============================================================

    public class MenuItemLegalInfoDto
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MenuClassificationCodesDto? classificationCodes { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MenuConformityInfoDto? conformityInfo { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MenuGovernmentRegistrationInfoDto? governmentRegistrationInfo { get; set; }
    }

    public class MenuClassificationCodesDto
    {
        public string okpd2Code { get; set; } = "56.10.11.120";
    }

    public class MenuConformityInfoDto
    {
        // not_required / certification_required / declaration_required /
        // certification_and_declaration_required
        public string status { get; set; } = null!;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MenuConformityDocumentsDto? documents { get; set; }
    }

    public class MenuConformityDocumentsDto
    {
        public List<MenuDeclarationDto> declarations { get; set; } = new();
    }

    public class MenuDeclarationDto
    {
        public string declarationNumber { get; set; } = null!;

        // Ссылка на карточку декларации во ФГИС
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? declaration { get; set; }

        // YYYY-MM-DD
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? declarationDateFrom { get; set; }

        // YYYY-MM-DD
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? declarationDateTo { get; set; }

        public List<MenuManufacturerInfoDto> manufacturerInfo { get; set; } = new();
    }

    public class MenuManufacturerInfoDto
    {
        public string manufacturerName { get; set; } = null!;

        public string legalAddress { get; set; } = null!;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? productionAddress { get; set; }
    }

    public class MenuGovernmentRegistrationInfoDto
    {
        // not_required / required
        public string status { get; set; } = null!;
    }


    public class MenuCategoryImageDto
    {
        public string url { get; set; } = null!;
        public DateTime updatedAt { get; set; }
    }

    public class MenuItemImageDto
    {
        public string hash { get; set; } = null!;
        public string url { get; set; } = null!;
    }

    public class MenuAvailabilityDto
    {
        public List<MenuAvailabilityItemDto> items { get; set; } = new();

        public List<object> modifiers { get; set; } = new();

        public List<object> combos { get; set; } = new();
    }

    public class MenuAvailabilityItemDto
    {
        public string itemId { get; set; } = null!;

        public int stock { get; set; }
    }

    public class OrderRequest
    {
        public string Platform { get; set; } = null!;

        public string Discriminator { get; set; } = null!;

        public string EatsId { get; set; } = null!;

        public string RestaurantId { get; set; } = null!;

        public OrderDeliveryInfo? DeliveryInfo { get; set; }

        public OrderPaymentInfo? PaymentInfo { get; set; }

        public List<OrderRequestItem> Items { get; set; } = new();

        public int? Persons { get; set; }

        public string? Comment { get; set; }

        public List<OrderPromoItem>? Promos { get; set; }
    }

    public class OrderDeliveryInfo
    {
        public string? ClientName { get; set; }

        public string? PhoneNumber { get; set; }

        public List<string?>? AdditionalPhoneNumbers { get; set; }

        public string? DeliveryDate { get; set; }

        public string? ClientArrivementDate { get; set; }

        public string? CourierArrivementDate { get; set; }

        public string? RealPhoneNumber { get; set; }

        public string? PickupCode { get; set; }

        public List<string>? Batches { get; set; }

        public DeliveryAddress? DeliveryAddress { get; set; }
    }

    public class DeliveryAddress
    {
        public string? Full { get; set; }

        public string? Latitude { get; set; }

        public string? Longitude { get; set; }
    }

    public class OrderPaymentInfo
    {
        public string? PaymentType { get; set; }

        public decimal? ItemsCost { get; set; }

        public decimal? DeliveryFee { get; set; }

        public decimal? Total { get; set; }

        public decimal? Change { get; set; }
    }

    public class OrderRequestItem
    {
        public string Id { get; set; } = null!;

        public string? Name { get; set; }

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }

        public List<OrderModificationItem>? Modifications { get; set; }

        public List<OrderPromoItem>? Promos { get; set; }

        public OrderComboInfo? ComboInfo { get; set; }
    }


    public class OrderModificationItem
    {
        public string? Id { get; set; }

        public string? GroupId { get; set; }

        public string? Name { get; set; }

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }
    }

    public class OrderPromoItem
    {
        public string? Type { get; set; }

        public decimal Discount { get; set; }

        public decimal PartnerDiscount { get; set; }

        public decimal YandexDiscount { get; set; }
    }

    public class OrderComboInfo
    {
        public string? Id { get; set; }

        public string? ComponentId { get; set; }
    }

    public class OrderStatusRequest
    {
        public string Status { get; set; } = null!;

        public List<string>? Attributes { get; set; }

        public string? Comment { get; set; }

        public string? Reason { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class YandexBlockRequest
    {
        [JsonPropertyName("vendorInfo")]
        public List<YandexVendorInfo> VendorInfo { get; set; } = new();

        [JsonPropertyName("from")]
        public string From { get; set; } = null!;

        [JsonPropertyName("to")]
        public string? To { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("tag")]
        public string Tag { get; set; } = null!;
    }

    public class YandexVendorInfo
    {
        [JsonPropertyName("vendorId")]
        public string VendorId { get; set; } = null!;

        [JsonPropertyName("deliveryType")]
        public string DeliveryType { get; set; } = null!;
    }

    public class YandexBlockStatusRequest
    {
        [JsonPropertyName("vendorInfo")]
        public List<YandexVendorInfo> VendorInfo { get; set; } = new();

        [JsonPropertyName("statuses")]
        public List<string>? Statuses { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 100;

        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;
    }

    public class YandexBlockStatusResponse
    {
        public List<YandexBlockStatusItem> Items { get; set; } = new();
    }

    public class YandexBlockStatusItem
    {
        public string VendorId { get; set; } = null!;

        public string BlockId { get; set; } = null!;

        public string? ExternalBlockId { get; set; }

        public string DeliveryType { get; set; } = null!;

        public bool AvailableToCancel { get; set; }

        public string Status { get; set; } = null!;

        public string From { get; set; } = null!;

        public string? To { get; set; }

        public string? Source { get; set; }

        public string? Message { get; set; }
    }

    public class YandexUnblockRequest
    {
        [JsonPropertyName("vendorInfo")]
        public List<YandexVendorInfo> VendorInfo { get; set; } = new();

        [JsonPropertyName("blockIds")]
        public List<string> BlockIds { get; set; } = new();
    }

    public class YandexMenuImportRequest
    {
        [JsonPropertyName("restaurantId")]
        public string RestaurantId { get; set; } = null!;

        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = "menu";
    }

    public class YandexOrdersHistoryRequest
    {
        [JsonPropertyName("origin_ids")]
        public List<YandexOriginId> OriginIds { get; set; } = new();

        [JsonPropertyName("service")]
        public List<string>? Service { get; set; }

        [JsonPropertyName("from")]
        public string From { get; set; } = null!;

        [JsonPropertyName("to")]
        public string To { get; set; } = null!;

        [JsonPropertyName("status")]
        public List<string>? Status { get; set; }

        [JsonPropertyName("pagination")]
        public YandexPagination Pagination { get; set; } = new();
    }

    public class YandexOriginId
    {
        [JsonPropertyName("origin_id")]
        public string OriginId { get; set; } = null!;

        [JsonPropertyName("delivery_type")]
        public string DeliveryType { get; set; } = null!;
    }

    public class YandexPagination
    {
        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }

    public class YandexOrdersHistoryResponse
    {
        [JsonPropertyName("orders")]
        public List<YandexOrderHistoryItem> Orders { get; set; } = new();
    }

    public class YandexOrderHistoryItem
    {
        [JsonPropertyName("eats_id")]
        public string EatsId { get; set; } = null!;

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = null!;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;

        [JsonPropertyName("service")]
        public string Service { get; set; } = null!;

        [JsonPropertyName("delivery_type")]
        public string DeliveryType { get; set; } = null!;

        [JsonPropertyName("origin_id")]
        public string OriginId { get; set; } = null!;

        [JsonPropertyName("items_cost")]
        public decimal ItemsCost { get; set; }

        [JsonPropertyName("cancel_reason")]
        public YandexCancelReason? CancelReason { get; set; }
    }

    public class YandexCancelReason
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = null!;

        [JsonPropertyName("message")]
        public string Message { get; set; } = null!;
    }

    public class YandexOrderDetailsRequest
    {
        [JsonPropertyName("eats_ids")]
        public List<string> EatsIds { get; set; } = new();
    }

    public class YandexOrderDetailsResponse
    {
        [JsonPropertyName("orders")]
        public List<YandexOrderDetails> Orders { get; set; } = new();

        [JsonPropertyName("not_found_eats_ids")]
        public List<string> NotFoundEatsIds { get; set; } = new();
    }

    public class YandexOrderDetails
    {
        [JsonPropertyName("eats_id")]
        public string EatsId { get; set; } = null!;

        [JsonPropertyName("order_id")]
        public string? OrderId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;

        [JsonPropertyName("items")]
        public List<YandexOrderDetailsItem> Items { get; set; } = new();

        [JsonPropertyName("items_cost")]
        public decimal ItemsCost { get; set; }

        [JsonPropertyName("cancel_reason")]
        public YandexCancelReason? CancelReason { get; set; }
    }

    public class YandexOrderDetailsItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        // base_price — сумма строки (цена × количество), не цена за единицу
        [JsonPropertyName("base_price")]
        public decimal Price { get; set; }

        [JsonPropertyName("original_price")]
        public decimal OriginalPrice { get; set; }

        [JsonPropertyName("weight")]
        public decimal Weight { get; set; }

        [JsonPropertyName("measure_unit")]
        public string MeasureUnit { get; set; } = null!;

        [JsonPropertyName("modifications")]
        public List<YandexOrderItemOption> Modifications { get; set; } = new();
    }

    public class YandexOrderItemOption
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("group_id")]
        public string GroupId { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }
    public class OrderCancelRequest
    {
        public string EatsId { get; set; } = null!;

        public string? Comment { get; set; }
    }

    // Результат загрузки продаж Яндекса (GlobalFunctionService.LoadYandexSalesAsync)
    public class YandexSalesLoadResult
    {
        // Записано строк продаж
        public int Loaded { get; set; }

        // Записано заказов (включая перезаписанные)
        public int LoadedOrders { get; set; }

        // Пропущено заказов — уже записаны
        public int SkippedOrders { get; set; }

        // Перезаписано заказов — были строки с DELETED = 1
        public int RewrittenOrders { get; set; }
    }
}
