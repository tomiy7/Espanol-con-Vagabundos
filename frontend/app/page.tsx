type ListItem = {
  content: string;
  items: ListItem[];
};

type Block = {
  type: string;
  data: {
    text?: string;
    level?: number;
    items?: string[] | ListItem[];
    style?: string;
    caption?: string;
    file?: { fileId: string };
  };
};

type Homepage = {
  hero_title: string;
  hero_subtitle: string;
  hero_image: string;
  content: {
    blocks?: Block[];
  } | null;
};

async function getHomepage(): Promise<Homepage> {
  const res = await fetch(`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/homepage`, {
    cache: "no-store",
  });
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

function renderContent(content: Homepage["content"]) {
  if (!content || !content.blocks) return null;

  return content.blocks.map((block, i) => {
    if (block.type === "paragraph") {
      return (
          <p
              key={i}
              className="mb-4"
              dangerouslySetInnerHTML={{ __html: block.data.text || "" }}
          />
      );
    }
    if (block.type === "header") {
      return (
          <h2 key={i} className="text-2xl font-medium mb-3 mt-6">
            {block.data.text}
          </h2>
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
          <figure key={i} className="mb-6">
            <img
                src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${block.data.file.fileId}`}
                alt={block.data.caption || ""}
                className="rounded-full w-48 h-48 object-cover mx-auto"
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

export default async function Home() {
  const homepage = await getHomepage();

  return (
      <div className="flex flex-col flex-1 bg-zinc-50 dark:bg-black">
        <section className="flex flex-col items-center text-center px-6 py-24">
          {homepage.hero_image && (
              <img
                  src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${homepage.hero_image}`}
                  alt={homepage.hero_title}
                  className="w-full max-w-md rounded-md mb-8"
              />
          )}
          <h1 className="text-4xl font-semibold mb-4 text-black dark:text-zinc-50">
            {homepage.hero_title}
          </h1>
          <p className="text-zinc-600 dark:text-zinc-400 max-w-xl">
            {homepage.hero_subtitle}
          </p>
        </section>

        <section className="w-full max-w-2xl mx-auto px-6 py-12 text-zinc-700 dark:text-zinc-300">
          {renderContent(homepage.content)}
        </section>
      </div>
  );
}

// import Link from "next/link";
//
// type Course = {
//   id: number;
//   title: string;
//   description: string;
//   price: string;
//   thumbnail: string;
// };
//
// async function getCourses(): Promise<Course[]> {
//   const res = await fetch(`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/courses`, {
//     cache: "no-store",
//   });
//   const json = await res.json();
//   return json.data;
// }
//
// export default async function Home() {
//   const courses = await getCourses();
//
//   return (
//       <div className="flex flex-col flex-1 items-center bg-zinc-50 font-sans dark:bg-black">
//         <main className="w-full max-w-3xl py-16 px-6">
//           <h1 className="text-3xl font-semibold mb-8 text-black dark:text-zinc-50">
//             Svi naši kursevi
//           </h1>
//
//           <div className="flex flex-col gap-4">
//             {courses.map((course) => (
//                 <Link
//                     key={course.id}
//                     href={`/courses/${course.id}`}
//                     className="rounded-lg border border-zinc-200 dark:border-zinc-800 p-6 hover:bg-zinc-100 dark:hover:bg-zinc-900 transition-colors"
//                 >
//                   {course.thumbnail && (
//                       <img
//                           src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${course.thumbnail}`}
//                           alt={course.title}
//                           className="w-full rounded-md mb-4"
//                           // className="w-full max-w-xs rounded-md mb-4 mx-auto"
//                       />
//                   )}
//                   <h2 className="text-xl font-medium text-black dark:text-zinc-50">
//                     {course.title}
//                   </h2>
//                   <p className="text-zinc-600 dark:text-zinc-400 mt-2">
//                     {course.description}
//                   </p>
//                   <p className="text-zinc-950 dark:text-zinc-50 font-medium mt-4">
//                     {course.price} €
//                   </p>
//                 </Link>
//             ))}
//           </div>
//         </main>
//       </div>
//   );
// }