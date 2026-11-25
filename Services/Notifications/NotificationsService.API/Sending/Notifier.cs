using Messaging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NotificationsService.API.Data;
using NotificationsService.API.Email;
using NotificationsService.API.Recipients;
using NotificationsService.API.Templates;

namespace NotificationsService.API.Sending;

// Salje jedan mejl jednom primaocu za jedan dogadjaj, najvise jednom:
//  1. vec poslato (ili namerno preskoceno) za isti (dogadjaj, primalac, sablon) -> nista;
//  2. kontakt iz Auth-a; nedostupan Auth -> izuzetak (poruka se ponavlja), nepostojeci
//     korisnik ili bez mejla -> zapis "Skipped", bez mejla;
//  3. zapis "Pending", slanje, zapis "Sent". Neuspelo slanje ostaje Pending sa greskom i
//     izuzetak ide dalje (ponavljanje, pa dead-letter).
// Ako proces padne izmedju slanja i upisa "Sent", ponovljena poruka salje mejl jos jednom -
// redak slucaj, prihvacen (bolje dupli nego izgubljen mejl).
public sealed class Notifier
{
    private readonly NotificationsDbContext _db;
    private readonly IUserDirectory _users;
    private readonly IEmailSender _sender;
    private readonly ILogger<Notifier> _logger;

    public Notifier(NotificationsDbContext db, IUserDirectory users, IEmailSender sender, ILogger<Notifier> logger)
    {
        _db = db;
        _users = users;
        _sender = sender;
        _logger = logger;
    }

    public async Task NotifyAsync(
        IntegrationEvent integrationEvent, Guid recipientUserId, string template,
        Func<UserContact, EmailMessage> compose, CancellationToken cancellationToken)
    {
        var record = await _db.SentNotifications.SingleOrDefaultAsync(n =>
            n.EventId == integrationEvent.EventId && n.RecipientUserId == recipientUserId && n.Template == template,
            cancellationToken);

        if (record is { Status: NotificationStatus.Sent or NotificationStatus.Skipped })
        {
            _logger.LogInformation("Notification {Template} for event {EventId} was already handled, skipping.",
                template, integrationEvent.EventId);
            return;
        }

        var contact = await _users.GetUserContactAsync(recipientUserId, cancellationToken);

        if (record is null)
        {
            record = new SentNotification
            {
                Id = Guid.NewGuid(),
                EventId = integrationEvent.EventId,
                EventType = integrationEvent.GetType().Name,
                RecipientUserId = recipientUserId,
                Template = template,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SentNotifications.Add(record);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Isti dogadjaj upravo obradjuje druga instanca - ona salje mejl.
                _logger.LogInformation("Notification {Template} for event {EventId} is handled elsewhere.",
                    template, integrationEvent.EventId);
                return;
            }
        }

        if (contact is null)
        {
            record.Status = NotificationStatus.Skipped;
            record.Error = "User does not exist or has no email address.";
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("No email address for user {UserId}, {Template} for event {EventId} was not sent.",
                recipientUserId, template, integrationEvent.EventId);
            return;
        }

        record.Email = contact.Email;
        record.Attempts++;
        try
        {
            await _sender.SendAsync(contact.Email, contact.FullName, compose(contact), cancellationToken);
        }
        catch (Exception e)
        {
            record.Error = e.Message.Length > 2000 ? e.Message[..2000] : e.Message;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        record.Status = NotificationStatus.Sent;
        record.SentAt = DateTime.UtcNow;
        record.Error = null;
        await _db.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("Sent {Template} to user {UserId} for event {EventId}.",
            template, recipientUserId, integrationEvent.EventId);
    }
}
