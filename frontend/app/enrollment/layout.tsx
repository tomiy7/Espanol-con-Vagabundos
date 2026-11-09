import type { Metadata } from "next";
import type { ReactNode } from "react";

export const metadata: Metadata = {
    title: "Prijava na kurs",
    description: "Izaberi način pohađanja kursa španskog jezika.",
};

export default function EnrollmentLayout({ children }: { children: ReactNode }) {
    return <>{children}</>;
}
