"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useAsync } from "../../../../src/hooks/useAsync";
import { useAuth } from "../../../../src/hooks/useAuth";
import { ErrorState, LoadingState } from "../../../../src/components/PanelState";
import {
    dayLabel,
    enrollmentStatusLabel,
    formatDate,
    groupStatusLabel,
    groupStatusStyle,
    groupTypeLabel,
    timeLabel,
} from "../../../../src/features/groups/labels";
import { getUserById, getUserDisplayName, type UserDetail } from "../../../../src/lib/api/auth";
import { getCourseOptions } from "../../../../src/lib/api/courses";
import { getGroup } from "../../../../src/lib/api/groups";

/** Kontakti polaznika iz Auth-a; ako neki ne stigne, ostali se i dalje prikazuju. */
async function loadContacts(userIds: string[]) {
    const results = await Promise.allSettled(userIds.map((id) => getUserById(id)));
    const contacts = new Map<string, UserDetail>();

    results.forEach((result) => {
        if (result.status === "fulfilled") contacts.set(result.value.id, result.value);
    });

    return contacts;
}

export default function ProfessorGroupPage() {
    const { groupId } = useParams<{ groupId: string }>();
    const { user } = useAuth();

    const group = useAsync(() => getGroup(groupId), groupId);
    const courses = useAsync(getCourseOptions);

    const data = group.data;
    // Otkazani polaznici nisu vise u grupi; prikazujemo samo ostale.
    const members = (data?.members ?? []).filter((member) => member.status !== "Cancelled");
    const memberIds = members.map((member) => member.userId);
    const contacts = useAsync(() => loadContacts(memberIds), memberIds.join(","));

    const courseName = data
        ? courses.data?.find((course) => course.id === data.courseId)?.name ?? (courses.loading ? "..." : "Kurs")
        : "";

    return (
        <section>
            <Link href="/professor/groups" className="mb-5 inline-block text-sm text-zinc-500 hover:text-zinc-900">
                ← Moje grupe
            </Link>

            {group.loading && !data ? (
                <LoadingState text="Učitavanje grupe..." />
            ) : group.error !== undefined && !data ? (
                <ErrorState error={group.error} serviceName="Servis za grupe" onRetry={group.reload} />
            ) : data ? (
                <div className="flex flex-col gap-6">
                    <div className="rounded-3xl border border-zinc-200 bg-white p-6 sm:p-8">
                        <div className="flex flex-wrap items-start justify-between gap-4">
                            <div>
                                <p className="text-xs font-semibold uppercase tracking-wider text-purple-600">
                                    {groupTypeLabel(data.type)}
                                </p>
                                <h2 className="mt-1 text-2xl font-bold text-zinc-900">{courseName}</h2>
                            </div>
                            <span className={`rounded-full px-3 py-1 text-sm font-semibold ${groupStatusStyle(data.status)}`}>
                                {groupStatusLabel(data.status)}
                            </span>
                        </div>

                        <dl className="mt-6 grid gap-3 text-sm sm:grid-cols-2">
                            <div className="rounded-2xl bg-[#fbfaff] p-4">
                                <dt className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Polaznika</dt>
                                <dd className="mt-1 font-medium text-zinc-800">
                                    {members.length} / {data.maxSize}
                                </dd>
                            </div>
                            <div className="rounded-2xl bg-[#fbfaff] p-4">
                                <dt className="text-xs font-semibold uppercase tracking-wider text-zinc-400">
                                    {data.startedAt ? "Počela" : "Prijave do"}
                                </dt>
                                <dd className="mt-1 font-medium text-zinc-800">
                                    {formatDate(data.startedAt ?? data.formationWindowEndsAt)}
                                </dd>
                            </div>
                        </dl>

                        <h3 className="mt-6 text-sm font-semibold text-zinc-900">Termini</h3>
                        <ul className="mt-2 space-y-1 text-sm">
                            {data.slots.map((slot) => {
                                const mine = slot.professorId === user?.id;

                                return (
                                    <li key={slot.slotIndex} className={mine ? "font-medium text-zinc-900" : "text-zinc-500"}>
                                        {dayLabel(slot.dayOfWeek)} {timeLabel(slot.time)}
                                        {mine && <span className="ml-2 text-xs text-purple-600">tvoj termin</span>}
                                    </li>
                                );
                            })}
                        </ul>
                    </div>

                    <div className="rounded-3xl border border-zinc-200 bg-white p-6 sm:p-8">
                        <h3 className="text-lg font-semibold text-zinc-900">Polaznici</h3>

                        {!contacts.loading && members.length > 0 && contacts.data?.size === 0 && (
                            <p className="mt-3 text-sm text-amber-700">Kontakti polaznika trenutno nisu dostupni.</p>
                        )}

                        {members.length === 0 ? (
                            <p className="mt-3 text-sm text-zinc-500">U grupi još nema polaznika.</p>
                        ) : (
                            <ul className="mt-4 divide-y divide-zinc-100">
                                {members.map((member) => {
                                    const contact = contacts.data?.get(member.userId);
                                    const name = contact
                                        ? getUserDisplayName(contact)
                                        : member.fullName || (contacts.loading ? "Učitavanje..." : "Nepoznat polaznik");

                                    return (
                                        <li key={member.enrollmentId} className="flex flex-wrap items-center justify-between gap-2 py-3">
                                            <div className="min-w-0">
                                                <p className="font-medium text-zinc-900">{name}</p>
                                                {contact && (
                                                    <p className="break-all text-sm text-zinc-500">
                                                        {contact.phone ? (
                                                            <a href={`tel:${contact.phone}`} className="text-purple-700 hover:underline">
                                                                {contact.phone}
                                                            </a>
                                                        ) : (
                                                            "bez telefona"
                                                        )}
                                                        {" · "}
                                                        <a href={`mailto:${contact.email}`} className="hover:underline">
                                                            {contact.email}
                                                        </a>
                                                    </p>
                                                )}
                                            </div>
                                            <span className="rounded-full bg-zinc-100 px-2.5 py-1 text-xs font-semibold text-zinc-600">
                                                {enrollmentStatusLabel(member.status)}
                                            </span>
                                        </li>
                                    );
                                })}
                            </ul>
                        )}
                    </div>
                </div>
            ) : null}
        </section>
    );
}
