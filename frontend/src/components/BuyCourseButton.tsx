"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

type BuyCourseButtonProps = {
    courseId: string;
    courseName: string;
    price: string;
};

export default function BuyCourseButton({
                                            courseId,
                                            courseName,
                                            price,
                                        }: BuyCourseButtonProps) {
    const router = useRouter();

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    async function handleBuy() {
        const token = localStorage.getItem("accessToken");

        // Ako nije ulogovan
        if (!token) {
            router.push("/login");
            return;
        }

        setLoading(true);
        setError("");

        try {
            const res = await fetch(
                `${process.env.NEXT_PUBLIC_PAYMENTS_URL}/payments`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        Authorization: `Bearer ${token}`,
                    },
                    body: JSON.stringify({
                        courseId,
                        productType: "Course",
                        payerName: "", // ovo ćemo odmah sledeće rešiti
                        amount: Number(price),
                        currency: "RSD",
                    }),
                }
            );

            const data = await res.json();

            if (!res.ok) {
                setError(
                    data.message ||
                    "Došlo je do greške prilikom kreiranja uplate."
                );
                return;
            }

            console.log("PAYMENT CREATED:", data);

        } catch {
            setError("Greška pri povezivanju sa payment servisom.");
        } finally {
            setLoading(false);
        }
    }

    return (
        <div>
            <button
                onClick={handleBuy}
                disabled={loading}
                className="rounded-full bg-black px-6 py-3 font-medium text-white transition hover:opacity-90 disabled:opacity-50"
            >
                {loading ? "Kreiram uplatu..." : "Kupi kurs"}
            </button>

            {error && (
                <p className="mt-3 text-sm text-red-600">
                    {error}
                </p>
            )}
        </div>
    );
}