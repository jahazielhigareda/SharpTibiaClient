using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenTibia.Data.Oracle.Contexts
{
    public class OracleContextFactory : IDesignTimeDbContextFactory<OracleContext>
    {
        public OracleContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OracleContext>();

            return new OracleContext(args[0], optionsBuilder.Options);
        }
    }
}