"use client";

import { useState } from "react";
import { DAYS, groupsErrorText } from "../../../src/features/groups/labels";
import { getUserDisplayName, type UserDetail } from "../../../src/lib/api/auth";
import type { CourseOption } from "../../../src/lib/api/courses";
import { createGroup, type DayOfWeek } from "../../../src/lib/api/groups";

type SlotForm = { dayOfWeek: DayOfWeek | ""; time: string; professorId: string };

const EMPTY_SLOTS: SlotForm[] = [
    { dayOfWeek: "Monday", time: "18:00", professorId: "" },
    { dayOfWeek: "Wednesday", time: "18:00", professorId: "" },
    { dayOfWeek: "Friday", time: "18:00", professorId: "" },
];

const inputClass =
    "w-full rounded-xl border border-zinc-200 bg-white px-3 py-2.5 text-sm text-zinc-900 outline-none transition focus:border-purple-400 focus:ring-2 focus:ring-purple-100";

export default function CreateGroupForm({
    courses,
    professors,
    onCreated,
    onCancel,
}: {
    courses: CourseOption[];
    professors: UserDetail[];
    onCreated: () => void;
    onCancel: () => void;
}) {
    const [courseId, setCourseId] = useState(courses[0]?.id ?? "");
    const [slots, setSlots] = useState<SlotForm[]>(EMPTY_SLOTS);
    const [windowDays, setWindowDays] = useState("7");
    const [error, setError] = useState("");
    const [saving, setSaving] = useState(false);

    function updateSlot(index: number, change: Partial<SlotForm>) {
        setSlots((current) => current.map((slot, i) => (i === index ? { ...slot, ...change } : slot)));
        setError("");
    }

    async function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        setError("");

        if (!courseId) {
            setError("Izaberi kurs.");
            return;
        }

        if (slots.some((slot) => !slot.dayOfWeek || !slot.time || !slot.professorId)) {
            setError("Popuni dan, vreme i profesora za sva 3 termina.");
            return;
        }

        const keys = slots.map((slot) => `${slot.dayOfWeek} ${slot.time}`);
        if (new Set(keys).size !== keys.length) {
            setError("Svaki termin mora imati drugačiji dan ili vreme.");
            return;
        }

        const days = Number(windowDays);
        if (!Number.isInteger(days) || days < 1) {
            setError("Rok za popunu grupe mora biti bar 1 dan.");
            return;
        }

        setSaving(true);

        try {
            await createGroup({
                courseId,
                slots: slots.map((slot) => ({
                    dayOfWeek: slot.dayOfWeek as DayOfWeek,
                    time: `${slot.time}:00`,
                    professorId: slot.professorId,
                })),
                formationWindowDays: days,
            });

            onCreated();
        } catch (err) {
            setError(groupsErrorText(err));
        } finally {
            setSaving(false);
        }
    }

    return (
        <form
            onSubmit={handleSubmit}
            className="mb-8 rounded-3xl border border-purple-100 bg-white p-5 shadow-sm sm:p-7"
        >
            <h3 className="text-lg font-semibold text-zinc-900">Nova grupa</h3>
            <p className="mt-1 text-sm text-zinc-500">
                Grupa ima 3 termina nedeljno. Polaznici mogu da se prijave dok traje rok za popunu.
            </p>

            <div className="mt-5 grid gap-4 sm:grid-cols-[1fr_180px]">
                <label className="flex flex-col gap-1.5 text-sm font-medium text-zinc-700">
                    Kurs
                    <select value={courseId} onChange={(e) => setCourseId(e.target.value)} className={inputClass}>
                        {courses.length === 0 && <option value="">Nema kurseva</option>}
                        {courses.map((course) => (
                            <option key={course.id} value={course.id}>
                                {course.name}
                            </option>
                        ))}
                    </select>
                </label>

                <label className="flex flex-col gap-1.5 text-sm font-medium text-zinc-700">
                    Rok za popunu (dana)
                    <input
                        type="number"
                        min={1}
                        value={windowDays}
                        onChange={(e) => setWindowDays(e.target.value)}
                        className={inputClass}
                    />
                </label>
            </div>

            <fieldset className="mt-5">
                <legend className="text-sm font-medium text-zinc-700">Termini</legend>

                {professors.length === 0 && (
                    <p className="mt-2 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">
                        Nema korisnika sa rolom profesora. Prvo nekom nalogu dodeli rolu „professor”.
                    </p>
                )}

                <div className="mt-2 flex flex-col gap-3">
                    {slots.map((slot, index) => (
                        <div key={index} className="grid gap-2 rounded-2xl bg-[#faf8ff] p-3 sm:grid-cols-[150px_120px_1fr]">
                            <select
                                aria-label={`Dan, termin ${index + 1}`}
                                value={slot.dayOfWeek}
                                onChange={(e) => updateSlot(index, { dayOfWeek: e.target.value as DayOfWeek })}
                                className={inputClass}
                            >
                                {DAYS.map((day) => (
                                    <option key={day.value} value={day.value}>
                                        {day.label}
                                    </option>
                                ))}
                            </select>

                            <input
                                aria-label={`Vreme, termin ${index + 1}`}
                                type="time"
                                value={slot.time}
                                onChange={(e) => updateSlot(index, { time: e.target.value })}
                                className={inputClass}
                            />

                            <select
                                aria-label={`Profesor, termin ${index + 1}`}
                                value={slot.professorId}
                                onChange={(e) => updateSlot(index, { professorId: e.target.value })}
                                className={inputClass}
                            >
                                <option value="">Izaberi profesora</option>
                                {professors.map((professor) => (
                                    <option key={professor.id} value={professor.id}>
                                        {getUserDisplayName(professor)}
                                    </option>
                                ))}
                            </select>
                        </div>
                    ))}
                </div>
            </fieldset>

            {error && (
                <p role="alert" className="mt-4 text-sm text-red-600">
                    {error}
                </p>
            )}

            <div className="mt-6 flex justify-end gap-2">
                <button
                    type="button"
                    onClick={onCancel}
                    className="rounded-full border border-zinc-200 px-5 py-2.5 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50"
                >
                    Odustani
                </button>
                <button
                    type="submit"
                    disabled={saving}
                    className="rounded-full bg-[#d8c6ff] px-5 py-2.5 text-sm font-semibold text-zinc-900 transition hover:bg-[#c9b0ff] disabled:opacity-50"
                >
                    {saving ? "Čuvam..." : "Napravi grupu"}
                </button>
            </div>
        </form>
    );
}
