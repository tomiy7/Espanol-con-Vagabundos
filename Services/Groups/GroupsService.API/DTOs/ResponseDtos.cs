namespace GroupsService.API.DTOs;

// Tipizirani odgovori (umesto anonimnih objekata) - vide se u Swagger-u i front zna oblik.
public record EnrollmentCreatedResponse(Guid EnrollmentId, Guid GroupId);

public record GroupCreatedResponse(Guid GroupId);

public record InvitationCreatedResponse(Guid EnrollmentId, Guid InvitationId);

public record InvitationAcceptedResponse(Guid EnrollmentId, Guid GroupId);
