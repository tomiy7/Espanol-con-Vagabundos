"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import type { Lesson } from "./page";

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
    title: string;
    content: { blocks?: Block[] } | null;
    order: number;
};

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
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(false);

    // Ucitaj sekcije cim se otvori lekcija (i kad se promeni lekcija)
    useEffect(() => {
        let cancelled = false;

        async function loadSections() {
            setLoading(true);
            setError(false);
            setSections([]);

            try {
                const query = new URLSearchParams({
                    "filter[lesson_id][_eq]": currentLessonId,
                    sort: "order",
                });

                const res = await fetch(
                    `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/sections?${query}`
                );

                if (!res.ok) throw new Error("Failed to load sections");

                const json = await res.json();
                if (!cancelled) setSections(json.data ?? []);
            } catch {
                if (!cancelled) setError(true);
            } finally {
                if (!cancelled) setLoading(false);
            }
        }

        void loadSections();

        return () => {
            cancelled = true;
        };
    }, [currentLessonId]);

    function selectLesson(lesson: Lesson) {
        if (lesson.id !== currentLessonId) {
            router.push(`/courses/${courseId}/lessons/${lesson.id}`);
        }
    }

    return (
        <div className="flex flex-col gap-3">
            {lessons.map((lesson) => {
                const isOpen = lesson.id === currentLessonId;
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
                            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-purple-100 font-semibold text-purple-800">
                                {lesson.order}
                            </span>

                            <span className="min-w-0 flex-1 font-medium text-zinc-900">
                                {lesson.title}
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
                                    <div className="flex flex-col gap-6 text-zinc-700">
                                        {sections.map((section) => (
                                            <section key={section.id}>
                                                <h3 className="mb-3 text-lg font-semibold text-zinc-900">
                                                    {section.title}
                                                </h3>
                                                {renderBlocks(
                                                    section.content?.blocks
                                                )}
                                            </section>
                                        ))}

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