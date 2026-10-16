import type { Metadata } from "next";
import type { ReactNode } from "react";

// Naslov taba = ime kursa iz Directus-a (dinamicki, nista hardkodirano)
export async function generateMetadata({
                                           params,
                                       }: {
    params: Promise<{ id: string }>;
}): Promise<Metadata> {
    const { id } = await params;

    try {
        const res = await fetch(
            `${process.env.NEXT_PUBLIC_DIRECTUS_URL}/items/courses/${id}?fields=name`,
            { cache: "no-store" }
        );

        if (!res.ok) {
            return { title: "Kurs" };
        }

        const json = await res.json();

        return { title: json.data?.name ?? "Kurs" };
    } catch {
        return { title: "Kurs" };
    }
}

export default function CourseLayout({ children }: { children: ReactNode }) {
    return <>{children}</>;
}