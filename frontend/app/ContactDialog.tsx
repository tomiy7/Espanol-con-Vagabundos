
"use client";

import { useEffect, useState } from "react";
import ContactForm from "./ContactForm";

export default function ContactDialog() {
    const [isOpen, setIsOpen] = useState(false);

    useEffect(() => {
        if (!isOpen) return;

        function handleEscape(event: KeyboardEvent) {
            if (event.key === "Escape") {
                setIsOpen(false);
            }
        }

        document.addEventListener("keydown", handleEscape);
        document.body.style.overflow = "hidden";

        return () => {
            document.removeEventListener("keydown", handleEscape);
            document.body.style.overflow = "";
        };
    }, [isOpen]);

    return (
        <>
            <span className="text-sm text-zinc-800">
                Imaš pitanje za nas?{" "}
                <button
                    type="button"
                    onClick={() => setIsOpen(true)}
                    className="font-semibold underline underline-offset-4 transition hover:text-zinc-950"
                >
                    Kontaktiraj nas.
                </button>
            </span>

            {isOpen && (
                <div
                    className="fixed inset-0 z-[100] flex items-center justify-center bg-zinc-950/60 px-4 py-6 backdrop-blur-sm"
                    onMouseDown={(event) => {
                        if (event.target === event.currentTarget) {
                            setIsOpen(false);
                        }
                    }}
                >
                    <div
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="contact-title"
                        className="relative max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-3xl bg-[#d8c6ff] p-7 shadow-2xl sm:p-9"
                    >
                        <button
                            type="button"
                            onClick={() => setIsOpen(false)}
                            aria-label="Zatvori"
                            className="absolute right-5 top-5 flex h-9 w-9 items-center justify-center rounded-full bg-white text-xl text-zinc-700 transition hover:bg-[#fff3c2]"
                        >
                            ×
                        </button>

                        <div className="mb-6 pr-8">
                            <h2
                                id="contact-title"
                                className="text-2xl font-bold text-zinc-900"
                            >
                                Imaš pitanje za nas?
                            </h2>

                            <p className="mt-2 text-sm leading-6 text-zinc-700">
                                Popuni formu i javićemo ti se u najkraćem roku.
                            </p>
                        </div>

                        <ContactForm />
                    </div>
                </div>
            )}
        </>
    );
}
