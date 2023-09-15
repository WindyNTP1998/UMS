using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UMS.Services.Entities;

namespace UMS.Services.Repository;

public class UmsEfCoreDbContext : IdentityDbContext<ApplicationUser>
{
    public UmsEfCoreDbContext(DbContextOptions<UmsEfCoreDbContext> optionsBuilderOptions) :
        base(optionsBuilderOptions)
    {
    }

    public DbSet<ApplicationUser> ApplicationUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}

