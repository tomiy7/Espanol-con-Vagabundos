export default function Footer() {
    return (
        <footer className="mt-auto w-full bg-[#d8c6ff]">
            <div className="mx-auto max-w-6xl px-6 py-6 text-sm text-zinc-700">
                © {new Date().getFullYear()} Español con Vagabundos
            </div>
        </footer>
    );
}