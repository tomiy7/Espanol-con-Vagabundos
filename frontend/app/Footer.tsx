import Link from "next/link";
import ContactForm from "./ContactForm";

const FACEBOOK_URL = "https://www.facebook.com/1316021344919088";
const INSTAGRAM_URL = "https://www.instagram.com/espanolconvagabundos_/";

function SocialLink({
                        href,
                        label,
                        children,
                    }: {
    href: string;
    label: string;
    children: React.ReactNode;
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
        <footer className="mt-auto w-full bg-[#d8c6ff]">
            <div className="mx-auto grid max-w-6xl gap-10 px-6 py-12 md:grid-cols-[1.1fr_0.7fr_1.2fr]">

                {/* LOGO + MREZE */}
                <div>
                    <Link href="/" className="inline-flex items-center gap-3">
                        {/* stavi logo u frontend/public/logo.jpg */}
                        <img
                            src="/logo.jpg"
                            alt="Español con Vagabundos"
                            className="h-16 w-16 rounded-full object-cover shadow-md ring-2 ring-white"
                        />
                        <span className="text-lg font-bold leading-tight tracking-tight text-zinc-900">
                            Español
                            <br />
                            con Vagabundos
                        </span>
                    </Link>

                    <p className="mt-4 max-w-xs text-sm leading-6 text-zinc-700">
                        Španski jezik uz vagabundose. Učimo zajedno, od nule do
                        samopouzdanog razgovora.
                    </p>

                    <div className="mt-6 flex gap-3">
                        <SocialLink href={FACEBOOK_URL} label="Facebook">
                            <svg
                                viewBox="0 0 24 24"
                                fill="currentColor"
                                className="h-5 w-5"
                            >
                                <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z" />
                            </svg>
                        </SocialLink>

                        <SocialLink href={INSTAGRAM_URL} label="Instagram">
                            <svg
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                                className="h-5 w-5"
                            >
                                <rect x="3" y="3" width="18" height="18" rx="5" />
                                <circle cx="12" cy="12" r="4" />
                                <circle
                                    cx="17.5"
                                    cy="6.5"
                                    r="1"
                                    fill="currentColor"
                                    stroke="none"
                                />
                            </svg>
                        </SocialLink>
                    </div>
                </div>

                {/* LINKOVI */}
                <div>
                    <h3 className="mb-4 text-base font-bold text-zinc-900">
                        Navigacija
                    </h3>
                    <ul className="space-y-3 text-[15px] text-zinc-800">
                        <li>
                            <Link href="/courses" className="hover:underline">
                                Kursevi
                            </Link>
                        </li>
                        <li>
                            <Link href="/login" className="hover:underline">
                                Prijava
                            </Link>
                        </li>
                        <li>
                            <Link href="/register" className="hover:underline">
                                Registracija
                            </Link>
                        </li>
                    </ul>
                </div>

                {/* KONTAKT */}
                <div>
                    <h3 className="mb-4 text-base font-bold text-zinc-900">
                        Pošalji nam poruku
                    </h3>
                    <ContactForm />
                </div>
            </div>

            <div className="border-t border-zinc-900/10">
                <div className="mx-auto max-w-6xl px-6 py-4 text-xs text-zinc-700">
                    © {new Date().getFullYear()} Español con Vagabundos
                </div>
            </div>
        </footer>
    );
}