using System.Text.RegularExpressions;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.ValueObjects;

public class Slot : Entity
{
    public int SlotIndex { get; private set; } // 0, 1 ili 2
    public DayOfWeek? DayOfWeek { get; private set; }
    public TimeOnly? Time { get; private set; }
    public Guid? ProfessorId { get; private set; }
    
    public bool IsAssigned => ProfessorId.HasValue && DayOfWeek.HasValue && Time.HasValue;
    
    private Slot() { }
    
    public static Slot CreateEmpty(int slotIndex)
    {
        if (slotIndex is < 0 or > 2)
            throw new GroupsDomainException("Slot index must be between 0 and 2.");

        return new Slot { Id = Guid.NewGuid(), SlotIndex = slotIndex };
    }
    
    public void Assign(DayOfWeek dayOfWeek, TimeOnly time, Guid professorId)
    {
        if (professorId == Guid.Empty)
            throw new GroupsDomainException("Professor ID cannot be empty.");

        DayOfWeek = dayOfWeek;
        Time = time;
        ProfessorId = professorId;
    }

    public void Clear()
    {
        DayOfWeek = null;
        Time = null;
        ProfessorId = null;
    }
}