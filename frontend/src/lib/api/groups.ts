import { apiRequest } from "./http";

export type DayOfWeek =
    | "Monday"
    | "Tuesday"
    | "Wednesday"
    | "Thursday"
    | "Friday"
    | "Saturday"
    | "Sunday";

export type GroupType = "Individual" | "Pair" | "Group";
export type GroupStatus = "Forming" | "Ready" | "Active" | "Completed" | "Dissolved";
export type EnrollmentStatus = "Searching" | "Ready" | "Active" | "Completed" | "Cancelled" | "Frozen";

export type Slot = {
    slotIndex: number;
    dayOfWeek: DayOfWeek | null;
    /** "HH:mm:ss" */
    time: string | null;
    professorId: string | null;
};

export type GroupSummary = {
    id: string;
    courseId: string;
    type: GroupType;
    status: GroupStatus;
    memberCount: number;
    minSize: number;
    maxSize: number;
    formationWindowEndsAt: string | null;
    slots: Slot[];
};

export type GroupMember = {
    enrollmentId: string;
    userId: string;
    fullName: string | null;
    status: EnrollmentStatus;
};

export type GroupDetail = Omit<GroupSummary, "memberCount"> & {
    startedAt: string | null;
    members: GroupMember[];
};

export type GroupFilters = {
    courseId?: string;
    status?: GroupStatus;
    type?: GroupType;
    professorId?: string;
};

export type CreateGroupRequest = {
    courseId: string;
    /** Tacno 3 termina, svaki sa razlicitim danom i vremenom. */
    slots: { dayOfWeek: DayOfWeek; time: string; professorId: string }[];
    formationWindowDays?: number;
};

/** Admin i profesor. */
export function getGroups(filters: GroupFilters = {}) {
    const params = new URLSearchParams(
        Object.entries(filters).filter((entry): entry is [string, string] => Boolean(entry[1]))
    );
    const query = params.toString() ? `?${params}` : "";

    return apiRequest<GroupSummary[]>("groups", `/groups${query}`, { auth: true });
}

/** Admin i profesor. */
export function getGroup(groupId: string) {
    return apiRequest<GroupDetail>("groups", `/groups/${groupId}`, { auth: true });
}

export function createGroup(request: CreateGroupRequest) {
    return apiRequest<{ groupId: string }>("groups", "/groups", {
        method: "POST",
        body: request,
        auth: true,
    });
}

export function addGroupMember(groupId: string, userId: string) {
    return apiRequest<{ enrollmentId: string; groupId: string }>("groups", `/groups/${groupId}/members`, {
        method: "POST",
        body: { userId },
        auth: true,
    });
}

/** force: pokreni i ako grupa nema minimalan broj polaznika. */
export function startGroup(groupId: string, force = false) {
    return apiRequest<null>("groups", `/groups/${groupId}/start`, {
        method: "POST",
        body: { force },
        auth: true,
    });
}

/** Samo admin. Rucno zatvaranje grupe, tesko se vraca. */
export function completeGroup(groupId: string) {
    return apiRequest<null>("groups", `/groups/${groupId}/complete`, {
        method: "POST",
        auth: true,
    });
}

/** Osoblje. Zamrznut upis nema pristup kursu, a otvaranje lekcija staje. */
export function freezeEnrollment(enrollmentId: string) {
    return apiRequest<null>("groups", `/enrollments/${enrollmentId}/freeze`, {
        method: "POST",
        auth: true,
    });
}

export function unfreezeEnrollment(enrollmentId: string) {
    return apiRequest<null>("groups", `/enrollments/${enrollmentId}/unfreeze`, {
        method: "POST",
        auth: true,
    });
}

/** Ucenik (svoj upis) ili osoblje (bilo ciji). Ne moze da se ponisti. */
export function cancelEnrollment(enrollmentId: string) {
    return apiRequest<null>("groups", `/enrollments/${enrollmentId}/cancel`, {
        method: "POST",
        auth: true,
    });
}
