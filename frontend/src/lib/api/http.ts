// Zajednicki poziv ka backend servisima: adresa, token, citanje gresaka.
// Svi novi pozivi (placanja, moji kursevi, lekcije, progres) idu preko apiRequest.

import { API_URLS, type ApiService } from "./config";
import { clearTokens, getAccessToken } from "../auth/tokens";

export class ApiError extends Error {
    /** HTTP status; 0 znaci da server nije dostupan. */
    readonly status: number;
    /** Kod greske sa backend-a, npr. "INVALID_PAYMENT". */
    readonly code?: string;
    /** Ceo odgovor servera, ako treba nesto posebno da se procita. */
    readonly data: unknown;

    constructor(message: string, status: number, data: unknown = null, code?: string) {
        super(message);
        this.name = "ApiError";
        this.status = status;
        this.data = data;
        this.code = code;
    }
}

type ApiRequestOptions = Omit<RequestInit, "body" | "headers"> & {
    /** Salje se kao JSON. */
    body?: unknown;
    headers?: Record<string, string>;
    /** Dodaje "Authorization: Bearer <token>". */
    auth?: boolean;
};

const NETWORK_ERROR_MESSAGE = "Greška pri povezivanju sa serverom. Pokušaj ponovo.";
const DEFAULT_ERROR_MESSAGE = "Došlo je do greške. Pokušaj ponovo.";
const NOT_LOGGED_IN_MESSAGE = "Potrebno je da se prijaviš.";

// Backend vraca { error, message }, a ASP.NET validacija { title, errors }.
function getErrorMessage(data: unknown): string | undefined {
    if (!data || typeof data !== "object") {
        return undefined;
    }

    const body = data as { message?: unknown; errors?: unknown; title?: unknown };

    if (typeof body.message === "string" && body.message) {
        return body.message;
    }

    if (body.errors && typeof body.errors === "object") {
        const messages = Object.values(body.errors as Record<string, unknown>)
            .flat()
            .filter((message): message is string => typeof message === "string");

        if (messages.length > 0) {
            return messages.join(" ");
        }
    }

    if (typeof body.title === "string" && body.title) {
        return body.title;
    }

    return undefined;
}

function getErrorCode(data: unknown): string | undefined {
    if (data && typeof data === "object") {
        const error = (data as { error?: unknown }).error;

        if (typeof error === "string") {
            return error;
        }
    }

    return undefined;
}

async function parseBody(response: Response): Promise<unknown> {
    const text = await response.text();

    if (!text) {
        return null;
    }

    try {
        return JSON.parse(text);
    } catch {
        return text;
    }
}

export async function apiRequest<T>(
    service: ApiService,
    path: string,
    options: ApiRequestOptions = {}
): Promise<T> {
    const { body, headers = {}, auth = false, ...init } = options;
    const baseUrl = API_URLS[service];

    if (!baseUrl) {
        throw new ApiError(`Adresa servisa "${service}" nije podešena.`, 0);
    }

    const requestHeaders: Record<string, string> = { ...headers };

    if (body !== undefined) {
        requestHeaders["Content-Type"] = "application/json";
    }

    if (auth) {
        const token = getAccessToken();

        if (!token) {
            throw new ApiError(NOT_LOGGED_IN_MESSAGE, 401);
        }

        requestHeaders.Authorization = `Bearer ${token}`;
    }

    let response: Response;

    try {
        response = await fetch(`${baseUrl}${path}`, {
            ...init,
            headers: requestHeaders,
            body: body === undefined ? undefined : JSON.stringify(body),
        });
    } catch {
        throw new ApiError(NETWORK_ERROR_MESSAGE, 0);
    }

    const data = await parseBody(response);

    if (!response.ok) {
        // Token vise ne vazi: odjavi korisnika svuda na sajtu.
        if (response.status === 401 && auth) {
            clearTokens();
        }

        throw new ApiError(
            getErrorMessage(data) ?? DEFAULT_ERROR_MESSAGE,
            response.status,
            data,
            getErrorCode(data)
        );
    }

    return data as T;
}
