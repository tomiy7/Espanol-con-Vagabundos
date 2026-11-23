"use client";

import Link from "next/link";
import { useState } from "react";
import CreateGroupForm from "./CreateGroupForm";
import { useAsync } from "../../../src/hooks/useAsync";
import { useDirectory } from "../../../src/features/admin/useDirectory";
import { EmptyState, ErrorState, LoadingState, errorText } from "../../../src/components/PanelState";
import {
    dayLabel,
    formatDate,
    groupStatusLabel,
    groupStatusStyle,
    groupTypeLabel,
    timeLabel,
} from "../../../src/features/groups/labels";
import { getGroups } from "../../../src/lib/api/groups";

export default function AdminGroupsPage() {
    const groups = useAsync(() => getGroups());
    const directory = useDirectory();
    const [showForm, setShowForm] = useState(false);
    const [created, setCreated] = useState(false);

    const professors = (directory.data?.users ?? []).filter((user) => user.role === "professor");

    return (
        <section aria-labelledby="groups-heading">
            <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
                <div>
                    <h2 id="groups-heading" className="text-2xl font-bold text-zinc-900">
                        Grupe
                    </h2>
                    <p className="mt-1 text-sm text-zinc-600">
                        Otvori novu grupu, dodaj polaznike i pokreni grupu kad je popunjena.
                    </p>
                </div>

                {!showForm && (
                    <button
                        type="button"
                        disabled={!directory.data}
                        onClick={() => {
                            setShowForm(true);
                            setCreated(false);
                        }}
                        className="rounded-full bg-zinc-900 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-zinc-700 disabled:opacity-50"
                    >
                        + Nova grupa
                    </button>
                )}
            </div>

            {directory.error !== undefined && !directory.loading && (
                <p className="mb-5 rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
                    Korisnici i kursevi nisu učitani ({errorText(directory.error, "Servis")}). Nova grupa ne može da se napravi dok se ne učitaju.
                </p>
            )}

            {created && (
                <p role="status" className="mb-5 rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                    Grupa je napravljena.
                </p>
            )}

            {showForm && directory.data && (
                <CreateGroupForm
                    courses={directory.data.courses}
                    professors={professors}
                    onCancel={() => setShowForm(false)}
                    onCreated={() => {
                        setShowForm(false);
                        setCreated(true);
                        groups.reload();
                    }}
                />
            )}

            {groups.loading ? (
                <LoadingState text="Učitavanje grupa..." />
            ) : groups.error !== undefined ? (
                <ErrorState error={groups.error} serviceName="Servis za grupe" onRetry={groups.reload} />
            ) : (groups.data ?? []).length === 0 ? (
                <EmptyState>Još nema grupa. Napravi prvu preko dugmeta „Nova grupa”.</EmptyState>
            ) : (
                <ul className="grid gap-4 md:grid-cols-2">
                    {(groups.data ?? []).map((group) => (
                        <li key={group.id}>
                            <Link
                                href={`/admin/groups/${group.id}`}
                                className="group flex h-full flex-col rounded-2xl border border-zinc-200 bg-white p-5 transition hover:border-purple-300 hover:shadow-sm"
                            >
                                <div className="flex items-start justify-between gap-3">
                                    <div className="min-w-0">
                                        <p className="text-xs font-semibold uppercase tracking-wider text-purple-600">
                                            {groupTypeLabel(group.type)}
                                        </p>
                                        <p className="mt-1 font-semibold text-zinc-900 group-hover:text-purple-700">
                                            {directory.data?.courseName(group.courseId) ?? "..."}
                                        </p>
                                    </div>
                                    <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${groupStatusStyle(group.status)}`}>
                                        {groupStatusLabel(group.status)}
                                    </span>
                                </div>

                                <ul className="mt-4 space-y-1 text-sm text-zinc-700">
                                    {group.slots.map((slot) => (
                                        <li key={slot.slotIndex}>
                                            {dayLabel(slot.dayOfWeek)} {timeLabel(slot.time)}
                                            <span className="text-zinc-500">
                                                {" · "}
                                                {slot.professorId ? directory.data?.userName(slot.professorId) ?? "..." : "bez profesora"}
                                            </span>
                                        </li>
                                    ))}
                                </ul>

                                <div className="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-zinc-100 pt-4 text-sm">
                                    <span className="font-medium text-zinc-800">
                                        {group.memberCount} / {group.maxSize} polaznika
                                        <span className="font-normal text-zinc-500"> (min {group.minSize})</span>
                                    </span>
                                    {group.status === "Forming" && group.formationWindowEndsAt && (
                                        <span className="text-xs text-zinc-500">
                                            Prijave do {formatDate(group.formationWindowEndsAt)}
                                        </span>
                                    )}
                                </div>
                            </Link>
                        </li>
                    ))}
                </ul>
            )}
        </section>
    );
}
