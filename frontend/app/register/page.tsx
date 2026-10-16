"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

type FieldName =
    | "username"
    | "email"
    | "password"
    | "confirmPassword"
    | "phone"
    | "firstName"
    | "lastName"
    | "gender";

async function isAvailable(
    kind: "username" | "email",
    value: string
): Promise<boolean> {
    const res = await fetch(
        `${process.env.NEXT_PUBLIC_AUTH_URL}/auth/check-${kind}?${kind}=${encodeURIComponent(value)}`
    );

    if (!res.ok) {
        throw new Error("Availability check failed");
    }

    const data = await res.json();

    return data.available === true;
}

function getPasswordProblems(password: string): string[] {
    const problems: string[] = [];

    if (password.length < 8) {
        problems.push("najmanje 8 karaktera");
    }

    if (!/[a-z]/.test(password)) {
        problems.push("malo slovo");
    }

    if (!/[A-Z]/.test(password)) {
        problems.push("veliko slovo");
    }

    if (!/\d/.test(password)) {
        problems.push("broj");
    }

    if (!/[^A-Za-z0-9\s]/.test(password)) {
        problems.push("specijalni karakter (npr. ! ? # $ %)");
    }

    return problems;
}

function FieldError({
                        id,
                        message,
                    }: {
    id: string;
    message?: string;
}) {
    if (!message) {
        return null;
    }

    return (
        <p
            id={id}
            role="alert"
            className="flex items-start gap-1.5 text-sm text-red-600"
        >
            <svg
                aria-hidden="true"
                viewBox="0 0 20 20"
                fill="currentColor"
                className="mt-0.5 h-4 w-4 shrink-0"
            >
                <path
                    fillRule="evenodd"
                    d="M10 18a8 8 0 100-16 8 8 0 000 16zM9 6a1 1 0 112 0v4a1 1 0 11-2 0V6zm1 8a1 1 0 100-2 1 1 0 000 2z"
                    clipRule="evenodd"
                />
            </svg>

            <span>{message}</span>
        </p>
    );
}

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

    // Greška vezana za jedno polje (prikazuje se ispod tog polja)
    const [fieldError, setFieldError] = useState<{
        field: FieldName;
        message: string;
    } | null>(null);

    // Opšta greška (server ne radi, nepoznata greška...)
    const [formError, setFormError] = useState("");

    const [loading, setLoading] = useState(false);

    const baseInput =
        "rounded-2xl border px-4 py-3 text-zinc-900 outline-none transition focus:bg-white focus:ring-4";

    const okInput =
        "border-zinc-200 bg-zinc-50 focus:border-[#d8c6ff] focus:ring-[#d8c6ff]/30";

    const badInput =
        "border-red-400 bg-red-50 focus:border-red-500 focus:ring-red-200";

    function inputClass(field: FieldName) {
        return `${baseInput} ${
            fieldError?.field === field ? badInput : okInput
        }`;
    }

    function errorFor(field: FieldName) {
        return fieldError?.field === field
            ? fieldError.message
            : undefined;
    }

    function showFieldError(field: FieldName, message: string) {
        setFieldError({ field, message });

        document.getElementById(field)?.focus();
    }

    function handleChange(
        e: React.ChangeEvent<
            HTMLInputElement | HTMLSelectElement
        >
    ) {
        setForm({
            ...form,
            [e.target.name]: e.target.value,
        });

        if (fieldError?.field === e.target.name) {
            setFieldError(null);
        }

        setFormError("");
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();

        setFieldError(null);
        setFormError("");
        setLoading(true);

        const username = form.username.trim();
        const email = form.email.trim();

        try {
            // 1. KORISNIČKO IME (format, pa zauzetost)

            if (!/^[A-Za-z0-9_]{3,30}$/.test(username)) {
                showFieldError(
                    "username",
                    "Korisničko ime mora imati između 3 i 30 karaktera i može sadržati samo slova, brojeve i _"
                );
                return;
            }

            if (!(await isAvailable("username", username))) {
                showFieldError(
                    "username",
                    "Korisničko ime je već zauzeto. Izaberi drugo."
                );
                return;
            }

            // 2. EMAIL (dužina, format, pa zauzetost)

            if (email.length > 254) {
                showFieldError(
                    "email",
                    "Email može imati najviše 254 karaktera."
                );
                return;
            }

            if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
                showFieldError(
                    "email",
                    "Unesi validnu email adresu."
                );
                return;
            }

            if (!(await isAvailable("email", email))) {
                showFieldError(
                    "email",
                    "Email je već zauzet. Izaberi drugi ili se prijavi."
                );
                return;
            }

            // 3. JAČINA LOZINKE

            const passwordProblems = getPasswordProblems(
                form.password
            );

            if (passwordProblems.length > 0) {
                showFieldError(
                    "password",
                    `Lozinka mora imati: ${passwordProblems.join(", ")}.`
                );
                return;
            }

            // 4. POTVRDA LOZINKE

            if (form.password !== form.confirmPassword) {
                showFieldError(
                    "confirmPassword",
                    "Lozinke se ne podudaraju."
                );
                return;
            }

            // 5. OSTALA POLJA

            if (!/^(06\d{8}|\+3816\d{8})$/.test(form.phone)) {
                showFieldError(
                    "phone",
                    "Telefon mora biti u formatu 06XXXXXXXX ili +3816XXXXXXXX."
                );
                return;
            }

            if (!form.firstName.trim()) {
                showFieldError("firstName", "Ime je obavezno.");
                return;
            }

            if (!form.lastName.trim()) {
                showFieldError("lastName", "Prezime je obavezno.");
                return;
            }

            if (!form.gender) {
                showFieldError("gender", "Izaberi pol.");
                return;
            }

            // SLANJE

            const res = await fetch(
                `${process.env.NEXT_PUBLIC_AUTH_URL}/auth/register`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                        username,
                        email,
                        phone: form.phone,
                        password: form.password,
                        confirmPassword: form.confirmPassword,
                        firstName: form.firstName,
                        lastName: form.lastName,
                        gender: form.gender,
                    }),
                }
            );

            const data = await res.json().catch(() => null);

            if (!res.ok) {
                const text = String(
                    data?.message || ""
                ).toLowerCase();

                // Neko je u međuvremenu zauzeo korisničko ime / email
                if (res.status === 409 && text.includes("username")) {
                    showFieldError(
                        "username",
                        "Korisničko ime je već zauzeto. Izaberi drugo."
                    );
                    return;
                }

                if (res.status === 409 && text.includes("email")) {
                    showFieldError(
                        "email",
                        "Email je već zauzet. Izaberi drugi ili se prijavi."
                    );
                    return;
                }

                let message = data?.message;

                if (!message && data?.errors) {
                    message = Object.values(data.errors)
                        .flat()
                        .join(" ");
                }

                setFormError(
                    message ||
                    data?.title ||
                    "Greška prilikom registracije"
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
            setFormError("Greška pri povezivanju sa serverom. Pokušaj ponovo.");
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


                        {formError && (
                            <div
                                role="alert"
                                className="mb-6 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-600"
                            >
                                {formError}
                            </div>
                        )}


                        <form
                            onSubmit={handleSubmit}
                            noValidate
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
                                        aria-invalid={
                                            fieldError?.field === "firstName"
                                        }
                                        aria-describedby="firstName-error"
                                        className={inputClass("firstName")}
                                    />

                                    <FieldError
                                        id="firstName-error"
                                        message={errorFor("firstName")}
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
                                        aria-invalid={
                                            fieldError?.field === "lastName"
                                        }
                                        aria-describedby="lastName-error"
                                        className={inputClass("lastName")}
                                    />

                                    <FieldError
                                        id="lastName-error"
                                        message={errorFor("lastName")}
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
                                    aria-invalid={
                                        fieldError?.field === "gender"
                                    }
                                    aria-describedby="gender-error"
                                    className={inputClass("gender")}
                                >
                                    <option value="">
                                        Izaberi pol
                                    </option>

                                    <option value="Female">
                                        Žensko
                                    </option>

                                    <option value="Male">
                                        Muško
                                    </option>

                                </select>

                                <FieldError
                                    id="gender-error"
                                    message={errorFor("gender")}
                                />

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
                                    maxLength={30}
                                    required
                                    aria-invalid={
                                        fieldError?.field === "username"
                                    }
                                    aria-describedby="username-error"
                                    className={inputClass("username")}
                                />

                                <FieldError
                                    id="username-error"
                                    message={errorFor("username")}
                                />

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
                                    maxLength={254}
                                    required
                                    aria-invalid={
                                        fieldError?.field === "email"
                                    }
                                    aria-describedby="email-error"
                                    className={inputClass("email")}
                                />

                                <FieldError
                                    id="email-error"
                                    message={errorFor("email")}
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
                                    aria-invalid={
                                        fieldError?.field === "phone"
                                    }
                                    aria-describedby="phone-error"
                                    className={inputClass("phone")}
                                />

                                <FieldError
                                    id="phone-error"
                                    message={errorFor("phone")}
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
                                        required
                                        aria-invalid={
                                            fieldError?.field === "password"
                                        }
                                        aria-describedby="password-error"
                                        className={inputClass("password")}
                                    />

                                    <FieldError
                                        id="password-error"
                                        message={errorFor("password")}
                                    />

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
                                        required
                                        aria-invalid={
                                            fieldError?.field ===
                                            "confirmPassword"
                                        }
                                        aria-describedby="confirmPassword-error"
                                        className={inputClass("confirmPassword")}
                                    />

                                    <FieldError
                                        id="confirmPassword-error"
                                        message={errorFor("confirmPassword")}
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