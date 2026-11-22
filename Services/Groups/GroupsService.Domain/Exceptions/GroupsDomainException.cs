namespace GroupsService.Domain.Exceptions;

public class GroupsDomainException : Exception
{
    public GroupsDomainException(string message) : base(message) { }

    public GroupsDomainException(string message, Exception inner) : base(message, inner) { }
}