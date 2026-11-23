"use client";

import { ApiError } from "../lib/api/http";

/** codeMessages: nas tekst po kodu greske iz backend-a (ima prednost nad porukom servisa). */
export function errorText(error: unknown, serviceName: string, codeMessages?: Record<string, string>) {
    if (error instanceof ApiError) {
        if (error.status === 0) return `${serviceName} trenutno nije dostupan. Proveri da li je pokrenut.`;
        if (error.status === 403) return "Nemaš dozvolu za ovu akciju.";
        if (error.code && codeMessages?.[error.code]) return codeMessages[error.code];
        return error.message;
    }

    return "Došlo je do greške. Pokušaj ponovo.";
}

export function LoadingState({ text = "Učitavanje..." }: { text?: string }) {
    return <div className="rounded-2xl bg-white p-6 text-sm text-zinc-500">{text}</div>;
}

export function ErrorState({
    error,
    serviceName,
    onRetry,
}: {
    error: unknown;
    serviceName: string;
    onRetry?: () => void;
}) {
    return (
        <div role="alert" className="rounded-2xl border border-red-200 bg-red-50 p-5 text-sm text-red-700">
            <p>{errorText(error, serviceName)}</p>
            {onRetry && (
                <button
                    type="button"
                    onClick={onRetry}
                    className="mt-3 rounded-full bg-white px-4 py-2 font-medium text-red-700 shadow-sm transition hover:bg-red-100"
                >
                    Pokušaj ponovo
                </button>
            )}
        </div>
    );
}

export function EmptyState({ children }: { children: React.ReactNode }) {
    return (
        <div className="rounded-2xl border border-dashed border-zinc-300 bg-white p-8 text-center text-sm text-zinc-500">
            {children}
        </div>
    );
}
