"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useAuth } from "../src/hooks/useAuth";

export default function Header() {
    const router = useRouter();
    const { user, isAuthenticated, loading, logout } = useAuth();

    const [isMenuOpen, setIsMenuOpen] = useState(false);

    function handleLogout() {
        logout();
        setIsMenuOpen(false);
        router.push("/");
    }

    return (
        <header className="w-full bg-[#d8c6ff]">
            <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">

                <Link
                    href="/"
                    className="text-lg font-semibold text-zinc-900"
                >
                    Español con Vagabundos
                </Link>

                <nav className="flex items-center gap-6">

                    <Link
                        href="/courses"
                        className="text-zinc-700 transition hover:text-zinc-950"
                    >
                        Kursevi
                    </Link>

                    {loading ? null : isAuthenticated ? (
                        <div className="relative">

                            <button
                                onClick={() =>
                                    setIsMenuOpen(!isMenuOpen)
                                }
                                className="flex items-center gap-2 text-zinc-700 transition hover:text-zinc-950"
                            >
                                <svg
                                    xmlns="http://www.w3.org/2000/svg"
                                    fill="currentColor"
                                    viewBox="0 0 24 24"
                                    className="h-5 w-5"
                                >
                                    <path d="M12 12a5 5 0 1 0 0-10 5 5 0 0 0 0 10Zm0 2c-5.33 0-8 2.67-8 6v2h16v-2c0-3.33-2.67-6-8-6Z" />
                                </svg>

                                <span>
                                    {user?.firstName ||
                                        user?.username ||
                                        "Moj nalog"}
                                </span>

                                <span className="text-xs">
                                    {isMenuOpen ? "▲" : "▼"}
                                </span>
                            </button>

                            {isMenuOpen && (
                                <div className="absolute right-0 top-full z-50 mt-3 w-44 rounded-xl border border-[#d8c6ff] bg-white shadow-lg">

                                    <Link
                                        href="/my-account"
                                        onClick={() =>
                                            setIsMenuOpen(false)
                                        }
                                        className="block px-4 py-3 text-sm text-zinc-700 transition hover:bg-[#fff3c2]"
                                    >
                                        Moj nalog
                                    </Link>

                                    <div className="border-t border-zinc-100" />

                                    <button
                                        onClick={handleLogout}
                                        className="block w-full px-4 py-3 text-left text-sm text-zinc-700 transition hover:bg-[#fff3c2]"
                                    >
                                        Odjavi se
                                    </button>

                                </div>
                            )}
                        </div>
                    ) : (
                        <>
                            <Link
                                href="/login"
                                className="text-zinc-700 transition hover:text-zinc-950"
                            >
                                Prijava
                            </Link>

                            <Link
                                href="/register"
                                className="text-zinc-700 transition hover:text-zinc-950"
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