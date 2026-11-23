import type { DayOfWeek, EnrollmentStatus, GroupStatus, GroupType } from "../../lib/api/groups";
import { errorText } from "../../components/PanelState";

// Groups jos nije konacan: nepoznat status/tip prikazujemo kao "Nepoznato", ne pucamo.
const UNKNOWN = "Nepoznato";

export const DAYS: { value: DayOfWeek; label: string }[] = [
    { value: "Monday", label: "Ponedeljak" },
    { value: "Tuesday", label: "Utorak" },
    { value: "Wednesday", label: "Sreda" },
    { value: "Thursday", label: "Četvrtak" },
    { value: "Friday", label: "Petak" },
    { value: "Saturday", label: "Subota" },
    { value: "Sunday", label: "Nedelja" },
];

export function dayLabel(day: DayOfWeek | null) {
    return DAYS.find((d) => d.value === day)?.label ?? "—";
}

/** "18:00:00" -> "18:00" */
export function timeLabel(time: string | null) {
    return time ? time.slice(0, 5) : "—";
}

const GROUP_TYPE_LABELS: Record<GroupType, string> = {
    Individual: "Individualno",
    Pair: "U paru",
    Group: "Grupno",
};

const GROUP_STATUS_LABELS: Record<GroupStatus, string> = {
    Forming: "Formira se",
    Ready: "Spremna za početak",
    Active: "U toku",
    Completed: "Završena",
    Dissolved: "Rasformirana",
};

const GROUP_STATUS_STYLES: Record<GroupStatus, string> = {
    Forming: "bg-amber-50 text-amber-800",
    Ready: "bg-purple-50 text-purple-700",
    Active: "bg-emerald-50 text-emerald-700",
    Completed: "bg-zinc-100 text-zinc-600",
    Dissolved: "bg-zinc-100 text-zinc-500",
};

const ENROLLMENT_STATUS_LABELS: Record<EnrollmentStatus, string> = {
    Searching: "Čeka popunu",
    Ready: "Čeka početak",
    Active: "Aktivan",
    Completed: "Završio kurs",
    Cancelled: "Otkazan",
    Frozen: "Zamrznut",
};

export function groupTypeLabel(type: string) {
    return GROUP_TYPE_LABELS[type as GroupType] ?? UNKNOWN;
}

export function groupStatusLabel(status: string) {
    return GROUP_STATUS_LABELS[status as GroupStatus] ?? UNKNOWN;
}

export function groupStatusStyle(status: string) {
    return GROUP_STATUS_STYLES[status as GroupStatus] ?? "bg-zinc-100 text-zinc-500";
}

export function enrollmentStatusLabel(status: string) {
    return ENROLLMENT_STATUS_LABELS[status as EnrollmentStatus] ?? UNKNOWN;
}

// Groups vraca poruke na engleskom; prikazujemo nas tekst po kodu greske,
// a backend poruku samo kad kod ne znamo.
const GROUPS_ERROR_MESSAGES: Record<string, string> = {
    INVALID_ENROLLMENT: "Upis nije moguć.",
    INVALID_INVITATION: "Pozivnica nije važeća.",
    INVALID_COURSE_ID: "Kurs ne postoji.",
    CANNOT_CANCEL: "Ovaj upis ne može da se otkaže.",
    CANNOT_FREEZE: "Ovaj upis ne može da se zamrzne.",
    CANNOT_UNFREEZE: "Ovaj upis ne može da se odmrzne.",
    CANNOT_CREATE_GROUP: "Grupa ne može da se napravi. Proveri termine i profesore.",
    CANNOT_ADD_MEMBER: "Polaznik ne može da se doda: grupa je možda popunjena ili je polaznik već upisan na ovaj kurs.",
    CANNOT_ASSIGN: "Termin ne može da se dodeli.",
    CANNOT_START: "Grupa ne može da se pokrene.",
    CANNOT_COMPLETE: "Grupa ne može da se završi.",
    ENROLLMENT_NOT_FOUND: "Upis nije pronađen.",
    GROUP_NOT_FOUND: "Grupa nije pronađena.",
    INVITATION_NOT_FOUND: "Pozivnica nije pronađena.",
    CONFLICT: "Podaci su se u međuvremenu promenili. Osveži stranicu i pokušaj ponovo.",
};

export function groupsErrorText(error: unknown) {
    return errorText(error, "Servis za grupe", GROUPS_ERROR_MESSAGES);
}

export function formatDate(value: string | null) {
    if (!value) return "—";

    return new Date(value).toLocaleDateString("sr-RS", {
        day: "numeric",
        month: "numeric",
        year: "numeric",
    });
}

export function formatDateTime(value: string) {
    return new Date(value).toLocaleString("sr-RS", {
        day: "numeric",
        month: "numeric",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
    });
}
