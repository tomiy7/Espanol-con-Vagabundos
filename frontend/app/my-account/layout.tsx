import type { Metadata } from "next";
import type { ReactNode } from "react";

export const metadata: Metadata = {
    title: "Moj nalog",
};

export default function MyAccountLayout({ children }: { children: ReactNode }) {
    return <>{children}</>;
}