namespace CoursesService.API.DTOs;

// Redovne cene po osobi (null = cena jos nije uneta u Directus-u). Popuste racuna Payments.
public sealed record PricesDto(decimal? Individual, decimal? Pair, decimal? Group, decimal? Ebook)
{
    public static PricesDto From(Entities.Course c) => new(c.PriceIndividual, c.PricePair, c.PriceGroup, c.EbookPrice);
}

// Za Payments: cene po ID-u kursa, i za kurs koji vise nije u ponudi.
public sealed record CoursePricingDto(
    Guid CourseId, string Currency, decimal? Individual, decimal? Pair, decimal? Group, decimal? Ebook);
