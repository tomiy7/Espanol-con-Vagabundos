import { apiRequest } from "./http";

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

export function getMe() {
    return apiRequest<MeResponse>("auth", "/auth/me", { auth: true });
}
