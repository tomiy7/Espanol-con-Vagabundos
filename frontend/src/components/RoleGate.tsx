"use client";

// Prikazuje sadrzaj samo korisnicima sa dozvoljenom rolom.
// Ovo je samo za prikaz - pravu zastitu drze backend endpointi ([Authorize(Roles = ...)]).

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAuth } from "../hooks/useAuth";
import type { Role } from "../lib/api/auth";

export default function RoleGate({
    roles,
    children,
}: {
    roles: Role[];
    children: ReactNode;
}) {
    const router = useRouter();
    const { user, isAuthenticated, loading } = useAuth();

    useEffect(() => {
        if (!loading && !isAuthenticated) {
            router.push("/login");
        }
    }, [loading, isAuthenticated, router]);

    if (loading || !isAuthenticated) {
        return (
            <div className="flex flex-1 items-center justify-center py-24 text-zinc-600">
                Učitavanje...
            </div>
        );
    }

    if (!roles.includes(user?.role as Role)) {
        return (
            <main className="flex flex-1 items-center justify-center px-6 py-24">
                <div className="max-w-md rounded-3xl border border-zinc-200 bg-white p-8 text-center shadow-sm">
                    <h1 className="text-xl font-bold text-zinc-900">Nemaš pristup ovoj stranici</h1>
                    <p className="mt-2 text-sm leading-6 text-zinc-600">
                        Ova stranica je dostupna samo osoblju škole.
                    </p>
                    <Link
                        href="/"
                        className="mt-6 inline-flex rounded-full bg-[#d8c6ff] px-5 py-3 text-sm font-semibold text-zinc-900 transition hover:bg-[#c9b0ff]"
                    >
                        Nazad na početnu
                    </Link>
                </div>
            </main>
        );
    }

    return <>{children}</>;
}
