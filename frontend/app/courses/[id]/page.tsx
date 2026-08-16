import Link from "next/link";
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

type RichText = {
    blocks?: Block[];
} | null;

type Lesson = {
    id: string;
    title: string;
    order: number;
    recording_url: string;
};

type Course = {
    id: string;
    name: string;
    description: RichText;
    price: string;
    ebook_price: string;
    ebook_pdf: string;
    is_published: boolean;
};

async function getCourse(id: string): Promise<Course> {
    const res = await fetch(`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/courses/${id}`, {
        cache: "no-store",
    });
    const json = await res.json();
    return json.data;
}

async function getLessons(courseId: string): Promise<Lesson[]> {
    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/lessons?filter[course_id][_eq]=${courseId}&filter[is_visible][_eq]=true&filter[is_published][_eq]=true&sort=order`,
        { cache: "no-store" }
    );
    const json = await res.json();
    return json.data;
}

function renderNestedList(items: ListItem[]) {
    return (
        <ul className="list-disc ml-5 mb-4">
            {items.map((item, i) => (
                <li key={i}>
                    {item.content}
                    {item.items && item.items.length > 0 && renderNestedList(item.items)}
                </li>
            ))}
        </ul>
    );
}

function renderContent(content: RichText) {
    if (!content || !content.blocks) return null;

    return content.blocks.map((block, i) => {
        if (block.type === "paragraph") {
            return (
                <p key={i} className="mb-4" dangerouslySetInnerHTML={{ __html: block.data.text || "" }} />
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
                <ul key={i} className="list-disc ml-5 mb-4">
                    {(block.data.items as string[]).map((item, j) => (
                        <li key={j}>{item}</li>
                    ))}
                </ul>
            );
        }
        if (block.type === "nestedlist" && Array.isArray(block.data.items)) {
            return (
                <div key={i} className="mb-4">
                    {renderNestedList(block.data.items as ListItem[])}
                </div>
            );
        }
        if (block.type === "image" && block.data.file) {
            return (
                <img
                    key={i}
                    src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${block.data.file.fileId}`}
                    alt=""
                    className="rounded-md max-w-full mb-4"
                />
            );
        }
        return null;
    });
}

export default async function CoursePage({
                                             params,
                                         }: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const course = await getCourse(id);

    if (!course || !course.is_published) {
        notFound();
    }

    const lessons = await getLessons(id);

    return (
        <div className="flex flex-col flex-1 items-center bg-zinc-50 font-sans dark:bg-black">
            <main className="w-full max-w-3xl py-16 px-6">
                <h1 className="text-3xl font-semibold mb-2 text-black dark:text-zinc-50">
                    {course.name}
                </h1>

                {course.ebook_pdf && (
                    <a href={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${course.ebook_pdf}`}
                       target="_blank"
                       rel="noopener noreferrer"
                       className="inline-block mb-4 text-blue-600 dark:text-blue-400 underline"
                    >
                        Preuzmi e-book uz kurs ({parseFloat(course.ebook_price)} €)
                    </a>
                )}

                <div className="text-zinc-600 dark:text-zinc-400 mb-8">
                    {renderContent(course.description)}
                </div>

                <h2 className="text-xl font-medium mb-4 text-black dark:text-zinc-50">
                    Lekcije
                </h2>

                <div className="flex flex-col gap-3">
                    {lessons.map((lesson) => (
                        <Link
                            key={lesson.id}
                            href={`/courses/${id}/lessons/${lesson.id}`}
                            className="rounded-lg border border-zinc-200 dark:border-zinc-800 p-4 hover:bg-zinc-100 dark:hover:bg-zinc-900 transition-colors block"
                        >
                            <p className="text-black dark:text-zinc-50">
                                {lesson.order}. {lesson.title}
                            </p>
                        </Link>
                    ))}
                </div>
            </main>
        </div>
    );
}