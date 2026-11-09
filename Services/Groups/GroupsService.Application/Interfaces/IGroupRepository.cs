using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Interfaces;

namespace GroupsService.Application.Interfaces;

public interface IGroupRepository : IAsyncRepository<Group>
{
    // Lista grupa za profesora/admina, sa opcionim filterima. professorId vraca grupe
    // u kojima taj profesor drzi bar jedan termin ("moje grupe" i zahtevi za Individual).
    Task<List<Group>> GetListAsync(Guid? courseId, GroupStatus? status, GroupType? type, Guid? professorId);

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