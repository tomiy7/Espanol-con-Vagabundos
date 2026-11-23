"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useAsync } from "../../../../src/hooks/useAsync";
import { useDirectory } from "../../../../src/features/admin/useDirectory";
import { ErrorState, LoadingState } from "../../../../src/components/PanelState";
import {
    dayLabel,
    enrollmentStatusLabel,
    formatDate,
    groupStatusLabel,
    groupStatusStyle,
    groupTypeLabel,
    groupsErrorText,
    timeLabel,
} from "../../../../src/features/groups/labels";
import { getUserDisplayName } from "../../../../src/lib/api/auth";
import {
    addGroupMember,
    cancelEnrollment,
    completeGroup,
    freezeEnrollment,
    getGroup,
    startGroup,
    unfreezeEnrollment,
    type GroupMember,
} from "../../../../src/lib/api/groups";

type Notice = { type: "success" | "error"; text: string };

export default function AdminGroupPage() {
    const { groupId } = useParams<{ groupId: string }>();
    const group = useAsync(() => getGroup(groupId), groupId);
    const directory = useDirectory();

    const [studentId, setStudentId] = useState("");
    const [busy, setBusy] = useState(false);
    const [notice, setNotice] = useState<Notice | null>(null);

    const data = group.data;
    const memberIds = new Set(
        (data?.members ?? []).filter((m) => m.status !== "Cancelled").map((m) => m.userId)
    );
    const students = (directory.data?.users ?? []).filter(
        (user) => user.role === "student" && !memberIds.has(user.id)
    );
    const canAddMembers = data && (data.status === "Forming" || data.status === "Ready") && memberIds.size < data.maxSize;
    const canStart = data && (data.status === "Forming" || data.status === "Ready");

    async function run(action: () => Promise<unknown>, successText: string) {
        setBusy(true);
        setNotice(null);

        try {
            await action();
            setNotice({ type: "success", text: successText });
            group.reload();
        } catch (error) {
            setNotice({ type: "error", text: groupsErrorText(error) });
            // Grupa se mogla promeniti u medjuvremenu (npr. popunila se), pa osvezavamo prikaz.
            group.reload();
        } finally {
            setBusy(false);
        }
    }

    function handleAddMember() {
        if (!studentId) return;

        const name = directory.data?.userName(studentId) ?? "Polaznik";
        void run(() => addGroupMember(groupId, studentId), `Dodato u grupu: ${name}.`);
        setStudentId("");
    }

    function handleStart() {
        if (!data) return;

        const belowMinimum = data.status === "Forming";
        const question = belowMinimum
            ? `Grupa nema minimalan broj polaznika (${memberIds.size} od min ${data.minSize}). Svejedno je pokrenuti?`
            : "Pokrenuti grupu? Polaznicima se otvara kurs.";

        if (!window.confirm(question)) return;

        void run(() => startGroup(groupId, belowMinimum), "Grupa je pokrenuta.");
    }

    function handleComplete() {
        if (!window.confirm("Završiti grupu? Ovo se teško vraća. Grupa se inače sama završi kad svi polaznici završe kurs.")) return;

        void run(() => completeGroup(groupId), "Grupa je završena.");
    }

    function memberName(member: GroupMember) {
        const user = directory.data?.userById(member.userId);
        return user ? getUserDisplayName(user) : member.fullName || "Nepoznat korisnik";
    }

    function handleFreeze(member: GroupMember) {
        if (!window.confirm(`Zamrznuti upis: ${memberName(member)}? Polaznik gubi pristup kursu dok se upis ne odmrzne.`)) return;

        void run(() => freezeEnrollment(member.enrollmentId), `Upis je zamrznut: ${memberName(member)}.`);
    }

    function handleUnfreeze(member: GroupMember) {
        void run(() => unfreezeEnrollment(member.enrollmentId), `Upis je odmrznut: ${memberName(member)}.`);
    }

    function handleCancel(member: GroupMember) {
        if (!window.confirm(`Otkazati upis: ${memberName(member)}? Ovo ne može da se poništi.`)) return;

        void run(() => cancelEnrollment(member.enrollmentId), `Upis je otkazan: ${memberName(member)}.`);
    }

    return (
        <section>
            <Link href="/admin/groups" className="mb-5 inline-block text-sm text-zinc-500 hover:text-zinc-900">
                ← Sve grupe
            </Link>

            {group.loading && !data ? (
                <LoadingState text="Učitavanje grupe..." />
            ) : group.error !== undefined && !data ? (
                <ErrorState error={group.error} serviceName="Servis za grupe" onRetry={group.reload} />
            ) : data ? (
                <div className="flex flex-col gap-6">
                    {notice && (
                        <p
                            role="status"
                            className={`rounded-2xl px-4 py-3 text-sm ${
                                notice.type === "success"
                                    ? "border border-emerald-200 bg-emerald-50 text-emerald-800"
                                    : "border border-red-200 bg-red-50 text-red-700"
                            }`}
                        >
                            {notice.text}
                        </p>
                    )}

                    <div className="rounded-3xl border border-zinc-200 bg-white p-6 sm:p-8">
                        <div className="flex flex-wrap items-start justify-between gap-4">
                            <div>
                                <p className="text-xs font-semibold uppercase tracking-wider text-purple-600">
                                    {groupTypeLabel(data.type)}
                                </p>
                                <h2 className="mt-1 text-2xl font-bold text-zinc-900">
                                    {directory.data?.courseName(data.courseId) ?? "..."}
                                </h2>
                            </div>
                            <span className={`rounded-full px-3 py-1 text-sm font-semibold ${groupStatusStyle(data.status)}`}>
                                {groupStatusLabel(data.status)}
                            </span>
                        </div>

                        <dl className="mt-6 grid gap-3 text-sm sm:grid-cols-3">
                            <div className="rounded-2xl bg-[#fbfaff] p-4">
                                <dt className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Polaznika</dt>
                                <dd className="mt-1 font-medium text-zinc-800">
                                    {memberIds.size} / {data.maxSize} (min {data.minSize})
                                </dd>
                            </div>
                            <div className="rounded-2xl bg-[#fbfaff] p-4">
                                <dt className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Prijave do</dt>
                                <dd className="mt-1 font-medium text-zinc-800">{formatDate(data.formationWindowEndsAt)}</dd>
                            </div>
                            <div className="rounded-2xl bg-[#fbfaff] p-4">
                                <dt className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Počela</dt>
                                <dd className="mt-1 font-medium text-zinc-800">{formatDate(data.startedAt)}</dd>
                            </div>
                        </dl>

                        <h3 className="mt-6 text-sm font-semibold text-zinc-900">Termini</h3>
                        <ul className="mt-2 space-y-1 text-sm text-zinc-700">
                            {data.slots.map((slot) => (
                                <li key={slot.slotIndex}>
                                    {dayLabel(slot.dayOfWeek)} {timeLabel(slot.time)}
                                    <span className="text-zinc-500">
                                        {" · "}
                                        {slot.professorId ? directory.data?.userName(slot.professorId) ?? "..." : "bez profesora"}
                                    </span>
                                </li>
                            ))}
                        </ul>

                        {canStart && (
                            <button
                                type="button"
                                disabled={busy}
                                onClick={handleStart}
                                className="mt-6 rounded-full bg-zinc-900 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-zinc-700 disabled:opacity-50"
                            >
                                Pokreni grupu
                            </button>
                        )}

                        {data.status === "Active" && (
                            <button
                                type="button"
                                disabled={busy}
                                onClick={handleComplete}
                                className="mt-6 rounded-full border border-zinc-300 px-5 py-2.5 text-sm font-semibold text-zinc-700 transition hover:bg-zinc-100 disabled:opacity-50"
                            >
                                Završi grupu
                            </button>
                        )}
                    </div>

                    <div className="rounded-3xl border border-zinc-200 bg-white p-6 sm:p-8">
                        <h3 className="text-lg font-semibold text-zinc-900">Polaznici</h3>

                        {data.members.length === 0 ? (
                            <p className="mt-3 text-sm text-zinc-500">U grupi još nema polaznika.</p>
                        ) : (
                            <ul className="mt-4 divide-y divide-zinc-100">
                                {data.members.map((member) => {
                                    const user = directory.data?.userById(member.userId);

                                    return (
                                        <li key={member.enrollmentId} className="flex flex-wrap items-center justify-between gap-2 py-3">
                                            <div className="min-w-0">
                                                <p className="font-medium text-zinc-900">
                                                    {memberName(member)}
                                                </p>
                                                {user && (
                                                    <p className="break-all text-sm text-zinc-500">
                                                        {user.phone || "bez telefona"} · {user.email}
                                                    </p>
                                                )}
                                            </div>
                                            <div className="flex flex-wrap items-center gap-2">
                                                <span className="rounded-full bg-zinc-100 px-2.5 py-1 text-xs font-semibold text-zinc-600">
                                                    {enrollmentStatusLabel(member.status)}
                                                </span>
                                                {member.status === "Active" && (
                                                    <button
                                                        type="button"
                                                        disabled={busy}
                                                        onClick={() => handleFreeze(member)}
                                                        className="rounded-full px-2.5 py-1 text-xs font-medium transition disabled:opacity-50 text-zinc-600 hover:bg-zinc-100"
                                                    >
                                                        Zamrzni
                                                    </button>
                                                )}
                                                {member.status === "Frozen" && (
                                                    <button
                                                        type="button"
                                                        disabled={busy}
                                                        onClick={() => handleUnfreeze(member)}
                                                        className="rounded-full px-2.5 py-1 text-xs font-medium transition disabled:opacity-50 text-zinc-600 hover:bg-zinc-100"
                                                    >
                                                        Odmrzni
                                                    </button>
                                                )}
                                                {member.status !== "Cancelled" && member.status !== "Completed" && (
                                                    <button
                                                        type="button"
                                                        disabled={busy}
                                                        onClick={() => handleCancel(member)}
                                                        className="rounded-full px-2.5 py-1 text-xs font-medium transition disabled:opacity-50 text-red-600 hover:bg-red-50"
                                                    >
                                                        Otkaži upis
                                                    </button>
                                                )}
                                            </div>
                                        </li>
                                    );
                                })}
                            </ul>
                        )}

                        {canAddMembers && (
                            <div className="mt-6 flex flex-col gap-2 border-t border-zinc-100 pt-5 sm:flex-row">
                                <select
                                    aria-label="Polaznik koga dodaješ"
                                    value={studentId}
                                    onChange={(e) => setStudentId(e.target.value)}
                                    disabled={!directory.data}
                                    className="flex-1 rounded-xl border border-zinc-200 bg-white px-3 py-2.5 text-sm text-zinc-900 outline-none focus:border-purple-400 focus:ring-2 focus:ring-purple-100"
                                >
                                    <option value="">Izaberi polaznika...</option>
                                    {students.map((student) => (
                                        <option key={student.id} value={student.id}>
                                            {getUserDisplayName(student)} ({student.email})
                                        </option>
                                    ))}
                                </select>
                                <button
                                    type="button"
                                    disabled={!studentId || busy}
                                    onClick={handleAddMember}
                                    className="rounded-full bg-[#d8c6ff] px-5 py-2.5 text-sm font-semibold text-zinc-900 transition hover:bg-[#c9b0ff] disabled:opacity-50"
                                >
                                    Dodaj u grupu
                                </button>
                            </div>
                        )}
                    </div>
                </div>
            ) : null}
        </section>
    );
}
