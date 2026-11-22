using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application;

public static class EnrollmentGuard
{
    public static GroupsDomainException Blocked(Enrollment existing, bool self = true)
    {
        if (existing.Status == EnrollmentStatus.Completed)
            return new GroupsDomainException(self
                ? "You have already completed this course."
                : "This person has already completed this course.");

        return new GroupsDomainException(self
            ? "You already have an active or pending enrollment for this course."
            : "This person already has an active or pending enrollment for this course.");
    }
}