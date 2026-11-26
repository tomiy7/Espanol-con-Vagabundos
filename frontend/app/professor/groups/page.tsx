"use client";

import Link from "next/link";
import { useAsync } from "../../../src/hooks/useAsync";
import { useAuth } from "../../../src/hooks/useAuth";
import { EmptyState, ErrorState, LoadingState } from "../../../src/components/PanelState";
import {
    dayLabel,
    formatDate,
    groupStatusLabel,
    groupStatusStyle,
    groupTypeLabel,
    timeLabel,
} from "../../../src/features/groups/labels";
import { getCourseOptions } from "../../../src/lib/api/courses";
import { getGroups, type GroupSummary } from "../../../src/lib/api/groups";

// Zavrsene i rasformirane grupe idu na kraj liste.
const STATUS_ORDER = ["Active", "Ready", "Forming", "Completed", "Dissolved"];

function byStatus(a: GroupSummary, b: GroupSummary) {
    const rank = (status: string) => {
        const index = STATUS_ORDER.indexOf(status);
        return index === -1 ? STATUS_ORDER.length : index;
    };

    return rank(a.status) - rank(b.status);
}

export default function ProfessorGroupsPage() {
    const { user } = useAuth();
    const professorId = user?.id ?? "";

    const groups = useAsync(
        () => (professorId ? getGroups({ professorId }) : Promise.resolve([])),
        professorId
    );
    // Nazivi kurseva su dodatak: ako Directus ne odgovori, lista grupa i dalje radi.
    const courses = useAsync(getCourseOptions);

    function courseName(courseId: string) {
        if (!courses.data) return courses.loading ? "..." : "Kurs";
        return courses.data.find((course) => course.id === courseId)?.name ?? "Nepoznat kurs";
    }

    const list = [...(groups.data ?? [])].sort(byStatus);

    return (
        <section aria-labelledby="professor-groups-heading">
            <div className="mb-6">
                <h2 id="professor-groups-heading" className="text-2xl font-bold text-zinc-900">
                    Moje grupe
                </h2>
                <p className="mt-1 text-sm text-zinc-600">
                    Grupe u kojima držiš bar jedan termin. Otvori grupu da vidiš polaznike i njihove kontakte.
                </p>
            </div>

            {groups.loading ? (
                <LoadingState text="Učitavanje grupa..." />
            ) : groups.error !== undefined ? (
                <ErrorState error={groups.error} serviceName="Servis za grupe" onRetry={groups.reload} />
            ) : list.length === 0 ? (
                <EmptyState>Još nemaš dodeljenih grupa. Kad te admin doda na termin, grupa će se pojaviti ovde.</EmptyState>
            ) : (
                <ul className="grid gap-4 md:grid-cols-2">
                    {list.map((group) => (
                        <li key={group.id}>
                            <Link
                                href={`/professor/groups/${group.id}`}
                                className="group flex h-full flex-col rounded-2xl border border-zinc-200 bg-white p-5 transition hover:border-purple-300 hover:shadow-sm"
                            >
                                <div className="flex items-start justify-between gap-3">
                                    <div className="min-w-0">
                                        <p className="text-xs font-semibold uppercase tracking-wider text-purple-600">
                                            {groupTypeLabel(group.type)}
                                        </p>
                                        <p className="mt-1 font-semibold text-zinc-900 group-hover:text-purple-700">
                                            {courseName(group.courseId)}
                                        </p>
                                    </div>
                                    <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${groupStatusStyle(group.status)}`}>
                                        {groupStatusLabel(group.status)}
                                    </span>
                                </div>

                                <ul className="mt-4 space-y-1 text-sm">
                                    {group.slots.map((slot) => {
                                        const mine = slot.professorId === professorId;

                                        return (
                                            <li key={slot.slotIndex} className={mine ? "font-medium text-zinc-900" : "text-zinc-500"}>
                                                {dayLabel(slot.dayOfWeek)} {timeLabel(slot.time)}
                                                {mine && <span className="ml-2 text-xs text-purple-600">tvoj termin</span>}
                                            </li>
                                        );
                                    })}
                                </ul>

                                <div className="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-zinc-100 pt-4 text-sm">
                                    <span className="font-medium text-zinc-800">
                                        {group.memberCount} / {group.maxSize} polaznika
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
