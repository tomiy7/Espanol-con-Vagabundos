using System.Text.Json;
using System.Text.Json.Serialization;

namespace Messaging;

// Dogadjaj koji jedan servis objavljuje, a drugi servisi slusaju (ugovor izmedju servisa).
// Namerno odvojen od domenskih dogadjaja: domenski dogadjaj je interni detalj agregata,
// a integracioni je javni ugovor koji se menja oprezno (samo dodavanje polja).
public abstract record IntegrationEvent
{
    // Jedinstven id poruke - potrosac po njemu prepoznaje duplikat (isporuka je "bar jednom").
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// Oblik poruke na RabbitMQ-u. Tip dogadjaja je i routing key, pa svaki servis vezuje
// svoj red samo za tipove koje stvarno obradjuje (topic exchange).
public sealed record EventEnvelope(Guid EventId, string EventType, DateTime OccurredAt, JsonElement Data)
{
    // Enum-i kao tekst ("Monday", ne 1) - citljivo i stabilno za druge servise; brojevi se
    // i dalje prihvataju pri citanju.
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string TypeOf<TEvent>() where TEvent : IntegrationEvent => TypeOf(typeof(TEvent));

    public static string TypeOf(Type eventType) => eventType.Name;

    public static EventEnvelope From(IntegrationEvent integrationEvent) => new(
        integrationEvent.EventId,
        TypeOf(integrationEvent.GetType()),
        integrationEvent.OccurredAt,
        JsonSerializer.SerializeToElement(integrationEvent, integrationEvent.GetType(), JsonOptions));
}
