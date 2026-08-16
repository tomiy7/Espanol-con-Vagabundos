"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "../src/hooks/useAuth";

export default function Header() {
    const router = useRouter();
    const { user, isAuthenticated, loading, logout } = useAuth();

    console.log("HEADER:", {
        user,
        isAuthenticated,
        loading,
    });

    function handleLogout() {
        logout();
        router.push("/");
    }

    return (
        <header className="w-full border-b border-zinc-200 dark:border-zinc-800">
            <div className="max-w-3xl mx-auto px-6 py-4 flex items-center justify-between">
                <Link
                    href="/"
                    className="font-semibold text-lg text-black dark:text-zinc-50"
                >
                    Español con Vagabundos
                </Link>

                <nav className="flex items-center gap-6">
                    <Link
                        href="/courses"
                        className="text-zinc-600 dark:text-zinc-400 hover:text-black dark:hover:text-zinc-50"
                    >
                        Kursevi
                    </Link>

                    {loading ? null : isAuthenticated ? (
                        <>
                            <Link
                                href="/profile"
                                className="text-zinc-600 dark:text-zinc-400 hover:text-black dark:hover:text-zinc-50"
                            >
                                {user?.firstName || user?.username || "Moj profil"}
                            </Link>

                            <button
                                onClick={handleLogout}
                                className="text-zinc-600 dark:text-zinc-400 hover:text-black dark:hover:text-zinc-50"
                            >
                                Odjavi se
                            </button>
                        </>
                    ) : (
                        <>
                            <Link
                                href="/login"
                                className="text-zinc-600 dark:text-zinc-400 hover:text-black dark:hover:text-zinc-50"
                            >
                                Prijava
                            </Link>

                            <Link
                                href="/register"
                                className="text-zinc-600 dark:text-zinc-400 hover:text-black dark:hover:text-zinc-50"
                            >
                                Registracija
                            </Link>
                        </>
                    )}
                </nav>
            </div>
        </header>
    );
}