import Link from "next/link";

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
  const res = await fetch(
      `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/homepage`,
      {
        cache: "no-store",
      }
  );

  const json = await res.json();
  return json.data;
}

function renderNestedList(items: ListItem[]) {
  return (
      <ul className="mt-4 space-y-2">
        {items.map((item, i) => (
            <li
                key={i}
                className="flex gap-3 text-[16px] leading-7 text-zinc-700"
            >
              <span className="mt-2.5 h-2 w-2 shrink-0 rounded-full bg-[#fff3c2]" />

              <div>
                {item.content}

                {item.items &&
                    item.items.length > 0 &&
                    renderNestedList(item.items)}
              </div>
            </li>
        ))}
      </ul>
  );
}

function renderContent(content: Homepage["content"]) {
  if (!content || !content.blocks) return null;

  const blocks = content.blocks;
  const renderedBlocks = [];

  let personIndex = 0;

  for (let i = 0; i < blocks.length; i++) {
    const block = blocks[i];
    const nextBlock = blocks[i + 1];

    /*
      PERSON SECTION

      Ako imamo paragraph + image ili image + paragraph,
      spajamo ih u jedan red.
    */

    const isParagraphThenImage =
        block.type === "paragraph" &&
        nextBlock?.type === "image" &&
        nextBlock.data.file;

    const isImageThenParagraph =
        block.type === "image" &&
        block.data.file &&
        nextBlock?.type === "paragraph";

    if (isParagraphThenImage || isImageThenParagraph) {
      const isEven = personIndex % 2 === 0;

      const imageBlock =
          block.type === "image" ? block : nextBlock;

      const textBlock =
          block.type === "paragraph" ? block : nextBlock;

      const image = (
          <div className="flex w-full justify-center md:w-[220px]">
            <img
                src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${imageBlock.data.file?.fileId}`}
                alt={imageBlock.data.caption || ""}
                className="h-32 w-32 shrink-0 rounded-full object-cover shadow-md md:h-40 md:w-40"
            />
          </div>
      );

      const text = (
          <div className="flex-1">
            <p
                className="text-[16px] leading-8 text-zinc-600"
                dangerouslySetInnerHTML={{
                  __html: textBlock.data.text || "",
                }}
            />
          </div>
      );

      renderedBlocks.push(
          <div
              key={`person-${i}`}
              className="my-14 flex flex-col items-center gap-8 md:flex-row md:items-center md:gap-12"
          >
            {isEven ? (
                <>
                  {image}
                  {text}
                </>
            ) : (
                <>
                  {text}
                  {image}
                </>
            )}
          </div>
      );

      personIndex++;

      /*
        Preskačemo sledeći blok jer smo ga već prikazali
      */
      i++;

      continue;
    }

    /*
      PARAGRAPH
    */

    if (block.type === "paragraph") {
      renderedBlocks.push(
          <p
              key={i}
              className="mb-5 text-[16px] leading-8 text-zinc-600"
              dangerouslySetInnerHTML={{
                __html: block.data.text || "",
              }}
          />
      );

      continue;
    }

    /*
      HEADER
    */

    if (block.type === "header") {
      renderedBlocks.push(
          <div key={i} className="mt-12 mb-5">
            <h2 className="inline-block rounded-full bg-[#fff3c2] px-5 py-2 text-xl font-semibold text-zinc-900">
              {block.data.text}
            </h2>
          </div>
      );

      continue;
    }

    /*
      LIST
    */

    if (
        block.type === "list" &&
        Array.isArray(block.data.items)
    ) {
      renderedBlocks.push(
          <ul key={i} className="mb-6 space-y-2">
            {(block.data.items as string[]).map((item, j) => (
                <li
                    key={j}
                    className="flex gap-3 text-[16px] leading-7 text-zinc-700"
                >
                  <span className="mt-2.5 h-2 w-2 shrink-0 rounded-full bg-[#fff3c2]" />

                  <span>{item}</span>
                </li>
            ))}
          </ul>
      );

      continue;
    }

    /*
      NESTED LIST
    */

    if (
        block.type === "nestedlist" &&
        Array.isArray(block.data.items)
    ) {
      renderedBlocks.push(
          <div key={i} className="mb-6">
            {renderNestedList(
                block.data.items as ListItem[]
            )}
          </div>
      );

      continue;
    }

    /*
      IMAGE KOJA NIJE DEO PERSON SECTION-A
    */

    if (
        block.type === "image" &&
        block.data.file
    ) {
      renderedBlocks.push(
          <figure
              key={i}
              className="my-10 flex justify-center"
          >
            <img
                src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${block.data.file.fileId}`}
                alt={block.data.caption || ""}
                className="max-h-[450px] rounded-3xl object-cover shadow-md"
            />

            {block.data.caption && (
                <figcaption className="mt-3 text-center text-sm text-zinc-500">
                  {block.data.caption}
                </figcaption>
            )}
          </figure>
      );
    }
  }

  return renderedBlocks;
}

export default async function Home() {
  const homepage = await getHomepage();

  return (
      <div className="flex flex-1 flex-col bg-white">

        {/* HERO */}

        <section className="w-full bg-[#bdebff]">
          <div className="mx-auto flex max-w-6xl flex-col items-center gap-12 px-6 py-16 md:flex-row md:py-20">

            {/* TEXT */}

            <div className="flex-1 text-center md:text-left">
            <span className="mb-5 inline-block rounded-full bg-white/70 px-4 py-2 text-sm font-medium text-zinc-700">
              Aprende español con nosotros
            </span>

              <h1 className="mb-6 text-4xl font-semibold leading-tight text-zinc-900 md:text-6xl">
                {homepage.hero_title}
              </h1>

              <p className="mb-7 max-w-xl text-lg leading-8 text-zinc-700">
                {homepage.hero_subtitle}
              </p>

              <Link
                  href="/courses"
                  className="inline-flex items-center gap-3 rounded-full bg-[#d8c6ff] px-6 py-3 font-medium text-zinc-900 transition hover:scale-[1.02] hover:shadow-md"
              >
                Istraži kurseve
                <span>→</span>
              </Link>
            </div>

            {/* HERO IMAGE */}

            {homepage.hero_image && (
                <div className="flex flex-1 justify-center">
                  <img
                      src={`${process.env.NEXT_PUBLIC_DIRECTUS_URL}/assets/${homepage.hero_image}`}
                      alt={homepage.hero_title}
                      className="aspect-square w-full max-w-[500px] rounded-full object-cover shadow-lg"
                  />
                </div>
            )}

          </div>
        </section>

        {/* CONTENT */}

        <section className="mx-auto w-full max-w-6xl px-6 py-16 md:py-20">
          <div className="mx-auto max-w-5xl">
            {renderContent(homepage.content)}
          </div>
        </section>

      </div>
  );
}