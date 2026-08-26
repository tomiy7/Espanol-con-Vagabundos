"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type User = {
    id: string;
    username: string;
    email: string;
    phone?: string;
    role: string;
    firstName?: string;
    lastName?: string;
    createdAt: string;
};

export default function MyAccountPage() {
    const router = useRouter();

    const [user, setUser] = useState<User | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        async function getUser() {
            const token = localStorage.getItem("accessToken");

            if (!token) {
                router.push("/login");
                return;
            }

            try {
                const res = await fetch(
                    `${process.env.NEXT_PUBLIC_AUTH_URL}/auth/me`,
                    {
                        headers: {
                            Authorization: `Bearer ${token}`,
                        },
                    }
                );

                if (!res.ok) {
                    setError("Nije moguće učitati podatke o korisniku.");
                    return;
                }

                const data = await res.json();
                setUser(data);
            } catch {
                setError("Greška pri povezivanju sa serverom.");
            } finally {
                setLoading(false);
            }
        }

        getUser();
    }, [router]);

    function handleLogout() {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("refreshToken");

        window.dispatchEvent(new Event("auth-change"));

        router.push("/");
    }

    if (loading) {
        return (
            <div className="flex flex-1 justify-center py-16">
                Učitavanje...
            </div>
        );
    }

    if (error) {
        return (
            <div className="flex flex-1 justify-center py-16 text-red-600">
                {error}
            </div>
        );
    }

    return (
        <div className="flex flex-1 flex-col bg-white">

            {/* HERO */}
            <section className="w-full bg-[#bdebff]">
                <div className="mx-auto w-full max-w-6xl px-6 py-14">

                    <span className="mb-4 inline-block rounded-full bg-white/70 px-4 py-2 text-sm font-medium text-zinc-700">
                        Tvoj prostor za učenje
                    </span>

                    <h1 className="mb-3 text-4xl font-semibold text-zinc-900">
                        Zdravo, {user?.firstName || user?.username}! 👋
                    </h1>

                    <p className="text-zinc-700">
                        Ovde možeš videti svoje podatke i kurseve kojima imaš pristup.
                    </p>

                </div>
            </section>


            {/* CONTENT */}
            <main className="mx-auto w-full max-w-6xl px-6 py-16">

                <div className="grid gap-8 md:grid-cols-2">

                    {/* USER INFO */}
                    {user && (
                        <section className="rounded-3xl border border-zinc-200 bg-white p-8 shadow-sm">

                            <div className="mb-6 flex items-center gap-4">

                                <div className="flex h-12 w-12 items-center justify-center rounded-full bg-[#d8c6ff] font-semibold text-zinc-800">
                                    {(user.firstName || user.username)
                                        ?.charAt(0)
                                        .toUpperCase()}
                                </div>

                                <div>
                                    <h2 className="text-xl font-semibold text-zinc-900">
                                        Podaci o korisniku
                                    </h2>

                                    <p className="text-sm text-zinc-500">
                                        Tvoji osnovni podaci
                                    </p>
                                </div>

                            </div>


                            <div className="flex flex-col gap-4">

                                <div className="rounded-2xl bg-zinc-50 p-4">
                                    <p className="mb-1 text-xs font-medium text-zinc-400">
                                        KORISNIČKO IME
                                    </p>

                                    <p className="font-medium text-zinc-800">
                                        {user.username}
                                    </p>
                                </div>


                                <div className="rounded-2xl bg-zinc-50 p-4">
                                    <p className="mb-1 text-xs font-medium text-zinc-400">
                                        EMAIL
                                    </p>

                                    <p className="font-medium text-zinc-800">
                                        {user.email}
                                    </p>
                                </div>


                                {(user.firstName || user.lastName) && (
                                    <div className="rounded-2xl bg-zinc-50 p-4">
                                        <p className="mb-1 text-xs font-medium text-zinc-400">
                                            IME I PREZIME
                                        </p>

                                        <p className="font-medium text-zinc-800">
                                            {user.firstName} {user.lastName}
                                        </p>
                                    </div>
                                )}


                                {user.phone && (
                                    <div className="rounded-2xl bg-zinc-50 p-4">
                                        <p className="mb-1 text-xs font-medium text-zinc-400">
                                            TELEFON
                                        </p>

                                        <p className="font-medium text-zinc-800">
                                            {user.phone}
                                        </p>
                                    </div>
                                )}

                            </div>

                        </section>
                    )}


                    {/* MY COURSES */}
                    <section className="rounded-3xl bg-[#fff3c2] p-8">

                        <span className="mb-4 inline-block rounded-full bg-white/70 px-4 py-2 text-xs font-medium text-zinc-700">
                            Moje učenje
                        </span>

                        <h2 className="mb-2 text-xl font-semibold text-zinc-900">
                            Moji kursevi
                        </h2>

                        <p className="mb-6 text-zinc-700">
                            Ovde će se prikazivati svi kursevi koje si kupila i kojima imaš pristup.
                        </p>


                        {/* EMPTY STATE */}
                        <div className="rounded-3xl bg-white/80 p-6">

                            <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-[#d8c6ff]">
                                📚
                            </div>

                            <h3 className="mb-2 font-semibold text-zinc-900">
                                Još nemaš kupljenih kurseva
                            </h3>

                            <p className="mb-5 text-sm text-zinc-600">
                                Kada kupiš svoj prvi kurs, on će se pojaviti upravo ovde.
                            </p>

                            <button
                                onClick={() => router.push("/courses")}
                                className="rounded-full bg-[#d8c6ff] px-5 py-3 text-sm font-medium text-zinc-900 transition hover:scale-[1.02] hover:shadow-md"
                            >
                                Istraži kurseve →
                            </button>

                        </div>

                    </section>

                </div>


                {/* LOGOUT */}

                <div className="mt-12 border-t border-zinc-200 pt-8">

                    <button
                        onClick={handleLogout}
                        className="rounded-full border border-zinc-300 bg-white px-6 py-3 font-medium text-zinc-700 transition hover:bg-zinc-100"
                    >
                        Odjavi se
                    </button>

                </div>

            </main>

        </div>
    );
}