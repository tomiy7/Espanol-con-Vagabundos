namespace GroupsService.Domain.Enums;

// Kako ucenik placa kurs: odjednom ili u ratama (broj rata i rokove odredjuje Payments).
// Bira se pri upisu; dok grupa nije potvrdjena menja se ovde, posle toga u Payments-u.
public enum PaymentPlan
{
    Full,
    Installments
}
