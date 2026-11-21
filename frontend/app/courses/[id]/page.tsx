
import Link from "next/link";
import CourseLessonsList from "./CourseLessonsList";
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

export type Lesson = {
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
    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/courses/${id}`,
        {
            cache: "no-store",
        }
    );

    if (!res.ok) {
        return null as unknown as Course;
    }

    const json = await res.json();
    return json.data;
}

async function getLessons(courseId: string): Promise<Lesson[]> {
    const params = new URLSearchParams({
        "filter[course_id][_eq]": courseId,
        "filter[is_visible][_eq]": "true",
        "filter[is_published][_eq]": "true",
        sort: "order",
        limit: "-1",
    });

    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/lessons?${params}`,
        {
            cache: "no-store",
        }
    );

    if (!res.ok) {
        return [];
    }

    const json = await res.json();
    return json.data ?? [];
}

function renderNestedList(items: ListItem[]) {
    return (
        <ul className="mb-4 ml-5 list-disc">
            {items.map((item, i) => (
                <li key={i}>
                    {item.content}
                    {item.items && item.items.length > 0 &&
                        renderNestedList(item.items)}
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
                <p
                    key={i}
                    className="mb-4"
                    dangerouslySetInnerHTML={{
                        __html: block.data.text || "",
                    }}
                />
            );
        }

        if (block.type === "header") {
            return (
                <h3
                    key={i}
                    className="mb-2 mt-4 text-lg font-medium"
                >
                    {block.data.text}
                </h3>
            );
        }

        if (
            block.type === "list" &&
            Array.isArray(block.data.items)
        ) {
            return (
                <ul key={i} className="mb-4 ml-5 list-disc">
                    {(block.data.items as string[]).map((item, j) => (
                        <li key={j}>{item}</li>
                    ))}
                </ul>
            );
        }

        if (
            block.type === "nestedlist" &&
            Array.isArray(block.data.items)
        ) {
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
                    className="mb-4 max-w-full rounded-md"
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
        <main className="flex-1 bg-zinc-50 px-4 py-10 sm:px-6 sm:py-16">
            <div className="mx-auto grid w-full max-w-5xl gap-8 lg:grid-cols-[1fr_320px]">

                <section className="min-w-0">
                    <Link
                        href="/courses"
                        className="mb-6 inline-block text-sm text-zinc-500 hover:text-zinc-900"
                    >
                        ← Svi kursevi
                    </Link>

                    <div className="mb-8 rounded-3xl border border-zinc-200 bg-white p-6 sm:p-8">
                        <p className="mb-3 text-sm font-semibold uppercase tracking-widest text-purple-600">
                            Español con Vagabundos
                        </p>

                        <h1 className="mb-5 text-3xl font-bold leading-tight text-zinc-900 sm:text-4xl">
                            {course.name}
                        </h1>

                        <div className="leading-7 text-zinc-600">
                            {renderContent(course.description)}
                        </div>
                    </div>

                    {lessons.length > 0 ? (
                        <CourseLessonsList
                            lessons={lessons}
                            courseId={id}
                        />
                    ) : (
                        <section>
                            <h2 className="mb-4 text-2xl font-semibold text-zinc-900">
                                Šta te očekuje na kursu?
                            </h2>

                            <p className="rounded-2xl border border-dashed border-zinc-300 bg-white p-6 text-zinc-500">
                                Lekcije će uskoro biti dostupne.
                            </p>
                        </section>
                    )}
                </section>

                <aside className="lg:sticky lg:top-6 lg:self-start">
                    <div className="rounded-3xl border border-zinc-200 bg-white p-6 shadow-sm">
                        <p className="mb-2 text-sm text-zinc-500">
                            Cena kursa
                        </p>

                        <p className="mb-6 text-4xl font-bold text-zinc-900">
                            {parseFloat(course.price).toLocaleString("sr-RS")} €
                        </p>

                        <button
                            type="button"
                            className="w-full rounded-full bg-[#d8c6ff] px-6 py-3.5 font-semibold text-zinc-900 transition hover:bg-[#c9b0ff] hover:shadow-md"
                        >
                            Kupi kurs
                        </button>

                        <p className="mt-3 text-center text-xs text-zinc-500">
                            Tvoj sledeći korak ka učenju španskog.
                        </p>

                        {course.ebook_pdf && (
                            <div className="mt-6 border-t border-zinc-200 pt-6">
                                <p className="mb-2 font-semibold text-zinc-900">
                                    E-book uz kurs
                                </p>

                                <p className="mb-4 text-sm leading-6 text-zinc-600">
                                    Dodatni materijal za učenje i vežbanje.
                                </p>

                                <p className="mb-4 text-xl font-semibold text-zinc-900">
                                    {parseFloat(course.ebook_price).toLocaleString("sr-RS")} €
                                </p>

                                <a
                                    href={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${course.ebook_pdf}`}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="block rounded-full border border-zinc-300 px-5 py-3 text-center text-sm font-medium text-zinc-700 transition hover:bg-zinc-50"
                                >
                                    Pogledaj e-book
                                </a>
                            </div>
                        )}
                    </div>
                </aside>
            </div>
        </main>
    );
}
