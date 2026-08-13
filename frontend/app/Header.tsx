import Link from "next/link";

export default function Header() {
    return (
        <header className="w-full border-b border-zinc-200 dark:border-zinc-800">
            <div className="max-w-3xl mx-auto px-6 py-4 flex items-center justify-between">
                <Link href="/" className="font-semibold text-lg text-black dark:text-zinc-50">
                    Español con Vagabundos
                </Link>
                <nav className="flex gap-6">
                    <Link href="/courses" className="text-zinc-600 dark:text-zinc-400 hover:text-black dark:hover:text-zinc-50">
                        Kursevi
                    </Link>
                </nav>
            </div>
        </header>
    );
}