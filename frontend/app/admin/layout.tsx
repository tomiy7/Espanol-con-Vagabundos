import type { Metadata } from "next";
import type { ReactNode } from "react";
import RoleGate from "../../src/components/RoleGate";
import AdminShell from "./AdminShell";

export const metadata: Metadata = {
    title: "Admin panel",
};

export default function AdminLayout({ children }: { children: ReactNode }) {
    return (
        <RoleGate roles={["admin"]}>
            <AdminShell>{children}</AdminShell>
        </RoleGate>
    );
}
