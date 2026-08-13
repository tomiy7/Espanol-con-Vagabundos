import Link from "next/link";

type Course = {
  id: number;
  title: string;
  description: string;
  price: string;
  thumbnail: string;
};

async function getCourses(): Promise<Course[]> {
  const res = await fetch(`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/courses`, {
    cache: "no-store",
  });
  const json = await res.json();
  return json.data;
}

export default async function Home() {
  const courses = await getCourses();

  return (
      <div className="flex flex-col flex-1 items-center bg-zinc-50 font-sans dark:bg-black">
        <main className="w-full max-w-3xl py-16 px-6">
          <h1 className="text-3xl font-semibold mb-8 text-black dark:text-zinc-50">
            Svi naši kursevi
          </h1>

          <div className="flex flex-col gap-4">
            {courses.map((course) => (
                <Link
                    key={course.id}
                    href={`/courses/${course.id}`}
                    className="rounded-lg border border-zinc-200 dark:border-zinc-800 p-6 hover:bg-zinc-100 dark:hover:bg-zinc-900 transition-colors"
                >
                  {course.thumbnail && (
                      <img
                          src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${course.thumbnail}`}
                          alt={course.title}
                          className="w-full rounded-md mb-4"
                          // className="w-full max-w-xs rounded-md mb-4 mx-auto"
                      />
                  )}
                  <h2 className="text-xl font-medium text-black dark:text-zinc-50">
                    {course.title}
                  </h2>
                  <p className="text-zinc-600 dark:text-zinc-400 mt-2">
                    {course.description}
                  </p>
                  <p className="text-zinc-950 dark:text-zinc-50 font-medium mt-4">
                    {course.price} €
                  </p>
                </Link>
            ))}
          </div>
        </main>
      </div>
  );
}