"use client";

// Prijavljeni korisnik za ceo sajt. Token se cita iz jednog zajednickog izvora
// (localStorage), pa Header, progres i stranice uvek vide isto stanje.
// Istekao token se tretira kao odjava.

import { useCallback, useEffect, useMemo, useSyncExternalStore } from "react";
import {
    ACCESS_TOKEN_KEY,
    AUTH_CHANGE_EVENT,
    clearTokens,
    decodeJwtPayload,
    getAccessToken,
    getTokenExpiresAt,
    readStoredAccessToken,
    type JwtPayload,
} from "../lib/auth/tokens";

export type AuthUser = {
    id?: string;
    username?: string;
    firstName?: string;
    lastName?: string;
    gender?: string;
    email?: string;
    role?: string;
};

const CLAIM = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/";
const ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

// setTimeout ne prihvata vise od ~24 dana.
const MAX_TIMEOUT_MS = 2_147_483_647;

function pick(payload: JwtPayload, ...keys: string[]): string | undefined {
    for (const key of keys) {
        const value = payload[key];

        if (typeof value === "string" && value) {
            return value;
        }
    }

    return undefined;
}

// Ista polja kao i ranije: id mora ostati isti jer se po njemu cuva progres.
function toUser(payload: JwtPayload): AuthUser {
    return {
        id: pick(payload, "sub", "userId", "user_id", "id", `${CLAIM}nameidentifier`),
        username: pick(payload, "username", "unique_name", "preferred_username", "email", `${CLAIM}name`),
        firstName: pick(payload, "firstName", "firstname", "given_name", `${CLAIM}givenname`),
        lastName: pick(payload, "lastName", "lastname", "family_name", `${CLAIM}surname`),
        gender: pick(payload, "gender", "Gender", `${CLAIM}gender`),
        email: pick(payload, "email", `${CLAIM}emailaddress`),
        role: pick(payload, "role", ROLE_CLAIM),
    };
}

function subscribe(onChange: () => void) {
    function handleStorage(event: StorageEvent) {
        // Prijava/odjava u drugom tabu
        if (event.key === null || event.key === ACCESS_TOKEN_KEY) {
            onChange();
        }
    }

    window.addEventListener(AUTH_CHANGE_EVENT, onChange);
    window.addEventListener("storage", handleStorage);

    return () => {
        window.removeEventListener(AUTH_CHANGE_EVENT, onChange);
        window.removeEventListener("storage", handleStorage);
    };
}

function getServerToken() {
    return null;
}

function subscribeNoop() {
    return () => {};
}

export function useAuth() {
    const token = useSyncExternalStore(subscribe, getAccessToken, getServerToken);

    // Na serveru i pri prvom renderu ne znamo da li je korisnik prijavljen.
    const hydrated = useSyncExternalStore(subscribeNoop, () => true, () => false);

    const user = useMemo(() => {
        if (!token) {
            return null;
        }

        const payload = decodeJwtPayload(token);

        return payload ? toUser(payload) : null;
    }, [token]);

    // Kad token istekne, obrisi ga (i odjavi korisnika na celom sajtu).
    useEffect(() => {
        const stored = readStoredAccessToken();

        if (!stored) {
            return;
        }

        const payload = decodeJwtPayload(stored);
        const expiresAt = payload ? getTokenExpiresAt(payload) : null;

        if (expiresAt === null) {
            return;
        }

        const msLeft = expiresAt - Date.now();

        if (msLeft <= 0) {
            clearTokens();
            return;
        }

        if (msLeft > MAX_TIMEOUT_MS) {
            return;
        }

        const timer = window.setTimeout(clearTokens, msLeft);

        return () => window.clearTimeout(timer);
    }, [token]);

    const logout = useCallback(() => {
        clearTokens();
    }, []);

    return {
        user,
        isAuthenticated: !!user,
        loading: !hydrated,
        logout,
    };
}
