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

                {lesson.recording_url && (

                    <a href={lesson.recording_url}
                       target="_blank"
                       rel="noopener noreferrer"
                       className="inline-block mb-8 text-blue-600 dark:text-blue-400 underline"
                    >
                        Pogledaj snimak lekcije
                    </a>
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