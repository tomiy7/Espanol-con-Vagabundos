using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentsService.Infrastructure.Data;

public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                               ?? "Host=localhost;Port=5436;Database=paymentsdb;Username=postgres;Password=changeme";

        optionsBuilder.UseNpgsql(connectionString);

        return new PaymentDbContext(optionsBuilder.Options);
    }
}