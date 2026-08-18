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
        <div className="flex flex-1 justify-center bg-zinc-50 px-6 py-16">
            <div className="w-full max-w-2xl">
                <h1 className="mb-8 text-3xl font-semibold">
                    Moj nalog
                </h1>

                {user && (
                    <div className="mb-10 rounded-lg border border-zinc-200 bg-white p-6">
                        <h2 className="mb-4 text-xl font-semibold">
                            Podaci o korisniku
                        </h2>

                        <div className="flex flex-col gap-2">
                            <p>
                                <strong>Korisničko ime:</strong>{" "}
                                {user.username}
                            </p>

                            <p>
                                <strong>Email:</strong>{" "}
                                {user.email}
                            </p>

                            {user.phone && (
                                <p>
                                    <strong>Telefon:</strong>{" "}
                                    {user.phone}
                                </p>
                            )}

                            <p>
                                <strong>Uloga:</strong>{" "}
                                {user.role}
                            </p>

                            {(user.firstName || user.lastName) && (
                                <p>
                                    <strong>Ime i prezime:</strong>{" "}
                                    {user.firstName} {user.lastName}
                                </p>
                            )}
                        </div>
                    </div>
                )}

                <div className="mb-10 rounded-lg border border-zinc-200 bg-white p-6">
                    <h2 className="mb-2 text-xl font-semibold">
                        Moji kursevi
                    </h2>

                    <p className="text-zinc-600">
                        Tvoji kupljeni kursevi će biti prikazani ovde.
                    </p>
                </div>

                <button
                    onClick={handleLogout}
                    className="rounded-full bg-black px-6 py-2 font-medium text-white"
                >
                    Odjavi se
                </button>
            </div>
        </div>
    );
}