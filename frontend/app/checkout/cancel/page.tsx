
import Link from "next/link";

export const metadata = {
    title: "Kupovina nije završena",
};

export default function CheckoutCancelPage() {
    return (
        <main className="flex min-h-[65vh] items-center justify-center bg-zinc-50 px-5 py-16">
            <section className="w-full max-w-xl rounded-3xl border border-zinc-100 bg-white p-8 text-center shadow-sm sm:p-12">
                <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-[#fff3c2] text-3xl">
                    ↩
                </div>

                <p className="mb-3 text-sm font-semibold uppercase tracking-widest text-zinc-500">
                    Español con Vagabundos
                </p>

                <h1 className="mb-4 text-3xl font-semibold text-zinc-900 sm:text-4xl">
                    Kupovina nije završena
                </h1>

                <p className="mx-auto mb-8 max-w-md leading-7 text-zinc-600">
                    Plaćanje je otkazano. Možeš da se vratiš na kurs i pokušaš ponovo
                    kada budeš spremna.
                </p>

                <div className="flex flex-col justify-center gap-3 sm:flex-row">
                    <Link
                        href="/courses"
                        className="rounded-full bg-[#d8c6ff] px-6 py-3 font-medium text-zinc-900 hover:shadow-md"
                    >
                        Vrati se na kurseve
                    </Link>

                    <Link
                        href="/my-account"
                        className="rounded-full border border-zinc-200 px-6 py-3 font-medium text-zinc-700 hover:bg-zinc-50"
                    >
                        Idi na moj nalog
                    </Link>
                </div>
            </section>
        </main>
    );
}
