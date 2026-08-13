export default function Footer() {
    return (
        <footer className="w-full border-t border-zinc-200 dark:border-zinc-800 mt-auto">
            <div className="max-w-3xl mx-auto px-6 py-6 text-zinc-500 dark:text-zinc-500 text-sm">
                © {new Date().getFullYear()} Español con Vagabundos
            </div>
        </footer>
    );
}