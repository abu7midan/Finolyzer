using Finolyzer.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.SettingManagement.EntityFrameworkCore;

namespace Finolyzer.Data
{
    public class FinolyzerDbContext : AbpDbContext<FinolyzerDbContext>
    {
        public DbSet<Portfolio> Portfolios { get; set; }
        public DbSet<ApplicationSystem> ApplicationSystems { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<Provider> Providers { get; set; }
        public DbSet<SystemDependency> SystemDependencies { get; set; }
        public DbSet<CostSummaryRequest> CostSummaryRequests { get; set; }
        public DbSet<IntegrationService> IntegrationServices { get; set; }
        public DbSet<ProviderSubscription> ProviderSubscriptions { get; set; }
        public DbSet<Server> Servers { get; set; }
        public DbSet<SystemIntegrationTransaction> SystemIntegrationTransactions { get; set; }

        public const string DbTablePrefix = "";
        public const string DbSchema = null;

        public FinolyzerDbContext(DbContextOptions<FinolyzerDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure ABP module tables if needed
            // builder.ConfigurePermissionManagement();
            //builder.ConfigureSettingManagement();
            // builder.ConfigureBackgroundJobs();
            builder.ConfigureAuditLogging();

            builder.Entity<Portfolio>(b =>
            {
                b.ToTable(DbTablePrefix + "Portfolios", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).IsRequired().HasMaxLength(128);
                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("PortfolioId");
                    y.ToTable(DbTablePrefix + "Portfolios_CustomCosts", DbSchema);
                    y.HasKey("PortfolioId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<ApplicationSystem>(b =>
            {
                b.ToTable(DbTablePrefix + "ApplicationSystems", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).IsRequired().HasMaxLength(500);
                b.HasOne(x => x.Portfolio).WithMany(x => x.ApplicationSystems).HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.NoAction);
                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("ApplicationSystemId");
                    y.ToTable(DbTablePrefix + "ApplicationSystems_CustomCosts", DbSchema);
                    y.HasKey("ApplicationSystemId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<Resource>(b =>
            {
                b.ToTable(DbTablePrefix + "Resources", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).IsRequired().HasMaxLength(500);
                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("ResourceId");
                    y.ToTable(DbTablePrefix + "Resources_CustomCosts", DbSchema);
                    y.HasKey("ResourceId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<Provider>(b =>
            {
                b.ToTable(DbTablePrefix + "Providers", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).IsRequired().HasMaxLength(500);
                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("ProviderId");
                    y.ToTable(DbTablePrefix + "Providers_CustomCosts", DbSchema);
                    y.HasKey("ProviderId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<SystemDependency>(b =>
            {
                b.ToTable(DbTablePrefix + "SystemDependencies", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Year).IsRequired();
                b.Property(x => x.Month).IsRequired();

                b.HasOne(x => x.ApplicationSystem)
                 .WithMany(y => y.SystemDependencies)
                 .HasForeignKey(x => x.ApplicationSystemId)
                 .IsRequired()
                 .OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.Server).WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.ProviderSubscription).WithMany().HasForeignKey(x => x.ProviderSubscriptionId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.IntegrationService).WithMany().HasForeignKey(x => x.IntegrationServiceId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.Resource).WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.NoAction);

                b.HasIndex(x => new { x.Month, x.Year });

                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("SystemDependencyId");
                    y.ToTable(DbTablePrefix + "SystemDependencies_CustomCosts", DbSchema);
                    y.HasKey("SystemDependencyId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<CostSummaryRequest>(b =>
            {
                b.ToTable(DbTablePrefix + "CostSummaryRequests", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Notes).IsRequired().HasMaxLength(5000);
                b.Property(x => x.Description).IsRequired().HasMaxLength(5000);

                b.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.ApplicationSystem).WithMany().HasForeignKey(x => x.ApplicationSystemId).OnDelete(DeleteBehavior.NoAction);

                b.HasIndex(x => new { x.PortfolioId, x.ApplicationSystemId, x.Quarter, x.Month, x.Year }).IsUnique();
            });

            builder.Entity<IntegrationService>(b =>
            {
                b.ToTable(DbTablePrefix + "IntegrationServices", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Description).IsRequired().HasMaxLength(5000);
                b.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.NoAction);

                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("IntegrationServiceId");
                    y.ToTable(DbTablePrefix + "IntegrationServices_CustomCosts", DbSchema);
                    y.HasKey("IntegrationServiceId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<ProviderSubscription>(b =>
            {
                b.ToTable(DbTablePrefix + "ProviderSubscriptions", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Description).IsRequired().HasMaxLength(5000);
                b.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.NoAction);

                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("ProviderSubscriptionId");
                    y.ToTable(DbTablePrefix + "ProviderSubscriptions_CustomCosts", DbSchema);
                    y.HasKey("ProviderSubscriptionId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<Server>(b =>
            {
                b.ToTable(DbTablePrefix + "Servers", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Specification).IsRequired().HasMaxLength(2000);
                b.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.NoAction);

                b.OwnsMany(x => x.CustomCosts, y =>
                {
                    y.WithOwner().HasForeignKey("ServerId");
                    y.ToTable(DbTablePrefix + "Servers_CustomCosts", DbSchema);
                    y.HasKey("ServerId", "Id");
                    y.Property(x => x.Description).IsRequired();
                });
            });

            builder.Entity<SystemIntegrationTransaction>(b =>
            {
                b.ToTable(DbTablePrefix + "SystemIntegrationTransactions", DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Description).IsRequired().HasMaxLength(2000);
                b.HasOne(x => x.IntegrationService).WithMany().HasForeignKey(x => x.IntegrationServiceId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.ApplicationSystem).WithMany().HasForeignKey(x => x.ApplicationSystemId).OnDelete(DeleteBehavior.NoAction);

                b.HasIndex(x => new { x.Day, x.Month, x.Year }).IsUnique();
            });
        }
    }
}