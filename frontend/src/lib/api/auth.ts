import { apiRequest } from "./http";

export type Role = "admin" | "professor" | "student";

export type MeResponse = {
    id: string;
    username: string;
    email: string;
    phone?: string;
    role: string;
    firstName?: string;
    lastName?: string;
    gender?: string;
    createdAt: string;
};

export type UserDetail = MeResponse;

export function getMe() {
    return apiRequest<MeResponse>("auth", "/auth/me", { auth: true });
}

/** Samo admin. */
export function getAllUsers() {
    return apiRequest<UserDetail[]>("auth", "/auth/admin/users", { auth: true });
}

export function getUserDisplayName(user: Pick<UserDetail, "firstName" | "lastName" | "username">) {
    return [user.firstName, user.lastName].filter(Boolean).join(" ") || user.username;
}

/** Samo admin. Nova uloga vazi tek kad se korisnik ponovo prijavi (ili mu se token osvezi). */
export function changeUserRole(userId: string, role: Role) {
    return apiRequest<UserDetail>("auth", `/auth/admin/users/${userId}/role`, {
        method: "PATCH",
        body: { role },
        auth: true,
    });
}
