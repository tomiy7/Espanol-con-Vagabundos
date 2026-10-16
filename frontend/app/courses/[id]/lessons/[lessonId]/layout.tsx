import type { Metadata } from "next";
import type { ReactNode } from "react";

// Naslov taba = naslov lekcije iz Directus-a (dinamicki, nista hardkodirano)
export async function generateMetadata({
                                           params,
                                       }: {
    params: Promise<{ id: string; lessonId: string }>;
}): Promise<Metadata> {
    const { lessonId } = await params;

    try {
        const res = await fetch(
            `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/lessons/${lessonId}?fields=title`,
            { cache: "no-store" }
        );

        if (!res.ok) {
            return { title: "Lekcija" };
        }

        const json = await res.json();

        return { title: json.data?.title ?? "Lekcija" };
    } catch {
        return { title: "Lekcija" };
    }
}

export default function LessonLayout({ children }: { children: ReactNode }) {
    return <>{children}</>;
}