using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PRN222.RagAssistant.Data;

public sealed class DataProtectionKeyDbContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeyDbContext(DbContextOptions<DataProtectionKeyDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
