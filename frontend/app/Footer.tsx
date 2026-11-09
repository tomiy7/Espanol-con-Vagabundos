
import Link from "next/link";
import type { ReactNode } from "react";
import ContactDialog from "./ContactDialog";

const FACEBOOK_URL = "https://www.facebook.com/1316021344919088";
const INSTAGRAM_URL = "https://www.instagram.com/espanolconvagabundos_/";
const YOUTUBE_URL = "https://www.youtube.com/@españolconvagabundos";

function SocialLink({
                        href,
                        label,
                        children,
                    }: {
    href: string;
    label: string;
    children: ReactNode;
}) {
    return (
        <a
            href={href}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={label}
            className="flex h-11 w-11 items-center justify-center rounded-full bg-white text-zinc-900 shadow-md transition hover:-translate-y-0.5 hover:bg-[#fff3c2] hover:shadow-lg"
        >
            {children}
        </a>
    );
}

export default function Footer() {
    return (
        <footer className="bg-[#d8c6ff] px-6 py-10 text-zinc-900">
            <div className="mx-auto grid max-w-7xl gap-10 md:grid-cols-3">

                {/* Logo, naziv i opis */}
                <div>
                    <Link
                        href="/"
                        aria-label="Español con Vagabundos - Početna"
                        className="mb-4 flex items-center gap-3"
                    >
                        <div className="flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-full bg-white shadow-md">
                            <img
                                src="/logo.jpg"
                                alt="Español con Vagabundos"
                                className="h-full w-full rounded-full object-cover"
                            />
                        </div>

                        <span className="text-lg font-bold">
              Español con Vagabundos
            </span>
                    </Link>

                    <p className="max-w-sm text-sm leading-6">
                        Učite španski jezik kroz zanimljive sadržaje, praktične
                        lekcije i iskustva koja povezuju jezik i kulturu.
                    </p>

                    {/* Društvene mreže */}
                    <div className="mt-6 flex gap-3">
                        <SocialLink href={FACEBOOK_URL} label="Facebook">
                            <svg
                                viewBox="0 0 24 24"
                                className="h-5 w-5"
                                fill="currentColor"
                                aria-hidden="true"
                            >
                                <path d="M13.5 21v-8.2h2.8l.4-3.2h-3.2V7.5c0-.9.3-1.5 1.6-1.5h1.7V3.1c-.3 0-1.3-.1-2.5-.1-2.5 0-4.2 1.5-4.2 4.3v2.3H7.3v3.2h2.8V21h3.4Z" />
                            </svg>
                        </SocialLink>

                        <SocialLink href={INSTAGRAM_URL} label="Instagram">
                            <svg
                                viewBox="0 0 24 24"
                                className="h-5 w-5"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="1.8"
                                aria-hidden="true"
                            >
                                <rect x="3" y="3" width="18" height="18" rx="5" />
                                <circle cx="12" cy="12" r="4" />
                                <circle
                                    cx="17.5"
                                    cy="6.5"
                                    r="1"
                                    fill="currentColor"
                                />
                            </svg>
                        </SocialLink>

                        <SocialLink href={YOUTUBE_URL} label="YouTube">
                            <svg
                                viewBox="0 0 24 24"
                                className="h-5 w-5"
                                fill="currentColor"
                                aria-hidden="true"
                            >
                                <path d="M23.5 6.2a3 3 0 0 0-2.1-2.1C19.5 3.6 12 3.6 12 3.6s-7.5 0-9.4.5A3 3 0 0 0 .5 6.2 31 31 0 0 0 0 12a31 31 0 0 0 .5 5.8 3 3 0 0 0 2.1 2.1c1.9.5 9.4.5 9.4.5s7.5 0 9.4-.5a3 3 0 0 0 2.1-2.1A31 31 0 0 0 24 12a31 31 0 0 0-.5-5.8ZM9.6 15.6V8.4l6.3 3.6-6.3 3.6Z" />
                            </svg>
                        </SocialLink>
                    </div>
                </div>

                {/* Informacije */}
                <div>
                    <h3 className="mb-5 text-lg font-semibold">
                        Informacije
                    </h3>

                    <ul className="space-y-3 text-sm">
                        <li>
                            <Link href="/" className="transition hover:underline">
                                Početna
                            </Link>
                        </li>

                        <li>
                            <Link
                                href="/#o-nama"
                                className="transition hover:underline"
                            >
                                O nama
                            </Link>
                        </li>

                        <li>
                            <Link
                                href="/courses"
                                className="transition hover:underline"
                            >
                                Kursevi
                            </Link>
                        </li>
                    </ul>
                </div>

                {/* Kontakt */}
                <div>
                    <h3 className="mb-5 text-lg font-semibold">
                        Kontakt
                    </h3>

                    <ContactDialog />
                </div>
            </div>

            {/* Donja linija */}
            <div className="mx-auto mt-10 max-w-7xl border-t border-zinc-900/20 pt-5 text-center text-sm">
                © {new Date().getFullYear()} Español con Vagabundos. Sva prava
                zadržana.
            </div>
        </footer>
    );
}
