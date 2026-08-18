import Link from "next/link";

type Block = {
    type: string;
    data: {
        text?: string;
    };
};

type RichText = {
    blocks?: Block[];
} | null;

type Course = {
    id: string;
    name: string;
    description: RichText;
    price: string;
    thumbnail: string;
};

async function getCourses(): Promise<Course[]> {
    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/courses?filter[is_published][_eq]=true`,
        { cache: "no-store" }
    );

    const json = await res.json();
    return json.data;
}

function getPreviewText(content: RichText): string {
    if (!content || !content.blocks) return "";

    const firstParagraph = content.blocks.find(
        (b) => b.type === "paragraph"
    );

    return firstParagraph?.data.text?.replace(/&nbsp;/g, " ") || "";
}

export default async function CoursesPage() {
    const courses = await getCourses();

    return (
        <div className="flex flex-col flex-1 items-center bg-zinc-50 font-sans dark:bg-black">
            <main className="w-full max-w-3xl py-16 px-6">
                <h1 className="text-3xl font-semibold mb-8 text-black dark:text-zinc-50">
                    Svi naši kursevi
                </h1>

                <div className="flex flex-col gap-4">
                    {courses.map((course) => (
                        <div
                            key={course.id}
                            className="rounded-lg border border-zinc-200 dark:border-zinc-800 p-6"
                        >
                            <Link
                                href={`/courses/${course.id}`}
                                className="block hover:bg-zinc-100 dark:hover:bg-zinc-900 transition-colors"
                            >
                                {course.thumbnail && (
                                    <img
                                        src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${course.thumbnail}`}
                                        alt={course.name}
                                        className="w-full rounded-md mb-4"
                                    />
                                )}

                                <h2 className="text-xl font-medium text-black dark:text-zinc-50">
                                    {course.name}
                                </h2>

                                <p className="text-zinc-600 dark:text-zinc-400 mt-2">
                                    {getPreviewText(course.description)}
                                </p>
                            </Link>

                            <p className="text-zinc-950 dark:text-zinc-50 font-medium mt-4">
                                {parseFloat(course.price)} €
                            </p>

                            <button
                                className="mt-4 rounded-lg bg-black px-6 py-3 font-medium text-white hover:bg-zinc-800 transition-colors dark:bg-white dark:text-black dark:hover:bg-zinc-200"
                            >
                                Kupi kurs
                            </button>
                        </div>
                    ))}
                </div>
            </main>
        </div>
    );
}