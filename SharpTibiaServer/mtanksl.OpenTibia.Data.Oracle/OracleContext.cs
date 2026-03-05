using Microsoft.EntityFrameworkCore;
using OpenTibia.Data.Contexts;

namespace OpenTibia.Data.Oracle.Contexts
{
    public class OracleContext : DatabaseContext
    {
        private string connectionString;

        public OracleContext(string connectionString) : base()
        {
            this.connectionString = connectionString;
        }

        public OracleContext(string connectionString, DbContextOptions options) : base(options)
        {
            this.connectionString = connectionString;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes() )
            {
                modelBuilder.Entity(entityType.ClrType).ToTable("mtots_" + entityType.GetTableName() );
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseOracle(connectionString, options =>
            {
                options.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
            } );

            base.OnConfiguring(optionsBuilder);
        }
    }
}