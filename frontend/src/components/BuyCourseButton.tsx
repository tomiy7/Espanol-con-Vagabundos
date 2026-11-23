"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../hooks/useAuth";
import { ApiError } from "../lib/api/http";
import { createPayment, type CreatePaymentResponse } from "../lib/api/payments";

type BuyCourseButtonProps = {
    courseId: string;
    price: string;
};

// Backend za sada vraca ovu poruku (na engleskom) kad vec postoji uplatnica na cekanju.
function getPendingReference(error: ApiError): string | null {
    const match = /already in pending.*reference:\s*([^)]+)\)/i.exec(error.message);
    return match ? match[1].trim() : null;
}

export default function BuyCourseButton({ courseId, price }: BuyCourseButtonProps) {
    const router = useRouter();
    const { user, isAuthenticated } = useAuth();

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");
    const [payment, setPayment] = useState<CreatePaymentResponse | null>(null);

    async function handleBuy() {
        if (!isAuthenticated) {
            router.push("/login");
            return;
        }

        setLoading(true);
        setError("");

        try {
            const payerName =
                [user?.firstName, user?.lastName].filter(Boolean).join(" ") ||
                user?.username ||
                "";

            const result = await createPayment({
                courseId,
                productType: "Course",
                payerName,
                amount: Number(price),
                currency: "RSD",
            });

            setPayment(result);
        } catch (err) {
            if (err instanceof ApiError && err.status === 401) {
                router.push("/login");
                return;
            }

            const pendingReference =
                err instanceof ApiError ? getPendingReference(err) : null;

            if (pendingReference) {
                setError(
                    `Već imaš uplatnicu na čekanju za ovaj kurs (poziv na broj: ${pendingReference}). Pristup kursu dobijaš čim uplata bude potvrđena.`
                );
            } else if (err instanceof ApiError && err.status === 0) {
                setError("Greška pri povezivanju sa servisom za plaćanje. Pokušaj ponovo.");
            } else {
                setError(
                    err instanceof ApiError
                        ? err.message
                        : "Došlo je do greške prilikom kreiranja uplate."
                );
            }
        } finally {
            setLoading(false);
        }
    }

    if (payment) {
        return (
            <div className="rounded-2xl bg-[#faf8ff] p-4 text-center">
                <p className="font-semibold text-zinc-900">
                    Uplatnica je spremna
                </p>

                <p className="mt-1 text-sm leading-6 text-zinc-600">
                    Skeniraj QR kod u aplikaciji svoje banke ili preuzmi uplatnicu.
                </p>

                {/* eslint-disable-next-line @next/next/no-img-element -- base64 slika sa servera */}
                <img
                    src={`data:image/png;base64,${payment.qrCodeImageBase64}`}
                    alt="IPS QR kod za plaćanje"
                    className="mx-auto mt-4 h-48 w-48 rounded-xl bg-white p-2 shadow-sm"
                />

                <p className="mt-4 text-xs uppercase tracking-wider text-zinc-500">
                    Poziv na broj
                </p>
                <p className="font-semibold tabular-nums text-zinc-900">
                    {payment.referenceNumber}
                </p>

                <a
                    href={`data:application/pdf;base64,${payment.uplatnicaPdfBase64}`}
                    download={`uplatnica-${payment.referenceNumber}.pdf`}
                    className="mt-4 block rounded-full border border-zinc-300 bg-white px-5 py-3 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50"
                >
                    Preuzmi uplatnicu (PDF)
                </a>

                <p className="mt-4 text-xs leading-5 text-zinc-500">
                    Pristup kursu dobijaš čim uplata bude potvrđena.
                </p>
            </div>
        );
    }

    return (
        <div>
            <button
                type="button"
                onClick={handleBuy}
                disabled={loading}
                className="w-full rounded-full bg-[#d8c6ff] px-6 py-3.5 font-semibold text-zinc-900 transition hover:bg-[#c9b0ff] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"
            >
                {loading ? "Kreiram uplatnicu..." : "Kupi kurs"}
            </button>

            {error && (
                <p role="alert" className="mt-3 text-sm leading-6 text-red-600">
                    {error}
                </p>
            )}
        </div>
    );
}
