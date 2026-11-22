using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GroupsService.Infrastructure.Data;

public class GroupDbContextFactory : IDesignTimeDbContextFactory<GroupDbContext>
{
    public GroupDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GroupDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                               ?? "Host=localhost;Port=5435;Database=groupsdb;Username=postgres;Password=changeme";

        optionsBuilder.UseNpgsql(connectionString);

        return new GroupDbContext(optionsBuilder.Options);
    }
}