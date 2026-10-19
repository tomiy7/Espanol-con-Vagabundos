
import Link from "next/link";

export const metadata = {
    title: "Kupovina uspešna",
};

export default function CheckoutSuccessPage() {
    return (
        <main className="flex min-h-[65vh] items-center justify-center bg-zinc-50 px-5 py-16">
            <section className="w-full max-w-xl rounded-3xl border border-zinc-100 bg-white p-8 text-center shadow-sm sm:p-12">
                <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-[#e8f7e9] text-3xl text-green-700">
                    ✓
                </div>

                <p className="mb-3 text-sm font-semibold uppercase tracking-widest text-zinc-500">
                    Español con Vagabundos
                </p>

                <h1 className="mb-4 text-3xl font-semibold text-zinc-900 sm:text-4xl">
                    Hvala na kupovini!
                </h1>

                <p className="mx-auto mb-8 max-w-md leading-7 text-zinc-600">
                    Plaćanje je uspešno završeno. Radujemo se što učiš španski sa nama!
                    Detalji i pristup kursu biće dostupni u tvom nalogu kada kupovina
                    bude potvrđena.
                </p>

                <div className="flex flex-col justify-center gap-3 sm:flex-row">
                    <Link
                        href="/my-account"
                        className="rounded-full bg-[#d8c6ff] px-6 py-3 font-medium text-zinc-900 hover:shadow-md"
                    >
                        Idi na moj nalog
                    </Link>

                    <Link
                        href="/courses"
                        className="rounded-full border border-zinc-200 px-6 py-3 font-medium text-zinc-700 hover:bg-zinc-50"
                    >
                        Pogledaj kurseve
                    </Link>
                </div>
            </section>
        </main>
    );
}
