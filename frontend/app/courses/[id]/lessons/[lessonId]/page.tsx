import { notFound } from "next/navigation";

type ListItem = {
    content: string;
    items: ListItem[];
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
    content: {
        blocks?: Block[];
    } | null;
    order: number;
};

type Lesson = {
    id: string;
    title: string;
    recording_url: string;
    is_visible: boolean;
    is_published: boolean;
};

async function getLesson(lessonId: string): Promise<Lesson> {
    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/lessons/${lessonId}`,
        { cache: "no-store" }
    );
    const json = await res.json();
    return json.data;
}

async function getSections(lessonId: string): Promise<Section[]> {
    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/sections?filter[lesson_id][_eq]=${lessonId}&sort=order`,
        { cache: "no-store" }
    );
    const json = await res.json();
    return json.data;
}

function renderNestedList(items: ListItem[]) {
    return (
        <ul className="list-disc ml-5 mb-3">
            {items.map((item, i) => (
                <li key={i}>
                    {item.content}
                    {item.items && item.items.length > 0 && renderNestedList(item.items)}
                </li>
            ))}
        </ul>
    );
}

function renderContent(content: Section["content"]) {
    if (!content || !content.blocks) return null;

    return content.blocks.map((block, i) => {
        if (block.type === "paragraph") {
            return (
                <p key={i} className="mb-3">
                    {block.data.text}
                </p>
            );
        }
        if (block.type === "header") {
            return (
                <h3 key={i} className="text-lg font-medium mb-2 mt-4">
                    {block.data.text}
                </h3>
            );
        }
        if (block.type === "list" && Array.isArray(block.data.items)) {
            return (
                <ul key={i} className="list-disc ml-5 mb-3">
                    {(block.data.items as string[]).map((item, j) => (
                        <li key={j}>{item}</li>
                    ))}
                </ul>
            );
        }
        if (block.type === "nestedlist" && Array.isArray(block.data.items)) {
            return (
                <div key={i} className="mb-3">
                    {renderNestedList(block.data.items as ListItem[])}
                </div>
            );
        }
        if (block.type === "image" && block.data.file) {
            return (
                <figure key={i} className="mb-4">
                    <img
                        src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${block.data.file.fileId}`}
                        alt={block.data.caption || ""}
                        className="rounded-md max-w-full mx-auto"
                    />
                    {block.data.caption && (
                        <figcaption className="text-center text-sm text-zinc-500 mt-2">
                            {block.data.caption}
                        </figcaption>
                    )}
                </figure>
            );
        }
        return null;
    });
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

export default async function LessonPage({
                                             params,
                                         }: {
    params: Promise<{ id: string; lessonId: string }>;
}) {
    const { lessonId } = await params;
    const lesson = await getLesson(lessonId);

    if (!lesson || !lesson.is_visible || !lesson.is_published) {
        notFound();
    }

    const sections = await getSections(lessonId);

    return (
        <div className="flex flex-col flex-1 items-center bg-zinc-50 font-sans dark:bg-black">
            <main className="w-full max-w-3xl py-16 px-6">
                <h1 className="text-3xl font-semibold mb-2 text-black dark:text-zinc-50">
                    {lesson.title}
                </h1>


                {lesson.recording_url ? (
                    (() => {
                        const embedUrl = getYouTubeEmbedUrl(lesson.recording_url);

                        return embedUrl ? (
                            <div className="mb-8 w-full overflow-hidden rounded-2xl bg-black shadow-sm">
                                <div className="aspect-video w-full">
                                    <iframe
                                        src={embedUrl}
                                        title={`Snimak lekcije: ${lesson.title}`}
                                        className="h-full w-full"
                                        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                                        allowFullScreen
                                    />
                                </div>
                            </div>
                        ) : (
                            <a
                                href={lesson.recording_url}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="mb-8 inline-block text-blue-600 underline"
                            >
                                Pogledaj snimak lekcije
                            </a>
                        );
                    })()
                ) : (
                    <div className="mb-8 flex aspect-video w-full flex-col items-center justify-center rounded-2xl border border-dashed border-purple-300 bg-purple-50 px-6 text-center">
                        <span className="mb-3 text-4xl">▶</span>
                        <h2 className="text-lg font-semibold text-zinc-800">
                            Snimak uskoro stiže
                        </h2>
                        <p className="mt-2 text-sm text-zinc-600">
                            Video lekcija će biti dostupna ovde.
                        </p>
                    </div>
                )}


                <div className="flex flex-col gap-6">
                    {sections.map((section) => (
                        <div key={section.id}>
                            <h2 className="text-xl font-medium mb-2 text-black dark:text-zinc-50">
                                {section.title}
                            </h2>
                            <div className="text-zinc-700 dark:text-zinc-300">
                                {renderContent(section.content)}
                            </div>
                        </div>
                    ))}
                </div>
            </main>
        </div>
    );
}