
"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import type { Lesson } from "./page";
import {
    useLessonProgress,
    type LessonSectionsMap,
} from "../../../../../src/features/progress/hooks/useLessonProgress";

type ListItem = {
    content: string;
    items?: ListItem[];
};

type Block = {
    type: string;
    data: {
        text?: string;
        items?: string[] | ListItem[];
        caption?: string;
        file?: { fileId: string };
    };
};

type Section = {
    id: string;
    lesson_id: string | number | { id: string | number };
    title: string;
    content: { blocks?: Block[] } | null;
    order: number;
};

function getLessonId(value: Section["lesson_id"]): string {
    if (typeof value === "object" && value !== null) return String(value.id);
    return String(value);
}

function getYouTubeEmbedUrl(url: string): string | null {
    try {
        const parsed = new URL(url);
        let videoId: string | null = null;

        if (parsed.hostname === "youtu.be") {
            videoId = parsed.pathname.slice(1);
        } else if (
            ["youtube.com", "www.youtube.com", "m.youtube.com"].includes(
                parsed.hostname
            )
        ) {
            if (parsed.pathname === "/watch") {
                videoId = parsed.searchParams.get("v");
            } else if (
                parsed.pathname.startsWith("/embed/") ||
                parsed.pathname.startsWith("/shorts/")
            ) {
                videoId = parsed.pathname.split("/")[2];
            }
        }

        return videoId
            ? `https://www.youtube.com/embed/${videoId}`
            : null;
    } catch {
        return null;
    }
}

// Editor.js cuva tekst kao HTML (&nbsp;, <b>, <i>...), pa ga ovako prikazujemo.
// Sadrzaj unose samo profesori preko Directusa (poverljivi korisnici).
function Html({ html, className }: { html?: string; className?: string }) {
    return (
        <span
            className={className}
            dangerouslySetInnerHTML={{ __html: html ?? "" }}
        />
    );
}

function renderBlocks(blocks: Block[] = []): React.ReactNode {
    return blocks.map((block, index) => {
        if (block.type === "paragraph") {
            return (
                <p key={index} className="mb-3">
                    <Html html={block.data.text} />
                </p>
            );
        }

        if (block.type === "header") {
            return (
                <h4 key={index} className="mb-2 mt-4 font-semibold">
                    <Html html={block.data.text} />
                </h4>
            );
        }

        if (block.type === "list" && Array.isArray(block.data.items)) {
            return (
                <ul key={index} className="mb-3 ml-5 list-disc">
                    {(block.data.items as string[]).map((item, i) => (
                        <li key={i}>
                            <Html html={item} />
                        </li>
                    ))}
                </ul>
            );
        }

        if (block.type === "nestedlist" && Array.isArray(block.data.items)) {
            const renderList = (items: ListItem[]): React.ReactNode => (
                <ul className="mb-3 ml-5 list-disc">
                    {items.map((item, i) => (
                        <li key={i}>
                            <Html html={item.content} />
                            {item.items?.length ? renderList(item.items) : null}
                        </li>
                    ))}
                </ul>
            );

            return (
                <div key={index}>
                    {renderList(block.data.items as ListItem[])}
                </div>
            );
        }

        if (block.type === "image" && block.data.file) {
            return (
                <figure key={index} className="my-4">
                    <img
                        src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${block.data.file.fileId}`}
                        alt={block.data.caption || ""}
                        className="mx-auto max-w-full rounded-lg"
                    />
                    {block.data.caption && (
                        <figcaption className="mt-2 text-center text-sm text-zinc-500">
                            {block.data.caption}
                        </figcaption>
                    )}
                </figure>
            );
        }

        return null;
    });
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

export default function LessonAccordion({
                                            lessons,
                                            courseId,
                                            currentLessonId,
                                        }: {
    lessons: Lesson[];
    courseId: string;
    currentLessonId: string;
}) {
    const router = useRouter();
    const [sections, setSections] = useState<Section[]>([]);
    const [lessonSections, setLessonSections] = useState<LessonSectionsMap>({});
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(false);

    const lessonIdsKey = useMemo(
        () => lessons.map((lesson) => lesson.id).sort().join(","),
        [lessons]
    );

    const {
        completedSectionIds,
        lessonProgressById,
        canTrackProgress,
        markSectionCompleted,
        markSectionIncomplete,
    } = useLessonProgress(courseId, lessonSections);

    // Ucitavamo sekcije svih lekcija da bismo mogli da izracunamo i progres kursa.
    // Sadrzaj sekcija prikazujemo samo za trenutno otvorenu lekciju.
    useEffect(() => {
        let cancelled = false;

        async function loadSections() {
            setLoading(true);
            setError(false);
            setSections([]);

            if (!lessonIdsKey) {
                setLessonSections({});
                setLoading(false);
                return;
            }

            try {
                const query = new URLSearchParams({
                    "filter[lesson_id][_in]": lessonIdsKey,
                    sort: "order",
                });

                const response = await fetch(
                    `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/sections?${query}`
                );

                if (!response.ok) throw new Error("Failed to load sections");

                const json = await response.json();
                const allSections = (json.data ?? []) as Section[];
                const nextMap: LessonSectionsMap = Object.fromEntries(
                    lessons.map((lesson) => [lesson.id, []])
                );

                for (const section of allSections) {
                    const lessonId = getLessonId(section.lesson_id);
                    if (nextMap[lessonId]) {
                        nextMap[lessonId].push(String(section.id));
                    }
                }

                if (!cancelled) {
                    setLessonSections(nextMap);
                    setSections(
                        allSections
                            .filter((section) => getLessonId(section.lesson_id) === currentLessonId)
                            .sort((a, b) => a.order - b.order)
                    );
                }
            } catch {
                if (!cancelled) {
                    setError(true);
                    setLessonSections(
                        Object.fromEntries(lessons.map((lesson) => [lesson.id, []]))
                    );
                }
            } finally {
                if (!cancelled) setLoading(false);
            }
        }

        void loadSections();

        return () => {
            cancelled = true;
        };
    }, [currentLessonId, lessonIdsKey, lessons]);

    function selectLesson(lesson: Lesson) {
        if (lesson.id !== currentLessonId) {
            router.push(`/courses/${courseId}/lessons/${lesson.id}`);
        }
    }

    const currentLessonProgress = lessonProgressById[currentLessonId] ?? {
        completedCount: 0,
        totalCount: 0,
        percentage: 0,
    };

    return (
        <div className="flex flex-col gap-4">
            {lessons.map((lesson) => {
                const isOpen = lesson.id === currentLessonId;
                const progress = lessonProgressById[lesson.id] ?? {
                    completedCount: 0,
                    totalCount: 0,
                    percentage: 0,
                };
                const isCompleted = progress.totalCount > 0 && progress.percentage === 100;
                const embedUrl = lesson.recording_url
                    ? getYouTubeEmbedUrl(lesson.recording_url)
                    : null;

                return (
                    <article
                        key={lesson.id}
                        className={`overflow-hidden rounded-2xl border bg-white ${
                            isOpen
                                ? "border-purple-300 shadow-sm"
                                : "border-zinc-200"
                        }`}
                    >
                        <button
                            type="button"
                            onClick={() => selectLesson(lesson)}
                            aria-expanded={isOpen}
                            className="flex w-full items-center gap-4 p-4 text-left transition hover:bg-purple-50/50 sm:p-5"
                        >
                            <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl font-semibold ${
                                isCompleted
                                    ? "bg-emerald-100 text-emerald-800"
                                    : "bg-purple-100 text-purple-800"
                            }`}>
                                {isCompleted ? "✓" : lesson.order}
                            </span>

                            <span className="min-w-0 flex-1 font-medium text-zinc-900">
                                {lesson.title}
                            </span>

                            <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold tabular-nums ${
                                isCompleted
                                    ? "bg-emerald-50 text-emerald-700"
                                    : progress.percentage > 0
                                        ? "bg-purple-50 text-purple-700"
                                        : "bg-zinc-100 text-zinc-600"
                            }`}>
                                {loading ? "..." : `${progress.percentage}%`}
                            </span>

                            <span className="text-xl text-zinc-400">
                                {isOpen ? "−" : "+"}
                            </span>
                        </button>

                        {isOpen && (
                            <div className="border-t border-zinc-100 p-4 sm:p-6">
                                {lesson.recording_url ? (
                                    embedUrl ? (
                                        <div className="mb-6 aspect-video overflow-hidden rounded-xl bg-black">
                                            <iframe
                                                src={embedUrl}
                                                title={lesson.title}
                                                loading="lazy"
                                                className="h-full w-full"
                                                allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                                                allowFullScreen
                                            />
                                        </div>
                                    ) : (
                                        <a
                                            href={lesson.recording_url}
                                            target="_blank"
                                            rel="noopener noreferrer"
                                            className="mb-6 inline-block text-purple-700 underline"
                                        >
                                            Pogledaj snimak lekcije
                                        </a>
                                    )
                                ) : (
                                    <div className="mb-6 flex aspect-video flex-col items-center justify-center rounded-xl border border-dashed border-purple-300 bg-purple-50 text-center">
                                        <span className="mb-3 text-3xl">▶</span>
                                        <p className="font-medium text-zinc-800">
                                            Snimak uskoro stiže
                                        </p>
                                        <p className="mt-2 text-sm text-zinc-500">
                                            Video lekcija će biti dostupna ovde.
                                        </p>
                                    </div>
                                )}

                                <div className="mb-5 rounded-xl bg-purple-50 p-4">
                                    <div className="mb-2 flex items-center justify-between gap-3">
                                        <p className="text-sm font-semibold text-zinc-900">
                                            Progres ove lekcije
                                        </p>
                                        <p className="text-sm font-semibold text-purple-700 tabular-nums">
                                            {currentLessonProgress.percentage}%
                                        </p>
                                    </div>
                                    <ProgressBar percentage={currentLessonProgress.percentage} />
                                    <p className="mt-2 text-xs text-zinc-600">
                                        {currentLessonProgress.completedCount} od {currentLessonProgress.totalCount} sekcija završeno
                                    </p>
                                </div>

                                {loading && (
                                    <p className="py-4 text-sm text-zinc-500">
                                        Učitavanje sadržaja...
                                    </p>
                                )}

                                {error && (
                                    <p className="py-4 text-sm text-red-600">
                                        Sadržaj nije učitan. Pokušaj ponovo.
                                    </p>
                                )}

                                {!loading && !error && (
                                    <div className="flex flex-col gap-5 text-zinc-700">
                                        {sections.map((section) => {
                                            const isSectionCompleted = completedSectionIds.includes(String(section.id));

                                            return (
                                                <section
                                                    key={section.id}
                                                    className="rounded-xl border border-zinc-200 p-4 sm:p-5"
                                                >
                                                    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                                                        <h3 className="text-lg font-semibold text-zinc-900">
                                                            {section.title}
                                                        </h3>
                                                        <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                                                            isSectionCompleted
                                                                ? "bg-emerald-50 text-emerald-700"
                                                                : "bg-zinc-100 text-zinc-600"
                                                        }`}>
                                                            {isSectionCompleted ? "Završena" : "Nije završena"}
                                                        </span>
                                                    </div>

                                                    {renderBlocks(section.content?.blocks)}

                                                    {canTrackProgress ? (
                                                        <button
                                                            type="button"
                                                            onClick={() =>
                                                                isSectionCompleted
                                                                    ? markSectionIncomplete(String(section.id))
                                                                    : markSectionCompleted(String(section.id))
                                                            }
                                                            className={`mt-3 rounded-full px-4 py-2 text-sm font-semibold transition focus:outline-none focus:ring-2 focus:ring-purple-500 focus:ring-offset-2 ${
                                                                isSectionCompleted
                                                                    ? "border border-zinc-300 text-zinc-700 hover:bg-zinc-50"
                                                                    : "bg-purple-600 text-white hover:bg-purple-700"
                                                            }`}
                                                        >
                                                            {isSectionCompleted
                                                                ? "Vrati sekciju na nezavršeno"
                                                                : "Označi sekciju kao završenu"}
                                                        </button>
                                                    ) : (
                                                        <p className="mt-3 text-sm text-zinc-500">
                                                            Prijavi se da bi mogla da pratiš svoj napredak.
                                                        </p>
                                                    )}
                                                </section>
                                            );
                                        })}

                                        {sections.length === 0 && (
                                            <p className="text-sm text-zinc-500">
                                                Sadržaj lekcije biće uskoro dostupan.
                                            </p>
                                        )}
                                    </div>
                                )}
                            </div>
                        )}
                    </article>
                );
            })}
        </div>
    );
}
