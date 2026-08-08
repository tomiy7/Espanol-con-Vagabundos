type Course = {
  id: number;
  title: string;
  description: string;
  price: string;
};

async function getCourses(): Promise<Course[]> {
  const res = await fetch("http://localhost:8055/items/courses", {
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
            Naši kursevi
          </h1>

          <div className="flex flex-col gap-4">
            {courses.map((course) => (
                <div
                    key={course.id}
                    className="rounded-lg border border-zinc-200 dark:border-zinc-800 p-6"
                >
                  <h2 className="text-xl font-medium text-black dark:text-zinc-50">
                    {course.title}
                  </h2>
                  <p className="text-zinc-600 dark:text-zinc-400 mt-2">
                    {course.description}
                  </p>
                  <p className="text-zinc-950 dark:text-zinc-50 font-medium mt-4">
                    {course.price} €
                  </p>
                </div>
            ))}
          </div>
        </main>
      </div>
  );
}