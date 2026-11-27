using System.Net;
using System.Text.Json;
using CoursesService.Tests.Infrastructure;

namespace CoursesService.Tests.Api;

// Javni katalog: objavljeni kursevi, tri cene po osobi, bez ID-a PDF-a e-knjige.
[Collection(CoursesCollection.Name)]
public class CatalogTests(CoursesFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [IntegrationFact]
    public async Task List_shows_only_published_courses_with_all_prices()
    {
        var course = Seed.Course();
        course.PriceIndividual = 15000;
        course.PricePair = 11000;
        course.PriceGroup = 8000;
        course.EbookPrice = 1500;
        await factory.SeedAsync(course, Seed.Course(published: false));

        var list = await Json(await factory.CreateClient().GetAsync("/courses"));

        var item = Assert.Single(list.EnumerateArray());
        Assert.Equal(course.Id, item.GetProperty("id").GetGuid());
        var prices = item.GetProperty("prices");
        Assert.Equal(15000, prices.GetProperty("individual").GetDecimal());
        Assert.Equal(11000, prices.GetProperty("pair").GetDecimal());
        Assert.Equal(8000, prices.GetProperty("group").GetDecimal());
        Assert.Equal(1500, prices.GetProperty("ebook").GetDecimal());
        Assert.Equal(9000, item.GetProperty("price").GetDecimal()); // stara cena ostaje dok front ne predje
    }

    [IntegrationFact]
    public async Task Prices_not_entered_yet_are_null_and_do_not_break_the_list()
    {
        await factory.SeedAsync(Seed.Course());

        var list = await Json(await factory.CreateClient().GetAsync("/courses"));

        var prices = Assert.Single(list.EnumerateArray()).GetProperty("prices");
        Assert.Equal(JsonValueKind.Null, prices.GetProperty("individual").ValueKind);
        Assert.Equal(JsonValueKind.Null, prices.GetProperty("group").ValueKind);
    }

    [IntegrationFact]
    public async Task Detail_lists_visible_lessons_in_order_and_hides_the_ebook_file()
    {
        var course = Seed.Course();
        course.EbookId = Guid.NewGuid();
        var second = Seed.Lesson(course, 5);
        var first = Seed.Lesson(course, 0);
        await factory.SeedAsync(course, second, first,
            Seed.Lesson(course, 1, published: false), Seed.Lesson(course, 2, visible: false));

        var detail = await Json(await factory.CreateClient().GetAsync($"/courses/{course.Slug}"));

        Assert.Equal(new[] { first.Id, second.Id },
            detail.GetProperty("lessons").EnumerateArray().Select(l => l.GetProperty("id").GetGuid()));
        Assert.False(detail.TryGetProperty("ebookPdf", out _));
        Assert.DoesNotContain(course.EbookId.ToString()!, detail.GetRawText());
        Assert.False(detail.GetProperty("lessons")[0].TryGetProperty("recordingUrl", out _));
    }

    [IntegrationFact]
    public async Task Detail_by_id_matches_detail_by_slug()
    {
        var course = Seed.Course();
        await factory.SeedAsync(course, Seed.Lesson(course, 1));

        var byId = await Json(await factory.CreateClient().GetAsync($"/courses/{course.Id}"));
        var bySlug = await Json(await factory.CreateClient().GetAsync($"/courses/{course.Slug}"));

        Assert.Equal(bySlug.GetRawText(), byId.GetRawText());
    }

    [IntegrationFact]
    public async Task Unpublished_course_is_not_found_by_id()
    {
        var hidden = Seed.Course(published: false);
        await factory.SeedAsync(hidden);

        var response = await factory.CreateClient().GetAsync($"/courses/{hidden.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationFact]
    public async Task Unpublished_or_unknown_course_detail_is_not_found()
    {
        var hidden = Seed.Course(published: false);
        await factory.SeedAsync(hidden);

        var unpublished = await factory.CreateClient().GetAsync($"/courses/{hidden.Slug}");
        var unknown = await factory.CreateClient().GetAsync("/courses/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, unpublished.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains("COURSE_NOT_FOUND", await unknown.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task Pricing_works_for_unpublished_courses_and_carries_the_currency()
    {
        var course = Seed.Course(published: false);
        course.PricePair = 11000;
        await factory.SeedAsync(course);

        var pricing = await Json(await factory.CreateClient().GetAsync($"/courses/{course.Id}/pricing"));

        Assert.Equal(course.Id, pricing.GetProperty("courseId").GetGuid());
        Assert.Equal("RSD", pricing.GetProperty("currency").GetString());
        Assert.Equal(11000, pricing.GetProperty("pair").GetDecimal());
        Assert.Equal(JsonValueKind.Null, pricing.GetProperty("individual").ValueKind);
    }

    [IntegrationFact]
    public async Task Pricing_of_unknown_course_is_not_found_with_code()
    {
        var response = await factory.CreateClient().GetAsync($"/courses/{Guid.NewGuid()}/pricing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("COURSE_NOT_FOUND", await response.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task Health_reports_healthy_with_the_database()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
