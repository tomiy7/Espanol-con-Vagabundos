
import Link from "next/link";

type Course = {
    id: string;
    name: string;
    price: string;
    is_published: boolean;
};

async function getCourses(): Promise<Course[]> {
    const params = new URLSearchParams({
        "filter[is_published][_eq]": "true",
        fields: "id,name,price,is_published",
        sort: "name",
        limit: "-1",
    });

    const res = await fetch(
        `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/courses?${params}`,
        {
            cache: "no-store",
        }
    );

    if (!res.ok) {
        throw new Error("Greška prilikom učitavanja kurseva.");
    }

    const json = await res.json();

    return json.data ?? [];
}

export default async function CoursesPage() {
    const courses = await getCourses();

    return (
        <main className="flex-1 bg-zinc-50 px-4 py-10 sm:px-6 sm:py-16">
            <div className="mx-auto w-full max-w-5xl">

                <div className="mb-10">
                    <p className="mb-3 text-sm font-semibold uppercase tracking-widest text-purple-600">
                        Español con Vagabundos
                    </p>

                    <h1 className="mb-3 text-4xl font-bold text-zinc-900">
                        Naši kursevi
                    </h1>

                    <p className="text-zinc-600">
                        Izaberi kurs i započni svoje putovanje kroz španski jezik.
                    </p>
                </div>

                {courses.length > 0 ? (
                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                        {courses.map((course) => (
                            <Link
                                key={course.id}
                                href={`/courses/${course.id}`}
                                className="group flex flex-col rounded-3xl border border-zinc-200 bg-white p-6 transition hover:border-purple-300 hover:shadow-md"
                            >
                                <p className="mb-3 text-xs font-semibold uppercase tracking-widest text-purple-600">
                                    Español con Vagabundos
                                </p>

                                <h2 className="mb-4 text-xl font-bold text-zinc-900 transition group-hover:text-purple-700">
                                    {course.name}
                                </h2>

                                <div className="mt-auto flex items-center justify-between border-t border-zinc-100 pt-5">
                                    <span className="text-xl font-bold text-zinc-900">
                                        {parseFloat(course.price).toLocaleString("sr-RS")} €
                                    </span>

                                    <span className="font-medium text-purple-700">
                                        Pogledaj kurs →
                                    </span>
                                </div>
                            </Link>
                        ))}
                    </div>
                ) : (
                    <div className="rounded-2xl border border-dashed border-zinc-300 bg-white p-8 text-center text-zinc-500">
                        Trenutno nema dostupnih kurseva.
                    </div>
                )}
            </div>
        </main>
    );
}
