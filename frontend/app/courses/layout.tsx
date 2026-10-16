import type { Metadata } from "next";
import type { ReactNode } from "react";

export const metadata: Metadata = {
    title: "Kursevi",
};

export default function CoursesLayout({ children }: { children: ReactNode }) {
    return <>{children}</>;
}