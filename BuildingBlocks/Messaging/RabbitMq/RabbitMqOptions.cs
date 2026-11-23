namespace Messaging.RabbitMq;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    // Iskljuceno: nema konekcije ni potrosaca (npr. integracioni testovi bez RabbitMQ-a).
    // Outbox i dalje upisuje poruke, pa se nista ne gubi - samo se ne salju.
    public bool Enabled { get; set; } = true;

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = string.Empty;

    // Zajednicki topic exchange za sve servise; routing key = tip dogadjaja.
    public string Exchange { get; set; } = "coursesite.events";

    // Red ovog servisa (npr. "groups"). Neuspele poruke idu u "{QueueName}.dead".
    public string QueueName { get; set; } = string.Empty;

    // Koliko puta se poruka pokusava obraditi (sa pauzom) pre dead-letter reda.
    public int MaxDeliveryAttempts { get; set; } = 5;

    public ushort PrefetchCount { get; set; } = 10;
}
