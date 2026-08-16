"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";

export default function LoginPage() {
    const router = useRouter();
    const [form, setForm] = useState({ identifier: "", password: "" });
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);

    function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
        setForm({ ...form, [e.target.name]: e.target.value });
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        setError("");
        setLoading(true);

        try {
            const res = await fetch(
                `${process.env.NEXT_PUBLIC_AUTH_URL}/auth/login`,
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(form),
                }
            );

            const data = await res.json();

            if (!res.ok) {
                setError(
                    data.message || "Pogrešno korisničko ime ili lozinka"
                );
                return;
            }

            localStorage.setItem("accessToken", data.accessToken);
            localStorage.setItem("refreshToken", data.refreshToken);


            window.dispatchEvent(new Event("auth-change"));

            router.push("/");
        } catch {
            setError("Greška pri povezivanju sa serverom");
        } finally {
            setLoading(false);
        }
    }

    return (
        <div className="flex flex-col flex-1 items-center bg-zinc-50 dark:bg-black px-6 py-16">
            <form
                onSubmit={handleSubmit}
                className="w-full max-w-sm flex flex-col gap-4"
            >
                <h1 className="text-2xl font-semibold mb-2 text-black dark:text-zinc-50">
                    Prijava
                </h1>

                {error && (
                    <p className="text-red-600 dark:text-red-400 text-sm">
                        {error}
                    </p>
                )}

                <input
                    name="identifier"
                    placeholder="Email ili korisničko ime"
                    value={form.identifier}
                    onChange={handleChange}
                    className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                    required
                />

                <input
                    name="password"
                    type="password"
                    placeholder="Lozinka"
                    value={form.password}
                    onChange={handleChange}
                    className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                    required
                />

                <button
                    type="submit"
                    disabled={loading}
                    className="rounded-full bg-black text-white dark:bg-white dark:text-black py-2 font-medium mt-2 disabled:opacity-50"
                >
                    {loading ? "Prijavljujem..." : "Prijavi se"}
                </button>

                <p className="text-sm text-zinc-600 dark:text-zinc-400 text-center">
                    Nemaš nalog?{" "}
                    <Link
                        href="/register"
                        className="underline font-medium text-black dark:text-white"
                    >
                        Registruj se
                    </Link>
                </p>
            </form>
        </div>
    );
}