"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

type User = {
    id: string;
    username: string;
    email: string;
    phone?: string;
    role: string;
    firstName?: string;
    lastName?: string;
    gender?: string;
    createdAt: string;
};

export default function MyAccountPage() {
    const router = useRouter();
    const [user, setUser] = useState<User | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        let isMounted = true;

        async function getUser() {
            const token = localStorage.getItem("accessToken");

            if (!token) {
                router.push("/login");
                return;
            }

            try {
                const res = await fetch(`${process.env.NEXT_PUBLIC_AUTH_URL}/auth/me`, {
                    headers: { Authorization: `Bearer ${token}` },
                });

                if (!res.ok) {
                    if (isMounted) setError("Nije moguće učitati podatke o korisniku.");
                    return;
                }

                const data = await res.json();
                if (isMounted) setUser(data);
            } catch {
                if (isMounted) setError("Greška pri povezivanju sa serverom.");
            } finally {
                if (isMounted) setLoading(false);
            }
        }

        getUser();
        return () => { isMounted = false; };
    }, [router]);

    function getVagabundoName() {
        const name = user?.firstName || user?.username || "";
        const gender = user?.gender?.trim().toLowerCase();
        if (["female", "žensko", "zensko"].includes(gender || "")) return `Vagabunda ${name}`;
        if (["male", "muško", "musko"].includes(gender || "")) return `Vagabundo ${name}`;
        return name;
    }

    function handleLogout() {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("refreshToken");
        window.dispatchEvent(new Event("auth-change"));
        router.push("/");
    }

    if (loading) {
        return <div className="flex flex-1 items-center justify-center py-24 text-zinc-600">Učitavanje tvog naloga...</div>;
    }

    if (error) {
        return (
            <main className="flex flex-1 items-center justify-center px-6 py-24">
                <div className="max-w-md rounded-3xl border border-red-200 bg-red-50 p-8 text-center text-red-700">
                    <p>{error}</p>
                    <button onClick={() => window.location.reload()} className="mt-5 rounded-full bg-white px-5 py-2.5 font-medium shadow-sm">Pokušaj ponovo</button>
                </div>
            </main>
        );
    }

    return (
        <main className="flex-1 bg-[#fbfaff]">
            <section className="bg-[#bdebff]">
                <div className="mx-auto flex max-w-7xl flex-col gap-6 px-6 py-10 sm:px-8 sm:py-14 md:flex-row md:items-center md:justify-between">
                    <div>
                        <span className="mb-4 inline-flex rounded-full bg-white/70 px-4 py-2 text-sm font-medium text-zinc-700">Tvoj prostor za učenje</span>
                        <h1 className="text-3xl font-bold tracking-tight text-zinc-900 sm:text-4xl">Zdravo{user ? `, ${getVagabundoName()}` : ""}! 👋</h1>
                        <p className="mt-3 max-w-2xl text-zinc-700">Ovde ćeš moći da pratiš svoje prijave za kurseve i aktivnosti na njima.</p>
                    </div>
                    <Link href="/courses" className="inline-flex w-fit items-center justify-center rounded-full bg-white px-6 py-3 font-semibold text-zinc-900 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md">
                        Pogledaj kurseve <span className="ml-2">→</span>
                    </Link>
                </div>
            </section>

            <div className="mx-auto max-w-7xl px-6 py-10 sm:px-8 sm:py-12">
                <section aria-labelledby="my-courses-heading">
                    <div>
                        <p className="text-sm font-semibold uppercase tracking-[0.16em] text-purple-700">Tvoje učenje</p>
                        <h2 id="my-courses-heading" className="mt-2 text-2xl font-bold text-zinc-900 sm:text-3xl">Moji kursevi</h2>
                    </div>

                    <div className="mt-6 rounded-3xl border border-dashed border-zinc-300 bg-white px-6 py-12 text-center">
                        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-[#fff3c2] text-2xl" aria-hidden="true">📚</div>
                        <h3 className="mt-4 text-lg font-semibold text-zinc-900">Prijavljeni kursevi će se prikazivati ovde</h3>
                        <p className="mx-auto mt-2 max-w-xl text-sm leading-6 text-zinc-600">
                            Trenutno još nije dostupna veza sa bazom za prikaz tvojih prijava, dodeljenih profesora i napretka kroz lekcije. Ovi podaci će biti prikazani kada odgovarajući backend deo bude implementiran.
                        </p>
                        <Link href="/courses" className="mt-5 inline-flex rounded-full bg-[#d8c6ff] px-5 py-3 text-sm font-semibold text-zinc-900 transition hover:bg-[#c9b0ff]">
                            Pogledaj katalog kurseva →
                        </Link>
                    </div>
                </section>

                {user && (
                    <section className="mt-12 rounded-3xl border border-zinc-100 bg-white p-6 shadow-sm sm:p-8">
                        <div className="flex flex-col justify-between gap-5 sm:flex-row sm:items-center">
                            <div className="flex items-center gap-4">
                                <div className="flex h-14 w-14 items-center justify-center rounded-full bg-[#d8c6ff] text-xl font-bold text-zinc-800">
                                    {(user.firstName || user.username || "K").charAt(0).toUpperCase()}
                                </div>
                                <div>
                                    <h2 className="text-xl font-bold text-zinc-900">Moji podaci</h2>
                                    <p className="mt-1 text-sm text-zinc-500">{user.firstName || user.username} {user.lastName || ""}</p>
                                </div>
                            </div>
                            <button onClick={handleLogout} className="w-fit rounded-full border border-zinc-200 px-5 py-2.5 text-sm font-semibold text-zinc-700 transition hover:border-red-200 hover:bg-red-50 hover:text-red-700">
                                Odjavi se
                            </button>
                        </div>

                        <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                            <div className="rounded-2xl bg-[#fbfaff] p-4">
                                <p className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Korisničko ime</p>
                                <p className="mt-1 font-medium text-zinc-800">{user.username}</p>
                            </div>
                            <div className="rounded-2xl bg-[#fbfaff] p-4">
                                <p className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Email</p>
                                <p className="mt-1 break-all font-medium text-zinc-800">{user.email}</p>
                            </div>
                            {(user.firstName || user.lastName) && (
                                <div className="rounded-2xl bg-[#fbfaff] p-4">
                                    <p className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Ime i prezime</p>
                                    <p className="mt-1 font-medium text-zinc-800">{user.firstName} {user.lastName}</p>
                                </div>
                            )}
                            {user.phone && (
                                <div className="rounded-2xl bg-[#fbfaff] p-4">
                                    <p className="text-xs font-semibold uppercase tracking-wider text-zinc-400">Telefon</p>
                                    <p className="mt-1 font-medium text-zinc-800">{user.phone}</p>
                                </div>
                            )}
                        </div>
                    </section>
                )}
            </div>
        </main>
    );
}
