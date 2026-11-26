"use client";

// Zajednicki okvir za admin i profesorski panel: naslov, meni i sadrzaj.

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

export type PanelNavItem = { href: string; label: string };

export default function PanelShell({
    title,
    nav,
    children,
}: {
    title: string;
    nav: PanelNavItem[];
    children: ReactNode;
}) {
    const pathname = usePathname();

    return (
        <main className="flex-1 bg-[#fbfaff]">
            <section className="bg-[#bdebff]">
                <div className="mx-auto max-w-6xl px-6 py-8 sm:py-10">
                    <span className="mb-3 inline-flex rounded-full bg-white/70 px-4 py-2 text-sm font-medium text-zinc-700">
                        Español con Vagabundos
                    </span>
                    <h1 className="text-3xl font-bold tracking-tight text-zinc-900">{title}</h1>

                    {nav.length > 1 && (
                        <nav className="mt-6 flex flex-wrap gap-2" aria-label={`${title} meni`}>
                            {nav.map((item) => {
                                const active = pathname.startsWith(item.href);

                                return (
                                    <Link
                                        key={item.href}
                                        href={item.href}
                                        aria-current={active ? "page" : undefined}
                                        className={`rounded-full px-5 py-2.5 text-sm font-semibold transition ${
                                            active
                                                ? "bg-zinc-900 text-white"
                                                : "bg-white/70 text-zinc-800 hover:bg-white"
                                        }`}
                                    >
                                        {item.label}
                                    </Link>
                                );
                            })}
                        </nav>
                    )}
                </div>
            </section>

            <div className="mx-auto max-w-6xl px-6 py-8 sm:py-10">{children}</div>
        </main>
    );
}
