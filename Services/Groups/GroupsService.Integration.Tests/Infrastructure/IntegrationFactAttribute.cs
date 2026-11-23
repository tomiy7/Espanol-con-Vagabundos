namespace GroupsService.Integration.Tests.Infrastructure;

// Integracioni testovi traze pravi Postgres (env GROUPS_TEST_CONNECTION). Bez njega se
// PRESKACU sa jasnom porukom, umesto da 77 testova padne - pa "dotnet test" radi i na
// masini bez baze (npr. samo unit testovi u CI-ju dok baza tamo ne postoji).
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!ApiFactory.IsConfigured) Skip = ApiFactory.NotConfiguredMessage;
    }
}

public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (!ApiFactory.IsConfigured) Skip = ApiFactory.NotConfiguredMessage;
    }
}
