"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

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
        gender: "",
    });

    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);

    function handleChange(
        e: React.ChangeEvent<
            HTMLInputElement | HTMLSelectElement
        >
    ) {
        setForm({
            ...form,
            [e.target.name]: e.target.value,
        });
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();

        setError("");
        setLoading(true);

        // Username
        if (!/^[A-Za-z0-9_]{3,50}$/.test(form.username)) {
            setError(
                "Korisničko ime mora imati između 3 i 50 karaktera i može sadržati samo slova, brojeve i _"
            );
            setLoading(false);
            return;
        }

        // Email
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
            setError("Unesite validnu email adresu");
            setLoading(false);
            return;
        }

        // Telefon
        if (!/^(06\d{8}|\+3816\d{8})$/.test(form.phone)) {
            setError(
                "Telefon mora biti u formatu 06XXXXXXXX ili +3816XXXXXXXX"
            );
            setLoading(false);
            return;
        }

        // Lozinka
        if (form.password.length < 8) {
            setError("Lozinka mora imati najmanje 8 karaktera");
            setLoading(false);
            return;
        }

        // Potvrda lozinke
        if (form.password !== form.confirmPassword) {
            setError("Lozinke se ne podudaraju");
            setLoading(false);
            return;
        }

        // Ime
        if (!form.firstName.trim()) {
            setError("Ime je obavezno");
            setLoading(false);
            return;
        }

        // Prezime
        if (!form.lastName.trim()) {
            setError("Prezime je obavezno");
            setLoading(false);
            return;
        }

        // Pol
        if (!form.gender) {
            setError("Izaberite pol");
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
                        gender: form.gender,
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

            localStorage.setItem(
                "accessToken",
                data.accessToken
            );

            localStorage.setItem(
                "refreshToken",
                data.refreshToken
            );

            window.dispatchEvent(
                new Event("auth-change")
            );

            router.push("/");
        } catch {
            setError("Greška pri povezivanju sa serverom");
        } finally {
            setLoading(false);
        }
    }

    return (
        <div className="flex min-h-full flex-1 flex-col bg-white">

            {/* HERO */}

            <section className="w-full bg-[#bdebff]">
                <div className="mx-auto w-full max-w-6xl px-6 py-14 text-center">

                    <span className="mb-4 inline-block rounded-full bg-white/70 px-4 py-2 text-sm font-medium text-zinc-700">
                        Español con Vagabundos
                    </span>

                    <h1 className="mb-3 text-4xl font-semibold text-zinc-900 md:text-5xl">
                        Pridruži nam se 👋
                    </h1>

                    <p className="mx-auto max-w-xl text-zinc-700">
                        Napravi svoj nalog i kreni na svoje
                        putovanje kroz španski jezik.
                    </p>

                </div>
            </section>


            {/* REGISTER CONTENT */}

            <main className="mx-auto flex w-full max-w-6xl flex-1 justify-center px-6 py-16">

                <div className="w-full max-w-2xl">

                    <div className="rounded-3xl border border-zinc-200 bg-white p-8 shadow-sm md:p-10">

                        <div className="mb-8">

                            <span className="mb-4 inline-block rounded-full bg-[#fff3c2] px-4 py-2 text-sm font-medium text-zinc-700">
                                Kreiraj svoj nalog
                            </span>

                            <h2 className="text-2xl font-semibold text-zinc-900">
                                Registracija
                            </h2>

                            <p className="mt-2 text-zinc-500">
                                Popuni svoje podatke i postani deo naše
                                zajednice.
                            </p>

                        </div>


                        {error && (
                            <div className="mb-6 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-600">
                                {error}
                            </div>
                        )}


                        <form
                            onSubmit={handleSubmit}
                            className="flex flex-col gap-5"
                        >

                            {/* IME I PREZIME */}

                            <div className="grid gap-5 md:grid-cols-2">

                                <div className="flex flex-col gap-2">

                                    <label
                                        htmlFor="firstName"
                                        className="text-sm font-medium text-zinc-700"
                                    >
                                        Ime{" "}
                                        <span className="text-red-500">
                                            *
                                        </span>
                                    </label>

                                    <input
                                        id="firstName"
                                        name="firstName"
                                        value={form.firstName}
                                        onChange={handleChange}
                                        required
                                        className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                    />

                                </div>


                                <div className="flex flex-col gap-2">

                                    <label
                                        htmlFor="lastName"
                                        className="text-sm font-medium text-zinc-700"
                                    >
                                        Prezime{" "}
                                        <span className="text-red-500">
                                            *
                                        </span>
                                    </label>

                                    <input
                                        id="lastName"
                                        name="lastName"
                                        value={form.lastName}
                                        onChange={handleChange}
                                        required
                                        className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                    />

                                </div>

                            </div>


                            {/* POL */}

                            <div className="flex flex-col gap-2">

                                <label
                                    htmlFor="gender"
                                    className="text-sm font-medium text-zinc-700"
                                >
                                    Pol{" "}
                                    <span className="text-red-500">
                                        *
                                    </span>
                                </label>

                                <select
                                    id="gender"
                                    name="gender"
                                    value={form.gender}
                                    onChange={handleChange}
                                    required
                                    className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                >
                                    <option value="">
                                        Izaberi pol
                                    </option>

                                    <option value="female">
                                        Žensko
                                    </option>

                                    <option value="male">
                                        Muško
                                    </option>

                                </select>

                                <p className="text-xs text-zinc-500">
                                    Na osnovu ovoga će se prikazivati
                                    Vagabunda ili Vagabundo.
                                </p>

                            </div>


                            {/* KORISNIČKO IME */}

                            <div className="flex flex-col gap-2">

                                <label
                                    htmlFor="username"
                                    className="text-sm font-medium text-zinc-700"
                                >
                                    Korisničko ime{" "}
                                    <span className="text-red-500">
                                        *
                                    </span>
                                </label>

                                <input
                                    id="username"
                                    name="username"
                                    value={form.username}
                                    onChange={handleChange}
                                    minLength={3}
                                    maxLength={50}
                                    pattern="[A-Za-z0-9_]{3,50}"
                                    required
                                    className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                />

                                <p className="text-xs text-zinc-500">
                                    Minimum 3 karaktera. Dozvoljena su
                                    slova, brojevi i _.
                                </p>

                            </div>


                            {/* EMAIL */}

                            <div className="flex flex-col gap-2">

                                <label
                                    htmlFor="email"
                                    className="text-sm font-medium text-zinc-700"
                                >
                                    Email{" "}
                                    <span className="text-red-500">
                                        *
                                    </span>
                                </label>

                                <input
                                    id="email"
                                    name="email"
                                    type="email"
                                    value={form.email}
                                    onChange={handleChange}
                                    required
                                    className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                />

                            </div>


                            {/* TELEFON */}

                            <div className="flex flex-col gap-2">

                                <label
                                    htmlFor="phone"
                                    className="text-sm font-medium text-zinc-700"
                                >
                                    Telefon{" "}
                                    <span className="text-red-500">
                                        *
                                    </span>
                                </label>

                                <input
                                    id="phone"
                                    name="phone"
                                    type="tel"
                                    value={form.phone}
                                    onChange={handleChange}
                                    required
                                    placeholder="06XXXXXXXX"
                                    className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition placeholder:text-zinc-400 focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                />

                            </div>


                            {/* LOZINKE */}

                            <div className="grid gap-5 md:grid-cols-2">

                                <div className="flex flex-col gap-2">

                                    <label
                                        htmlFor="password"
                                        className="text-sm font-medium text-zinc-700"
                                    >
                                        Lozinka{" "}
                                        <span className="text-red-500">
                                            *
                                        </span>
                                    </label>

                                    <input
                                        id="password"
                                        name="password"
                                        type="password"
                                        value={form.password}
                                        onChange={handleChange}
                                        minLength={8}
                                        required
                                        className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                    />

                                    <p className="text-xs text-zinc-500">
                                        Minimum 8 karaktera.
                                    </p>

                                </div>


                                <div className="flex flex-col gap-2">

                                    <label
                                        htmlFor="confirmPassword"
                                        className="text-sm font-medium text-zinc-700"
                                    >
                                        Potvrdi lozinku{" "}
                                        <span className="text-red-500">
                                            *
                                        </span>
                                    </label>

                                    <input
                                        id="confirmPassword"
                                        name="confirmPassword"
                                        type="password"
                                        value={form.confirmPassword}
                                        onChange={handleChange}
                                        minLength={8}
                                        required
                                        className="rounded-2xl border border-zinc-200 bg-zinc-50 px-4 py-3 text-zinc-900 outline-none transition focus:border-[#d8c6ff] focus:bg-white focus:ring-4 focus:ring-[#d8c6ff]/30"
                                    />

                                </div>

                            </div>


                            {/* SUBMIT */}

                            <button
                                type="submit"
                                disabled={loading}
                                className="mt-3 rounded-full bg-[#d8c6ff] px-6 py-4 font-medium text-zinc-900 transition hover:scale-[1.01] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-50"
                            >
                                {loading
                                    ? "Registrujem..."
                                    : "Registruj se →"}
                            </button>


                            {/* LOGIN */}

                            <div className="mt-2 text-center text-sm text-zinc-600">

                                Već imaš nalog?{" "}

                                <Link
                                    href="/login"
                                    className="font-semibold text-zinc-900 underline decoration-[#d8c6ff] decoration-2 underline-offset-4 transition hover:text-zinc-600"
                                >
                                    Prijavi se
                                </Link>

                            </div>

                        </form>

                    </div>

                </div>

            </main>

        </div>
    );
}