"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useMemo, useState } from "react";

type Course = {
    id: string;
    name: string;
    price: string | number;
    thumbnail?: string | null;
    is_published: boolean;
};

type EnrollmentType = "individual" | "pair" | "group";

const enrollmentOptions: {
    id: EnrollmentType;
    title: string;
    subtitle: string;
    description: string;
    icon: string;
}[] = [
    {
        id: "individual",
        title: "Individualno",
        subtitle: "Časovi jedan na jedan",
        description: "Učiš svojim tempom, uz profesora i termine prilagođene individualnoj nastavi.",
        icon: "✦",
    },
    {
        id: "pair",
        title: "U paru",
        subtitle: "Uči sa prijateljem",
        description: "Prijavi se zajedno sa osobom sa kojom želiš da pohađaš kurs.",
        icon: "♧",
    },
    {
        id: "group",
        title: "Grupno",
        subtitle: "Uči sa drugim polaznicima",
        description: "Izaberi grupu koja ti odgovara ili iskaži interesovanje za formiranje nove grupe.",
        icon: "◎",
    },
];

function EnrollmentContent() {
    const searchParams = useSearchParams();
    const requestedCourseId = searchParams.get("courseId");

    const [courses, setCourses] = useState<Course[]>([]);
    const [coursesLoading, setCoursesLoading] = useState(true);
    const [coursesError, setCoursesError] = useState("");
    const [selectedCourseId, setSelectedCourseId] = useState(requestedCourseId ?? "");
    const [selectedType, setSelectedType] = useState<EnrollmentType | "">("");
    const [partnerEmail, setPartnerEmail] = useState("");
    const [notice, setNotice] = useState("");

    useEffect(() => {
        let active = true;

        async function loadCourses() {
            try {
                const directusUrl = process.env.NEXT_PUBLIC_DIRECTUS_URL;
                if (!directusUrl) {
                    throw new Error("Directus adresa nije podešena.");
                }

                const response = await fetch(
                    `${directusUrl}/items/courses?filter[is_published][_eq]=true&fields=id,name,price,thumbnail,is_published&sort=name`,
                    { cache: "no-store" }
                );

                if (!response.ok) {
                    throw new Error("Kursevi trenutno nisu dostupni.");
                }

                const result = await response.json();
                if (active) {
                    const publishedCourses: Course[] = Array.isArray(result.data) ? result.data : [];
                    setCourses(publishedCourses);
                    setSelectedCourseId((current) => {
                        if (current && publishedCourses.some((course) => course.id === current)) return current;
                        return publishedCourses[0]?.id ?? "";
                    });
                }
            } catch (error) {
                if (active) {
                    setCoursesError(error instanceof Error ? error.message : "Došlo je do greške pri učitavanju kurseva.");
                }
            } finally {
                if (active) setCoursesLoading(false);
            }
        }

        loadCourses();
        return () => { active = false; };
    }, []);

    const selectedCourse = useMemo(
        () => courses.find((course) => course.id === selectedCourseId),
        [courses, selectedCourseId]
    );

    function handleContinue() {
        setNotice("Izbor je zabeležen u ovoj sesiji. Slanje prijave biće omogućeno kada povežemo Groups servis.");
    }

    return (
        <main className="min-h-[75vh] bg-[#faf8ff] px-4 py-10 sm:px-6 sm:py-16">
            <div className="mx-auto max-w-5xl">
                <Link href="/courses" className="mb-7 inline-flex items-center gap-2 text-sm font-medium text-zinc-500 transition hover:text-zinc-900">
                    <span aria-hidden="true">←</span> Svi kursevi
                </Link>

                <div className="mb-10 max-w-2xl">
                    <p className="mb-3 text-sm font-semibold uppercase tracking-[0.18em] text-purple-700">Español con Vagabundos</p>
                    <h1 className="text-3xl font-bold tracking-tight text-zinc-900 sm:text-4xl">Prijava na kurs</h1>
                    <p className="mt-4 text-base leading-7 text-zinc-600">
                        Izaberi kurs i način na koji želiš da učiš španski. Svaki način pohađanja ima svoje korake.
                    </p>
                </div>

                <section className="rounded-3xl border border-purple-100 bg-white p-5 shadow-sm sm:p-8">
                    <div className="mb-7 flex items-start gap-4">
                        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[#eee5ff] font-semibold text-purple-800">1</span>
                        <div>
                            <h2 className="text-xl font-semibold text-zinc-900">Izaberi kurs</h2>
                            <p className="mt-1 text-sm text-zinc-500">Prikazani su objavljeni kursevi iz kataloga.</p>
                        </div>
                    </div>

                    {coursesLoading ? (
                        <div className="rounded-2xl bg-zinc-50 p-5 text-sm text-zinc-500">Učitavanje kurseva…</div>
                    ) : coursesError ? (
                        <div className="rounded-2xl border border-red-200 bg-red-50 p-5 text-sm text-red-700">{coursesError}</div>
                    ) : courses.length === 0 ? (
                        <div className="rounded-2xl border border-dashed border-zinc-300 bg-zinc-50 p-6 text-sm text-zinc-600">
                            Trenutno nema objavljenih kurseva.
                        </div>
                    ) : (
                        <div className="grid gap-3 sm:grid-cols-2">
                            {courses.map((course) => {
                                const selected = selectedCourseId === course.id;
                                return (
                                    <button
                                        key={course.id}
                                        type="button"
                                        onClick={() => { setSelectedCourseId(course.id); setNotice(""); }}
                                        aria-pressed={selected}
                                        className={`flex items-center justify-between gap-4 rounded-2xl border p-4 text-left transition ${selected ? "border-purple-400 bg-purple-50 ring-2 ring-purple-100" : "border-zinc-200 bg-white hover:border-purple-200 hover:bg-purple-50/40"}`}
                                    >
                                        <span className="min-w-0">
                                            <span className="block font-semibold text-zinc-900">{course.name}</span>
                                            <span className="mt-1 block text-sm text-zinc-500">{Number(course.price).toLocaleString("sr-RS")} €</span>
                                        </span>
                                        <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full border text-xs ${selected ? "border-purple-600 bg-purple-600 text-white" : "border-zinc-300 text-transparent"}`}>✓</span>
                                    </button>
                                );
                            })}
                        </div>
                    )}
                </section>

                <section className="mt-6 rounded-3xl border border-purple-100 bg-white p-5 shadow-sm sm:p-8">
                    <div className="mb-7 flex items-start gap-4">
                        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[#eee5ff] font-semibold text-purple-800">2</span>
                        <div>
                            <h2 className="text-xl font-semibold text-zinc-900">Izaberi način pohađanja</h2>
                            <p className="mt-1 text-sm text-zinc-500">Možeš da promeniš izbor pre potvrde prijave.</p>
                        </div>
                    </div>

                    <div className="grid gap-4 md:grid-cols-3">
                        {enrollmentOptions.map((option) => {
                            const selected = selectedType === option.id;
                            return (
                                <button
                                    key={option.id}
                                    type="button"
                                    onClick={() => { setSelectedType(option.id); setNotice(""); }}
                                    aria-pressed={selected}
                                    className={`rounded-2xl border p-5 text-left transition ${selected ? "border-purple-400 bg-purple-50 ring-2 ring-purple-100" : "border-zinc-200 bg-white hover:-translate-y-0.5 hover:border-purple-200 hover:shadow-sm"}`}
                                >
                                    <span className="mb-4 flex h-11 w-11 items-center justify-center rounded-xl bg-[#eee5ff] text-xl text-purple-800">{option.icon}</span>
                                    <span className="block text-lg font-semibold text-zinc-900">{option.title}</span>
                                    <span className="mt-1 block text-sm font-medium text-purple-700">{option.subtitle}</span>
                                    <span className="mt-3 block text-sm leading-6 text-zinc-500">{option.description}</span>
                                </button>
                            );
                        })}
                    </div>

                    {selectedType && (
                        <div className="mt-7 rounded-2xl bg-[#faf8ff] p-5 sm:p-6">
                            {selectedType === "individual" && (
                                <>
                                    <h3 className="text-lg font-semibold text-zinc-900">Individualna nastava</h3>
                                    <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-600">
                                        U sledećem koraku biće prikazani profesori i njihovi slobodni termini. Trenutno ne prikazujemo izbor dok ti podaci ne budu dostupni iz sistema.
                                    </p>
                                    <p className="mt-4 text-xs font-medium text-zinc-500">Izbor profesora i termina biće omogućen nakon povezivanja Groups servisa.</p>
                                </>
                            )}

                            {selectedType === "pair" && (
                                <>
                                    <h3 className="text-lg font-semibold text-zinc-900">Prijava u paru</h3>
                                    <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-600">
                                        Unesi e-mail adresu osobe koju želiš da pozoveš. Pozivnica će biti poslata kada povezivanje sa servisom za prijave bude završeno.
                                    </p>
                                    <label htmlFor="partner-email" className="mt-5 block text-sm font-medium text-zinc-700">E-mail prijatelja</label>
                                    <input
                                        id="partner-email"
                                        type="email"
                                        autoComplete="email"
                                        value={partnerEmail}
                                        onChange={(event) => { setPartnerEmail(event.target.value); setNotice(""); }}
                                        placeholder="ime@email.com"
                                        className="mt-2 w-full max-w-md rounded-xl border border-zinc-300 bg-white px-4 py-3 text-sm text-zinc-900 outline-none transition placeholder:text-zinc-400 focus:border-purple-400 focus:ring-2 focus:ring-purple-100"
                                    />
                                </>
                            )}

                            {selectedType === "group" && (
                                <>
                                    <h3 className="text-lg font-semibold text-zinc-900">Grupna nastava</h3>
                                    <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-600">
                                        Ovde će biti prikazane grupe za izabrani kurs, sa datumom početka i raspoloživim mestima. Pošto podaci o grupama još nisu povezani, ne prikazujemo izmišljene termine ili grupe.
                                    </p>
                                    <div className="mt-4 rounded-xl border border-dashed border-purple-200 bg-white px-4 py-4 text-sm text-zinc-500">
                                        Grupe i lista čekanja biće dostupne nakon povezivanja Groups servisa.
                                    </div>
                                </>
                            )}
                        </div>
                    )}

                    <div className="mt-8 flex flex-col gap-4 border-t border-zinc-100 pt-6 sm:flex-row sm:items-center sm:justify-between">
                        <p className="text-sm text-zinc-500">{selectedCourse ? `Izabrani kurs: ${selectedCourse.name}` : "Prvo izaberi kurs."}</p>
                        <button
                            type="button"
                            onClick={handleContinue}
                            disabled={!selectedCourse || !selectedType || coursesLoading || Boolean(coursesError)}
                            className="rounded-full bg-[#d8c6ff] px-7 py-3.5 text-sm font-semibold text-zinc-900 transition hover:bg-[#c9b0ff] disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            Nastavi
                        </button>
                    </div>
                    {notice && <p role="status" className="mt-4 rounded-xl border border-purple-100 bg-purple-50 px-4 py-3 text-sm leading-6 text-purple-900">{notice}</p>}
                </section>
            </div>
        </main>
    );
}

export default function EnrollmentPage() {
    return (
        <Suspense fallback={<main className="min-h-[70vh] bg-[#faf8ff] px-6 py-16 text-center text-zinc-500">Učitavanje…</main>}>
            <EnrollmentContent />
        </Suspense>
    );
}
