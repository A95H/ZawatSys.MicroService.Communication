using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data;

public sealed class CommunicationDbContextFactory : IDesignTimeDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CommunicationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=ZawatSys.Communication;Username=postgres;Password=admin");

        return new CommunicationDbContext(optionsBuilder.Options);
    }
}
