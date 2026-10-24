namespace GroupsService.Domain.Enums;

public enum EnrollmentStatus
{
    // Ceka partnera (Pair, pre prihvatanja) ili popunu grupe (Group, u formation window-u).
    // Individual ovde ostaje samo trenutak, do Ready (odmah po kreiranju privatne Group).
    Searching,

    // Grupa/par/individual dosegao minimum, ali jos nije zvanicno pocelo - ceka da profesor
    // potvrdi termin(e) na svim slotovima (3 za Pair/Group, 1 za Individual)
    Ready,

    // Kurs je u toku - za sve tipove tek kad se Group pokrene (Group.Start), ukljucujuci Individual
    Active,

    Completed,
    Cancelled,

    // Privremeno zamrznut, odobreno od admina/profesora - vraca se u Active preko Unfreeze
    Frozen
}