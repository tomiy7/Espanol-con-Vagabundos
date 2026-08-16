"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";

export default function RegisterPage() {
    const router = useRouter();

    const [form, setForm] = useState({
        username: "",
        email: "",
        phone: "",
        password: "",
        confirmPassword: "",
        firstName: "",
        lastName: "",
    });

    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);

    function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
        setForm({
            ...form,
            [e.target.name]: e.target.value,
        });
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        setError("");
        setLoading(true);

        // Username: minimum 3 karaktera, samo slova, brojevi i _
        if (!/^[A-Za-z0-9_]{3,50}$/.test(form.username)) {
            setError(
                "Korisničko ime mora imati između 3 i 50 karaktera i može sadržati samo slova, brojeve i _"
            );
            setLoading(false);
            return;
        }

        // Email: validan format
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
            setError("Unesite validnu email adresu");
            setLoading(false);
            return;
        }

        // Phone: 06XXXXXXXX ili +3816XXXXXXXX
        if (!/^(06\d{8}|\+3816\d{8})$/.test(form.phone)) {
            setError(
                "Telefon mora biti u formatu 06XXXXXXXX ili +3816XXXXXXXX"
            );
            setLoading(false);
            return;
        }

        // Password: minimum 8 karaktera
        if (form.password.length < 8) {
            setError("Lozinka mora imati najmanje 8 karaktera");
            setLoading(false);
            return;
        }

        // Confirm password
        if (form.password !== form.confirmPassword) {
            setError("Lozinke se ne podudaraju");
            setLoading(false);
            return;
        }

        // First name
        if (!form.firstName.trim()) {
            setError("Ime je obavezno");
            setLoading(false);
            return;
        }

        // Last name
        if (!form.lastName.trim()) {
            setError("Prezime je obavezno");
            setLoading(false);
            return;
        }

        try {
            const res = await fetch(
                `${process.env.NEXT_PUBLIC_AUTH_URL}/auth/register`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                        username: form.username,
                        email: form.email,
                        phone: form.phone,
                        password: form.password,
                        confirmPassword: form.confirmPassword,
                        firstName: form.firstName,
                        lastName: form.lastName,
                    }),
                }
            );

            const data = await res.json();

            if (!res.ok) {
                setError(
                    data.message || "Greška prilikom registracije"
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
                    Registracija
                </h1>

                {error && (
                    <p className="text-red-600 dark:text-red-400 text-sm">
                        {error}
                    </p>
                )}

                {/* Ime */}
                <div className="flex flex-col gap-1">
                    <label
                        htmlFor="firstName"
                        className="text-sm font-medium text-black dark:text-zinc-50"
                    >
                        Ime <span className="text-red-600">*</span>
                    </label>

                    <input
                        id="firstName"
                        name="firstName"
                        value={form.firstName}
                        onChange={handleChange}
                        className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                        required
                    />
                </div>

                {/* Prezime */}
                <div className="flex flex-col gap-1">
                    <label
                        htmlFor="lastName"
                        className="text-sm font-medium text-black dark:text-zinc-50"
                    >
                        Prezime <span className="text-red-600">*</span>
                    </label>

                    <input
                        id="lastName"
                        name="lastName"
                        value={form.lastName}
                        onChange={handleChange}
                        className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                        required
                    />
                </div>

                {/* Username */}
                <div className="flex flex-col gap-1">
                    <label
                        htmlFor="username"
                        className="text-sm font-medium text-black dark:text-zinc-50"
                    >
                        Korisničko ime{" "}
                        <span className="text-red-600">*</span>
                    </label>

                    <input
                        id="username"
                        name="username"
                        value={form.username}
                        onChange={handleChange}
                        minLength={3}
                        maxLength={50}
                        pattern="[A-Za-z0-9_]{3,50}"
                        className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                        required
                    />
                </div>

                {/* Email */}
                <div className="flex flex-col gap-1">
                    <label
                        htmlFor="email"
                        className="text-sm font-medium text-black dark:text-zinc-50"
                    >
                        Email <span className="text-red-600">*</span>
                    </label>

                    <input
                        id="email"
                        name="email"
                        type="email"
                        value={form.email}
                        onChange={handleChange}
                        pattern="^[^\s@]+@[^\s@]+\.[^\s@]+$"
                        className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                        required
                    />
                </div>

                {/* Telefon */}
                <div className="flex flex-col gap-1">
                    <label
                        htmlFor="phone"
                        className="text-sm font-medium text-black dark:text-zinc-50"
                    >
                        Telefon <span className="text-red-600">*</span>
                    </label>

                    <input
                        id="phone"
                        name="phone"
                        type="tel"
                        value={form.phone}
                        onChange={handleChange}
                        pattern="(06[0-9]{8}|\+3816[0-9]{8})"
                        className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                        required
                    />
                </div>

                {/* Password */}
                <div className="flex flex-col gap-1">
                    <label
                        htmlFor="password"
                        className="text-sm font-medium text-black dark:text-zinc-50"
                    >
                        Lozinka <span className="text-red-600">*</span>
                    </label>

                    <input
                        id="password"
                        name="password"
                        type="password"
                        value={form.password}
                        onChange={handleChange}
                        minLength={8}
                        className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                        required
                    />

                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                        Minimum 8 karaktera.
                    </p>
                </div>

                {/* Confirm Password */}
                <div className="flex flex-col gap-1">
                    <label
                        htmlFor="confirmPassword"
                        className="text-sm font-medium text-black dark:text-zinc-50"
                    >
                        Potvrdi lozinku{" "}
                        <span className="text-red-600">*</span>
                    </label>

                    <input
                        id="confirmPassword"
                        name="confirmPassword"
                        type="password"
                        value={form.confirmPassword}
                        onChange={handleChange}
                        minLength={8}
                        className="border border-zinc-300 dark:border-zinc-700 rounded-md p-2 bg-white dark:bg-zinc-900"
                        required
                    />
                </div>

                <button
                    type="submit"
                    disabled={loading}
                    className="rounded-full bg-black text-white dark:bg-white dark:text-black py-2 font-medium mt-2 disabled:opacity-50"
                >
                    {loading ? "Registrujem..." : "Registruj se"}
                </button>

                <p className="text-sm text-zinc-600 dark:text-zinc-400 text-center">
                    Već imaš nalog?{" "}
                    <Link
                        href="/login"
                        className="underline font-medium text-black dark:text-white"
                    >
                        Prijavi se
                    </Link>
                </p>
            </form>
        </div>
    );
}