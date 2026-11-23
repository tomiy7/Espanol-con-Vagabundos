"use client";

import { useState } from "react";
import { useAsync } from "../../../src/hooks/useAsync";
import { useAuth } from "../../../src/hooks/useAuth";
import { formatDate } from "../../../src/features/groups/labels";
import { EmptyState, ErrorState, LoadingState, errorText } from "../../../src/components/PanelState";
import {
    changeUserRole,
    getAllUsers,
    getUserDisplayName,
    type Role,
    type UserDetail,
} from "../../../src/lib/api/auth";

type Notice = { type: "success" | "error"; text: string };
type RoleFilter = Role | "all";

const ROLE_LABELS: Record<Role, string> = {
    student: "Student",
    professor: "Profesor",
    admin: "Admin",
};

const ROLE_STYLES: Record<Role, string> = {
    student: "bg-zinc-100 text-zinc-700",
    professor: "bg-purple-50 text-purple-700",
    admin: "bg-amber-50 text-amber-800",
};

const FILTERS: { value: RoleFilter; label: string }[] = [
    { value: "all", label: "Svi" },
    { value: "student", label: "Studenti" },
    { value: "professor", label: "Profesori" },
    { value: "admin", label: "Admini" },
];

const AUTH_ERROR_MESSAGES: Record<string, string> = {
    INVALID_ROLE: "Nepoznata uloga.",
    CANNOT_CHANGE_OWN_ROLE: "Ne možeš da promeniš sopstvenu ulogu.",
    USER_NOT_FOUND: "Korisnik nije pronađen.",
};

function roleLabel(role: string) {
    return ROLE_LABELS[role as Role] ?? "Nepoznato";
}

function matches(user: UserDetail, query: string) {
    if (!query) return true;

    return [getUserDisplayName(user), user.username, user.email, user.phone]
        .filter(Boolean)
        .some((value) => value!.toLowerCase().includes(query));
}

export default function AdminUsersPage() {
    const users = useAsync(getAllUsers);
    const { user: currentUser } = useAuth();

    const [filter, setFilter] = useState<RoleFilter>("all");
    const [search, setSearch] = useState("");
    const [busyId, setBusyId] = useState<string | null>(null);
    const [notice, setNotice] = useState<Notice | null>(null);

    const all = users.data ?? [];
    const query = search.trim().toLowerCase();
    const visible = all
        .filter((user) => filter === "all" || user.role === filter)
        .filter((user) => matches(user, query))
        .sort((a, b) => getUserDisplayName(a).localeCompare(getUserDisplayName(b), "sr"));

    function count(value: RoleFilter) {
        return value === "all" ? all.length : all.filter((user) => user.role === value).length;
    }

    async function handleRoleChange(user: UserDetail, role: Role) {
        if (role === user.role) return;

        const name = getUserDisplayName(user);
        const question =
            role === "admin"
                ? `Dati ${name} admin ulogu? Admin vidi sve korisnike i uplate i može da menja uloge.`
                : `Promeniti ulogu za ${name}: ${roleLabel(user.role)} → ${ROLE_LABELS[role]}?`;

        if (!window.confirm(question)) return;

        setBusyId(user.id);
        setNotice(null);

        try {
            await changeUserRole(user.id, role);
            setNotice({
                type: "success",
                text: `${name} je sada ${ROLE_LABELS[role].toLowerCase()}. Promena važi kad se ponovo prijavi.`,
            });
            users.reload();
        } catch (error) {
            setNotice({ type: "error", text: errorText(error, "Servis za naloge", AUTH_ERROR_MESSAGES) });
        } finally {
            setBusyId(null);
        }
    }

    return (
        <section aria-labelledby="users-heading">
            <div className="mb-6">
                <h2 id="users-heading" className="text-2xl font-bold text-zinc-900">
                    Korisnici
                </h2>
                <p className="mt-1 text-sm text-zinc-600">
                    Svi nalozi na sajtu. Ovde možeš da nekome daš ulogu profesora ili admina.
                </p>
            </div>

            {notice && (
                <p
                    role="status"
                    className={`mb-5 rounded-2xl px-4 py-3 text-sm ${
                        notice.type === "success"
                            ? "border border-emerald-200 bg-emerald-50 text-emerald-800"
                            : "border border-red-200 bg-red-50 text-red-700"
                    }`}
                >
                    {notice.text}
                </p>
            )}

            <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex flex-wrap gap-2" role="group" aria-label="Filter po ulozi">
                    {FILTERS.map((item) => (
                        <button
                            key={item.value}
                            type="button"
                            aria-pressed={filter === item.value}
                            onClick={() => setFilter(item.value)}
                            className={`rounded-full px-4 py-2 text-sm font-medium transition ${
                                filter === item.value
                                    ? "bg-zinc-900 text-white"
                                    : "border border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50"
                            }`}
                        >
                            {item.label}
                            {users.data && <span className="ml-1.5 opacity-60">{count(item.value)}</span>}
                        </button>
                    ))}
                </div>

                <input
                    type="search"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Pretraga po imenu, mejlu ili telefonu"
                    aria-label="Pretraga korisnika"
                    className="w-full rounded-xl border border-zinc-200 bg-white px-3 py-2.5 text-sm text-zinc-900 outline-none focus:border-purple-400 focus:ring-2 focus:ring-purple-100 sm:w-80"
                />
            </div>

            {users.loading && !users.data ? (
                <LoadingState text="Učitavanje korisnika..." />
            ) : users.error !== undefined && !users.data ? (
                <ErrorState error={users.error} serviceName="Servis za naloge" onRetry={users.reload} />
            ) : visible.length === 0 ? (
                <EmptyState>{all.length === 0 ? "Još nema korisnika." : "Nijedan korisnik ne odgovara pretrazi."}</EmptyState>
            ) : (
                <ul className="divide-y divide-zinc-100 rounded-3xl border border-zinc-200 bg-white">
                    {visible.map((user) => {
                        const isMe = user.id === currentUser?.id;

                        return (
                            <li key={user.id} className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
                                <div className="min-w-0">
                                    <p className="font-medium text-zinc-900">
                                        {getUserDisplayName(user)}
                                        <span className="font-normal text-zinc-500"> @{user.username}</span>
                                        {isMe && <span className="ml-2 text-xs font-semibold text-purple-600">(ti)</span>}
                                    </p>
                                    <p className="break-all text-sm text-zinc-500">
                                        {user.email} · {user.phone || "bez telefona"} · od {formatDate(user.createdAt)}
                                    </p>
                                </div>

                                <div className="flex shrink-0 items-center gap-2">
                                    <span
                                        className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                                            ROLE_STYLES[user.role as Role] ?? "bg-zinc-100 text-zinc-500"
                                        }`}
                                    >
                                        {roleLabel(user.role)}
                                    </span>
                                    <select
                                        aria-label={`Uloga za ${getUserDisplayName(user)}`}
                                        value={user.role}
                                        disabled={isMe || busyId !== null}
                                        title={isMe ? "Ne možeš da promeniš sopstvenu ulogu." : undefined}
                                        onChange={(e) => void handleRoleChange(user, e.target.value as Role)}
                                        className="rounded-xl border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-purple-400 focus:ring-2 focus:ring-purple-100 disabled:opacity-50"
                                    >
                                        {(Object.keys(ROLE_LABELS) as Role[]).map((role) => (
                                            <option key={role} value={role}>
                                                {ROLE_LABELS[role]}
                                            </option>
                                        ))}
                                    </select>
                                </div>
                            </li>
                        );
                    })}
                </ul>
            )}
        </section>
    );
}
