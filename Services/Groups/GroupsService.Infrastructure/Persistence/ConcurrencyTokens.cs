namespace GroupsService.Infrastructure.Persistence;

public static class ConcurrencyTokens
{
    // Shadow property (nije deo domena) koje Npgsql mapira na sistemsku kolonu xmin.
    public const string RowVersion = "RowVersion";
}
