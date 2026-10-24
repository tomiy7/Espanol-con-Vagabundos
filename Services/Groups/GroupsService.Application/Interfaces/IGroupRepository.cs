using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Interfaces;

namespace GroupsService.Application.Interfaces;

public interface IGroupRepository : IAsyncRepository<Group>
{
    // Otvorene Group-tip grupe za kurs (Forming/Ready, ispod MaxSize) - gde CreateGroupEnrollment
    // pokusava prvo da nadje mesto pre nego sto otvori novu grupu.
    Task<Group?> FindOpenGroupForCourseAsync(Guid courseId, GroupType type);

    Task<List<Group>> GetAvailableForCourseAsync(Guid courseId);

    // Nivo 1 zastite od duplog zakazivanja profesora: pre nego sto se slot potvrdi,
    // provera da taj profesor vec nema termin na isti DayOfWeek+Time u NEKOJ DRUGOJ
    // (ne-Completed) grupi, bez obzira na kurs - profesor moze drzati casove u vise
    // kurseva/grupa paralelno, ali ne u isto vreme. excludeGroupId izuzima grupu koja
    // se bas sad azurira (reassign istog slota ne treba da se kosi sa samim sobom).
    // Ne pokriva konkretne datume/praznike - to je "Nivo 2" (vidi komentar u Slot.cs).
    Task<bool> HasConflictingSlotAsync(
        Guid professorId, DayOfWeek dayOfWeek, TimeOnly time, Guid excludeGroupId);
}