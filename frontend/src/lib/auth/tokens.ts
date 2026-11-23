// Jedino mesto koje zna kako se cuvaju tokeni (localStorage) i kako se cita JWT.
// Kljucevi i "auth-change" event su isti kao ranije, pa login/register rade bez izmena.

export const ACCESS_TOKEN_KEY = "accessToken";
export const REFRESH_TOKEN_KEY = "refreshToken";
export const AUTH_CHANGE_EVENT = "auth-change";

export type JwtPayload = Record<string, unknown> & { exp?: number };

let cachedToken: string | null = null;
let cachedPayload: JwtPayload | null = null;

export function decodeJwtPayload(token: string): JwtPayload | null {
    if (token === cachedToken) {
        return cachedPayload;
    }

    let payload: JwtPayload | null = null;

    try {
        const base64 = token
            .split(".")[1]
            .replace(/-/g, "+")
            .replace(/_/g, "/");

        const json = decodeURIComponent(
            window
                .atob(base64)
                .split("")
                .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
                .join("")
        );

        const parsed: unknown = JSON.parse(json);

        if (typeof parsed === "object" && parsed !== null) {
            payload = parsed as JwtPayload;
        }
    } catch {
        payload = null;
    }

    cachedToken = token;
    cachedPayload = payload;

    return payload;
}

export function getTokenExpiresAt(payload: JwtPayload): number | null {
    return typeof payload.exp === "number" ? payload.exp * 1000 : null;
}

export function readStoredAccessToken(): string | null {
    if (typeof window === "undefined") {
        return null;
    }

    try {
        return window.localStorage.getItem(ACCESS_TOKEN_KEY);
    } catch {
        return null;
    }
}

/** Vraca token samo ako je ispravan i nije istekao. */
export function getAccessToken(): string | null {
    const token = readStoredAccessToken();

    if (!token) {
        return null;
    }

    const payload = decodeJwtPayload(token);

    if (!payload) {
        return null;
    }

    const expiresAt = getTokenExpiresAt(payload);

    if (expiresAt !== null && expiresAt <= Date.now()) {
        return null;
    }

    return token;
}

export function setTokens(accessToken: string, refreshToken: string) {
    window.localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    window.localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    window.dispatchEvent(new Event(AUTH_CHANGE_EVENT));
}

export function clearTokens() {
    if (typeof window === "undefined") {
        return;
    }

    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    window.localStorage.removeItem(REFRESH_TOKEN_KEY);
    window.dispatchEvent(new Event(AUTH_CHANGE_EVENT));
}
