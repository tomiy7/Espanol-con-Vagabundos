"use client";

import type { ReactNode } from "react";
import PanelShell from "../../src/components/PanelShell";

const NAV = [
    { href: "/admin/payments", label: "Uplate" },
    { href: "/admin/groups", label: "Grupe" },
    { href: "/admin/users", label: "Korisnici" },
];

export default function AdminShell({ children }: { children: ReactNode }) {
    return (
        <PanelShell title="Admin panel" nav={NAV}>
            {children}
        </PanelShell>
    );
}
