using System.Collections.Concurrent;
using GroupsService.Infrastructure.Messaging;
using Messaging;
using CourseCompletedEvent = Messaging.Contracts.CourseCompleted;

namespace GroupsService.Integration.Tests.Infrastructure;

// Sta test zeli da pukne u obradi CourseCompleted poruke. Jedna instanca za ceo test
// (singleton), pa test posle vidi koliko je pokusaja bilo i u kojim instancama.
public sealed class HandlerFaults
{
    public ConcurrentDictionary<Guid, int> Attempts { get; } = new();
    public ConcurrentBag<object> HandlerInstances { get; } = new();

    // Prvih N pokusaja za svaku poruku baca gresku, posle radi normalno.
    public int FailFirstAttempts { get; set; }

    // Upisi za koje obrada uvek puca (trajna greska).
    public HashSet<Guid> AlwaysFail { get; } = new();

    // Izvrsava se pre prvog pokusaja (npr. da napravi pravu trku sa drugim procesom).
    public Func<IServiceProvider, CourseCompletedEvent, Task>? BeforeFirstAttempt { get; set; }

    public int AttemptsFor(Guid enrollmentId) => Attempts.GetValueOrDefault(enrollmentId);
}

// Omotac oko pravog handler-a koji ubacuje greske po uputstvu iz HandlerFaults.
public sealed class FaultyCourseCompletedHandler : IIntegrationEventHandler<CourseCompletedEvent>
{
    private readonly CourseCompletedEventHandler _inner;
    private readonly HandlerFaults _faults;
    private readonly IServiceProvider _services;

    public FaultyCourseCompletedHandler(CourseCompletedEventHandler inner, HandlerFaults faults, IServiceProvider services)
    {
        _inner = inner;
        _faults = faults;
        _services = services;
    }

    public async Task HandleAsync(CourseCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        var attempt = _faults.Attempts.AddOrUpdate(integrationEvent.EnrollmentId, 1, (_, count) => count + 1);
        _faults.HandlerInstances.Add(this);

        if (_faults.AlwaysFail.Contains(integrationEvent.EnrollmentId))
            throw new InvalidOperationException("Injected permanent handler failure.");

        if (attempt <= _faults.FailFirstAttempts)
            throw new TimeoutException("Injected transient handler failure.");

        if (attempt == 1 && _faults.BeforeFirstAttempt is not null)
            await _faults.BeforeFirstAttempt(_services, integrationEvent);

        await _inner.HandleAsync(integrationEvent, cancellationToken);
    }
}
