
import { notFound } from "next/navigation";
import LessonAccordion from "./LessonAccordion";

export type Lesson = {
    id: string;
    title: string;
    order: number;
    recording_url: string | null;
    course_id: string;
    is_visible: boolean;
    is_published: boolean;
};

async function getLesson(id: string): Promise<Lesson | null> {
    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/lessons/${id}`,
        { cache: "no-store" }
    );

    if (!res.ok) return null;

    const json = await res.json();
    return json.data ?? null;
}

async function getLessons(courseId: string): Promise<Lesson[]> {
    const params = new URLSearchParams({
        "filter[course_id][_eq]": courseId,
        "filter[is_visible][_eq]": "true",
        "filter[is_published][_eq]": "true",
        sort: "order",
    });

    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/lessons?${params}`,
        { cache: "no-store" }
    );

    if (!res.ok) return [];

    const json = await res.json();
    return json.data ?? [];
}

export default async function LessonPage({
                                             params,
                                         }: {
    params: Promise<{ id: string; lessonId: string }>;
}) {
    const { id, lessonId } = await params;
    const lesson = await getLesson(lessonId);

    if (
        !lesson ||
        !lesson.is_visible ||
        !lesson.is_published ||
        lesson.course_id !== id
    ) {
        notFound();
    }

    const lessons = await getLessons(id);

    return (
        <main className="min-h-screen flex-1 bg-zinc-50 px-4 py-10 sm:px-6 sm:py-14">
            <div className="mx-auto w-full max-w-3xl">
                <p className="mb-2 text-sm font-semibold uppercase tracking-widest text-purple-600">
                    Español con Vagabundos
                </p>

                <h1 className="mb-2 text-3xl font-bold text-zinc-900 sm:text-4xl">
                    Lekcije
                </h1>

                <p className="mb-8 text-zinc-600">
                    Izaberi lekciju da pogledaš snimak i materijale.
                </p>

                <LessonAccordion
                    lessons={lessons}
                    courseId={id}
                    currentLessonId={lessonId}
                />
            </div>
        </main>
    );
}
