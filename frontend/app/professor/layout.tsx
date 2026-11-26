import type { Metadata } from "next";
import type { ReactNode } from "react";
import RoleGate from "../../src/components/RoleGate";
import PanelShell from "../../src/components/PanelShell";

export const metadata: Metadata = {
    title: "Profesorski panel",
};

const NAV = [{ href: "/professor/groups", label: "Moje grupe" }];

export default function ProfessorLayout({ children }: { children: ReactNode }) {
    return (
        <RoleGate roles={["professor"]}>
            <PanelShell title="Profesorski panel" nav={NAV}>
                {children}
            </PanelShell>
        </RoleGate>
    );
}
