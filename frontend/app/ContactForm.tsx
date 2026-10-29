"use client";

import { useState } from "react";

type Status = "idle" | "sending" | "sent" | "error";

export default function ContactForm() {
    const [status, setStatus] = useState<Status>("idle");
    const [message, setMessage] = useState("");

    async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
        e.preventDefault();

        const form = e.currentTarget;
        const data = new FormData(form);

        setStatus("sending");
        setMessage("");

        try {
            const res = await fetch("/api/contact", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    name: data.get("name"),
                    email: data.get("email"),
                    message: data.get("message"),
                    website: data.get("website"), // honeypot
                }),
            });

            const json = await res.json().catch(() => ({}));

            if (!res.ok) {
                setStatus("error");
                setMessage(json.message || "Slanje nije uspelo. Pokušaj ponovo.");
                return;
            }

            form.reset();
            setStatus("sent");
            setMessage("Hvala! Poruka je poslata, javićemo ti se uskoro.");
        } catch {
            setStatus("error");
            setMessage("Slanje nije uspelo. Proveri vezu i pokušaj ponovo.");
        }
    }

    const inputClass =
        "w-full rounded-xl border border-white/0 bg-white px-4 py-3 text-sm text-zinc-900 placeholder:text-zinc-400 shadow-sm outline-none transition focus:border-zinc-900/30 focus:ring-2 focus:ring-white";

    return (
        <form onSubmit={handleSubmit} className="space-y-3">
            <input
                name="name"
                type="text"
                required
                minLength={2}
                maxLength={80}
                placeholder="Tvoje ime"
                className={inputClass}
            />

            <input
                name="email"
                type="email"
                required
                maxLength={254}
                placeholder="Tvoj email"
                className={inputClass}
            />

            <textarea
                name="message"
                required
                minLength={10}
                maxLength={2000}
                rows={4}
                placeholder="Kako možemo da ti pomognemo?"
                className={`${inputClass} resize-none`}
            />

            {/* honeypot: ljudi ga ne vide, botovi popune */}
            <input
                name="website"
                type="text"
                tabIndex={-1}
                autoComplete="off"
                aria-hidden="true"
                className="hidden"
            />

            <button
                type="submit"
                disabled={status === "sending"}
                className="inline-flex w-full items-center justify-center gap-2 rounded-full bg-zinc-900 px-6 py-3 text-sm font-semibold text-white transition hover:bg-zinc-700 disabled:opacity-60"
            >
                {status === "sending" ? "Šaljem..." : "Pošalji poruku"}
                <span aria-hidden="true">→</span>
            </button>

            {message && (
                <p
                    role="status"
                    className={`text-sm ${
                        status === "sent" ? "text-green-800" : "text-red-700"
                    }`}
                >
                    {message}
                </p>
            )}
        </form>
    );
}