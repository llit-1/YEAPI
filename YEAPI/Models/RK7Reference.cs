namespace YEAPI.Models
{
    public class RK7Reference
    {
        public List<RK7Category> Categories { get; set; } = new();
    }

    public class RK7Category
    {
        public int Code { get; set; }
        public string Name { get; set; } = "";

        public List<RK7Category> Categories { get; set; } = new();
        public List<RK7MenuItem> Items { get; set; } = new();
    }

    public class RK7MenuItem
    {
        public int Code { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }
}
