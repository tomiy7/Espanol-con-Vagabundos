"use client";

import { useState } from "react";
import { useAsync } from "../../../src/hooks/useAsync";
import { useDirectory } from "../../../src/features/admin/useDirectory";
import { formatDateTime } from "../../../src/features/groups/labels";
import { EmptyState, ErrorState, LoadingState, errorText } from "../../../src/components/PanelState";
import {
    cancelPayment,
    confirmPayment,
    getPendingPayments,
    type PendingPayment,
} from "../../../src/lib/api/payments";

type Notice = { type: "success" | "error"; text: string };

export default function AdminPaymentsPage() {
    const payments = useAsync(getPendingPayments);
    const directory = useDirectory();

    const [busyId, setBusyId] = useState<string | null>(null);
    const [handledIds, setHandledIds] = useState<string[]>([]);
    const [notice, setNotice] = useState<Notice | null>(null);

    const pending = (payments.data ?? []).filter((p) => !handledIds.includes(p.id));

    function describe(payment: PendingPayment) {
        const who = directory.data?.userName(payment.userId) ?? "korisnika";
        return `${who}, poziv na broj ${payment.referenceNumber}`;
    }

    async function handle(payment: PendingPayment, action: "confirm" | "cancel") {
        const question =
            action === "confirm"
                ? `Potvrditi uplatu (${describe(payment)})? Proveri prvo da je novac stigao na račun.`
                : `Otkazati uplatu (${describe(payment)})?`;

        if (!window.confirm(question)) return;

        setBusyId(payment.id);
        setNotice(null);

        try {
            if (action === "confirm") {
                await confirmPayment(payment.id);
            } else {
                await cancelPayment(payment.id);
            }

            setHandledIds((ids) => [...ids, payment.id]);
            setNotice({
                type: "success",
                text: action === "confirm" ? `Uplata potvrđena: ${describe(payment)}.` : `Uplata otkazana: ${describe(payment)}.`,
            });
        } catch (error) {
            setNotice({ type: "error", text: errorText(error, "Servis za plaćanje") });
        } finally {
            setBusyId(null);
        }
    }

    return (
        <section aria-labelledby="payments-heading">
            <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
                <div>
                    <h2 id="payments-heading" className="text-2xl font-bold text-zinc-900">
                        Uplate na čekanju
                    </h2>
                    <p className="mt-1 text-sm text-zinc-600">
                        Kad novac stigne na račun, potvrdi uplatu i polaznik dobija pristup kursu.
                    </p>
                </div>

                <button
                    type="button"
                    onClick={() => {
                        setHandledIds([]);
                        setNotice(null);
                        payments.reload();
                    }}
                    className="rounded-full border border-zinc-200 bg-white px-4 py-2 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50"
                >
                    Osveži
                </button>
            </div>

            {notice && (
                <p
                    role="status"
                    className={`mb-5 rounded-2xl px-4 py-3 text-sm ${
                        notice.type === "success"
                            ? "border border-emerald-200 bg-emerald-50 text-emerald-800"
                            : "border border-red-200 bg-red-50 text-red-700"
                    }`}
                >
                    {notice.text}
                </p>
            )}

            {directory.error !== undefined && !directory.loading && (
                <p className="mb-5 rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
                    Imena korisnika i kurseva nisu učitana ({errorText(directory.error, "Servis")}).
                </p>
            )}

            {payments.loading ? (
                <LoadingState text="Učitavanje uplata..." />
            ) : payments.error !== undefined ? (
                <ErrorState error={payments.error} serviceName="Servis za plaćanje" onRetry={payments.reload} />
            ) : pending.length === 0 ? (
                <EmptyState>Nema uplata na čekanju.</EmptyState>
            ) : (
                <ul className="flex flex-col gap-3">
                    {pending.map((payment) => {
                        const user = directory.data?.userById(payment.userId);
                        const busy = busyId === payment.id;

                        return (
                            <li
                                key={payment.id}
                                className="flex flex-col gap-4 rounded-2xl border border-zinc-200 bg-white p-5 sm:flex-row sm:items-center sm:justify-between"
                            >
                                <div className="min-w-0">
                                    <p className="font-semibold text-zinc-900">
                                        {directory.data?.userName(payment.userId) ?? "Učitavanje..."}
                                    </p>
                                    {user && (
                                        <p className="mt-0.5 break-all text-sm text-zinc-500">
                                            {user.email}
                                            {user.phone ? ` · ${user.phone}` : ""}
                                        </p>
                                    )}
                                    <p className="mt-2 text-sm text-zinc-700">
                                        {payment.productType === "Ebook" ? "E-knjiga: " : "Kurs: "}
                                        {directory.data?.courseName(payment.courseId) ?? "..."}
                                    </p>
                                    <p className="mt-1 text-xs text-zinc-500">
                                        Poziv na broj{" "}
                                        <span className="font-semibold tabular-nums text-zinc-800">
                                            {payment.referenceNumber}
                                        </span>{" "}
                                        · {formatDateTime(payment.createdAt)}
                                    </p>
                                </div>

                                <div className="flex shrink-0 flex-col items-start gap-3 sm:items-end">
                                    <p className="text-lg font-bold tabular-nums text-zinc-900">
                                        {payment.amount.toLocaleString("sr-RS")} {payment.currency}
                                    </p>
                                    <div className="flex gap-2">
                                        <button
                                            type="button"
                                            disabled={busyId !== null}
                                            onClick={() => handle(payment, "cancel")}
                                            className="rounded-full border border-zinc-200 px-4 py-2 text-sm font-medium text-zinc-700 transition hover:border-red-200 hover:bg-red-50 hover:text-red-700 disabled:opacity-50"
                                        >
                                            Otkaži
                                        </button>
                                        <button
                                            type="button"
                                            disabled={busyId !== null}
                                            onClick={() => handle(payment, "confirm")}
                                            className="rounded-full bg-[#d8c6ff] px-4 py-2 text-sm font-semibold text-zinc-900 transition hover:bg-[#c9b0ff] disabled:opacity-50"
                                        >
                                            {busy ? "Čuvam..." : "Potvrdi uplatu"}
                                        </button>
                                    </div>
                                </div>
                            </li>
                        );
                    })}
                </ul>
            )}
        </section>
    );
}
