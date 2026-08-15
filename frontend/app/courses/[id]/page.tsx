import Link from "next/link";
import { notFound } from "next/navigation";

type Lesson = {
    id: string;
    title: string;
    order: number;
    recording_url: string;
};

type Course = {
    id: string;
    name: string;
    description: string;
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
                        Preuzmi e-book uz kurs ({course.ebook_price} €)
                    </a>
                )}

                <p className="text-zinc-600 dark:text-zinc-400 mb-8">
                    {course.description}
                </p>

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