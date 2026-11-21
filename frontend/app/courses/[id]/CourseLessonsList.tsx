
"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import type { Lesson } from "./page";
import {
    useLessonProgress,
    type LessonSectionsMap,
} from "../../../src/features/progress/hooks/useLessonProgress";

type DirectusSection = {
    id: string | number;
    lesson_id: string | number | { id: string | number };
};

function getLessonId(value: DirectusSection["lesson_id"]): string {
    if (typeof value === "object" && value !== null) {
        return String(value.id);
    }

    return String(value);
}

function ProgressBar({ percentage }: { percentage: number }) {
    return (
        <div
            className="h-2 overflow-hidden rounded-full bg-purple-100"
            role="progressbar"
            aria-label="Progres"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={percentage}
        >
            <div
                className="h-full rounded-full bg-purple-600 transition-all duration-300"
                style={{ width: `${percentage}%` }}
            />
        </div>
    );
}

export default function CourseLessonsList({
                                              lessons,
                                              courseId,
                                          }: {
    lessons: Lesson[];
    courseId: string;
}) {
    const [lessonSections, setLessonSections] =
        useState<LessonSectionsMap>({});

    const [sectionsLoading, setSectionsLoading] = useState(true);

    const lessonIdsKey = useMemo(
        () => lessons.map((lesson) => lesson.id).sort().join(","),
        [lessons]
    );

    useEffect(() => {
        let cancelled = false;

        async function loadSectionIds() {
            if (!lessonIdsKey) {
                setLessonSections({});
                setSectionsLoading(false);
                return;
            }

            setSectionsLoading(true);

            try {
                const params = new URLSearchParams({
                    "filter[lesson_id][_in]": lessonIdsKey,
                    fields: "id,lesson_id",
                    limit: "-1",
                });

                const response = await fetch(
                    `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/sections?${params}`
                );

                if (!response.ok) {
                    throw new Error("Failed to load sections");
                }

                const json = await response.json();

                const nextMap: LessonSectionsMap = Object.fromEntries(
                    lessons.map((lesson) => [lesson.id, []])
                );

                for (const section of (json.data ?? []) as DirectusSection[]) {
                    const lessonId = getLessonId(section.lesson_id);

                    if (nextMap[lessonId]) {
                        nextMap[lessonId].push(String(section.id));
                    }
                }

                if (!cancelled) {
                    setLessonSections(nextMap);
                }
            } catch (error) {
                console.error("Error loading lesson sections:", error);

                if (!cancelled) {
                    setLessonSections(
                        Object.fromEntries(
                            lessons.map((lesson) => [lesson.id, []])
                        )
                    );
                }
            } finally {
                if (!cancelled) {
                    setSectionsLoading(false);
                }
            }
        }

        void loadSectionIds();

        return () => {
            cancelled = true;
        };
    }, [lessonIdsKey, lessons]);

    const {
        lessonProgressById,
        completedCount,
        totalCount,
        percentage,
        completedLessonCount,
        totalLessonCount,
        loading,
        canTrackProgress,
    } = useLessonProgress(courseId, lessonSections);

    const isLoading = sectionsLoading || loading;

    return (
        <div className="flex flex-col gap-4">

            {/* UKUPAN PROGRES KURSA */}
            <div className="rounded-2xl border border-purple-100 bg-white p-4 sm:p-5">
                <div className="mb-2 flex items-center justify-between gap-3">
                    <p className="font-semibold text-zinc-900">
                        Tvoj napredak na kursu
                    </p>

                    <p className="text-sm font-semibold text-purple-700">
                        {isLoading ? "..." : `${percentage}%`}
                    </p>
                </div>

                <ProgressBar percentage={percentage} />

                <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-xs text-zinc-500">
                    <span>
                        {isLoading
                            ? "Učitavanje progresa..."
                            : `${completedCount} od ${totalCount} sekcija završeno`}
                    </span>

                    <span>
                        {isLoading
                            ? ""
                            : `${completedLessonCount} od ${totalLessonCount} lekcija završeno`}
                    </span>
                </div>

                {!isLoading && canTrackProgress && (
                    <p className="mt-3 text-xs text-zinc-500">
                        Napredak se za sada čuva u ovom pregledaču.
                    </p>
                )}

                {!isLoading && !canTrackProgress && (
                    <p className="mt-3 text-sm text-amber-700">
                        Prijavi se da bi se tvoj napredak čuvao odvojeno za tvoj nalog.
                    </p>
                )}
            </div>

            <h2 className="mb-0 mt-2 text-2xl font-semibold text-zinc-900">
                Šta te očekuje na kursu?
            </h2>

            {/* POJEDINAČNI PROGRES LEKCIJA */}
            {lessons.map((lesson) => {
                const progress = lessonProgressById[lesson.id] ?? {
                    completedCount: 0,
                    totalCount: 0,
                    percentage: 0,
                };

                const isCompleted =
                    progress.totalCount > 0 &&
                    progress.percentage === 100;

                return (
                    <Link
                        key={lesson.id}
                        href={`/courses/${courseId}/lessons/${lesson.id}`}
                        className="group flex items-center gap-4 rounded-2xl border border-zinc-200 bg-white p-4 transition hover:border-purple-300 hover:shadow-sm sm:p-5"
                    >
                        <span
                            className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl font-semibold ${
                                isCompleted
                                    ? "bg-emerald-100 text-emerald-800"
                                    : "bg-purple-100 text-purple-800"
                            }`}
                        >
                            {isCompleted ? "✓" : lesson.order}
                        </span>

                        <span className="min-w-0 flex-1">
                            <span className="flex flex-wrap items-center gap-2">
                                <span className="font-medium text-zinc-900">
                                    {lesson.title}
                                </span>

                                <span
                                    className={`rounded-full px-2 py-0.5 text-xs font-semibold ${
                                        isCompleted
                                            ? "bg-emerald-50 text-emerald-700"
                                            : "bg-purple-50 text-purple-700"
                                    }`}
                                >
                                    {isLoading
                                        ? "..."
                                        : `${progress.percentage}%`}
                                </span>
                            </span>

                            <span className="mt-2 block">
                                <ProgressBar
                                    percentage={progress.percentage}
                                />
                            </span>

                            <span className="mt-1 block text-xs text-zinc-500">
                                {isLoading
                                    ? "Učitavanje..."
                                    : `${progress.completedCount} od ${progress.totalCount} sekcija`}
                            </span>
                        </span>

                        <span className="text-xl text-zinc-400 transition group-hover:translate-x-1 group-hover:text-purple-600">
                            →
                        </span>
                    </Link>
                );
            })}
        </div>
    );
}
