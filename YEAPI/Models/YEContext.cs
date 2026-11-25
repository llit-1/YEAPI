using Microsoft.EntityFrameworkCore;

namespace YEAPI.Models
{
    public class YEContext : DbContext
    {
        public YEContext(DbContextOptions<YEContext> options)
            : base(options)
        {
        }

        public DbSet<Location> Locations => Set<Location>();

        public DbSet<YEGroup> YEGroups => Set<YEGroup>();

        public DbSet<YEItem> YEItems => Set<YEItem>();

        public DbSet<YEDeclaration> YEDeclarations => Set<YEDeclaration>();

        public DbSet<YEOrder> YEOrders => Set<YEOrder>();

        public DbSet<YEOrderItem> YEOrderItems => Set<YEOrderItem>();

        public DbSet<YEHttpsLog> YEHttpsLog => Set<YEHttpsLog>();
        public DbSet<YESettings> YESettings => Set<YESettings>();

        public DbSet<YEStop> YEStops => Set<YEStop>();
        public DbSet<YEStopPack> YEStopPack => Set<YEStopPack>();

        public DbSet<YEStoplog> YEStoplogs => Set<YEStoplog>();

        public DbSet<YECashClient> YECashClients => Set<YECashClient>();

        public DbSet<YEServicesLog> YEServicesLogs => Set<YEServicesLog>();
        public DbSet<SALEOBJECTSAGREGATORS> SALEOBJECTSAGREGATORS => Set<SALEOBJECTSAGREGATORS>();

        public DbSet<ApiClient> ApiClients => Set<ApiClient>();
        public DbSet<YECashVersion> YECashVersions => Set<YECashVersion>();
        public DbSet<YECashTask> YECashTasks => Set<YECashTask>();
        public DbSet<YERestaurantBlockLog> YERestaurantBlockLogs => Set<YERestaurantBlockLog>();
        public DbSet<YECancelCode> YECancelCodes => Set<YECancelCode>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<YEOrderItem>()
                .HasKey(x => new { x.Order, x.Item });
        }
    }
}
