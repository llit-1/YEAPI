using System.Xml.Linq;
using YEAPI.Models;

namespace YEAPI.RK7
{
    public static class RK7Parser
    {
        // Старый парсер — с фильтрацией по цене
        public static RK7Reference Parse(string xml)
        {
            return ParseInternal(xml, filterByPrice: true);
        }

        // Новый парсер — все позиции независимо от цены
        public static RK7Reference ParseAll(string xml)
        {
            return ParseInternal(xml, filterByPrice: false);
        }

        private static RK7Reference ParseInternal(
            string xml,
            bool filterByPrice)
        {
            var doc = XDocument.Parse(xml);

            var referenceElement = doc.Root?
                .Element("RK7Reference");

            if (referenceElement == null)
                throw new Exception("RK7Reference not found");

            var result = new RK7Reference();

            var childItems = referenceElement.Element("RIChildItems");

            if (childItems != null)
            {
                result.Categories = ParseCategories(
                    childItems,
                    filterByPrice);
            }

            return result;
        }

        private static List<RK7Category> ParseCategories(
            XElement parent,
            bool filterByPrice)
        {
            var result = new List<RK7Category>();

            foreach (var categoryElement in
                     parent.Elements("TCategListItem"))
            {
                var category = new RK7Category
                {
                    Code = (int)categoryElement.Attribute("Code")!,
                    Name = (string)categoryElement.Attribute("Name")!
                };

                var childItems =
                    categoryElement.Element("RIChildItems");

                if (childItems != null)
                {
                    category.Categories =
                        ParseCategories(
                            childItems,
                            filterByPrice);

                    category.Items =
                        ParseItems(
                            childItems,
                            filterByPrice);
                }

                // Добавляем категорию только если в ней
                // есть позиции или дочерние категории
                if (category.Items.Count > 0 ||
                    category.Categories.Count > 0)
                {
                    result.Add(category);
                }
            }

            return result;
        }

        private static List<RK7MenuItem> ParseItems(
            XElement parent,
            bool filterByPrice)
        {
            var result = new List<RK7MenuItem>();

            foreach (var x in
                     parent.Elements("TRK7MenuItem"))
            {
                var priceString =
                    (string?)x.Attribute("PRICETYPES-1017607");

                // =====================================================
                // РЕЖИМ GetMenu()
                // =====================================================

                if (filterByPrice)
                {
                    // Если цены нет — пропускаем
                    if (string.IsNullOrWhiteSpace(priceString))
                        continue;

                    // Если цена длиннее 7 символов — пропускаем
                    if (priceString.Length > 8 || priceString == "0")
                        priceString = "0";

                    // Если цена не число — пропускаем
                    if (!decimal.TryParse(
                            priceString,
                            out var price))
                        continue;

                    result.Add(new RK7MenuItem
                    {
                        Code = (int)x.Attribute("Code")!,
                        Name = (string)x.Attribute("Name")!,
                        Price = price
                    });

                    continue;
                }

                // =====================================================
                // РЕЖИМ GetALLMenu()
                // =====================================================

                decimal allPrice = 0;

                // Если цена есть и она корректная —
                // используем её.
                if (!string.IsNullOrWhiteSpace(priceString))
                {
                    decimal.TryParse(
                        priceString,
                        out allPrice);
                }

                result.Add(new RK7MenuItem
                {
                    Code = (int)x.Attribute("Code")!,
                    Name = (string)x.Attribute("Name")!,
                    Price = allPrice
                });
            }

            return result;
        }
    }
}
