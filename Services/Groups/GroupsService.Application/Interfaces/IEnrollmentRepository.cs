using GroupsService.Domain.Entities;
using GroupsService.Domain.Interfaces;

namespace GroupsService.Application.Interfaces;

public interface IEnrollmentRepository : IAsyncRepository<Enrollment>
{
    Task<List<Enrollment>> GetByUserIdAsync(Guid userId);
    Task<List<Enrollment>> GetByGroupIdAsync(Guid groupId);
    // Upis korisnika na kurs koji nije otkazan (Searching/Ready/Active/Frozen/Completed).
    // Zavrsen kurs se ne pohadja ponovo, pa i Completed blokira novi upis; otkazan ne
    // blokira. Po definiciji ih je najvise jedan.
    Task<Enrollment?> GetNonCancelledAsync(Guid userId, Guid courseId);
}