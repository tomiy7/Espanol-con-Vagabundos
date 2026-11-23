import { apiRequest } from "./http";

export type ProductType = "Course" | "Ebook";

export type CreatePaymentRequest = {
    courseId: string;
    productType: ProductType;
    payerName: string;
    // Privremeno: dok PaymentsService ne bude sam citao cenu kursa (vidi docs/API-CONTRACT.md).
    // Kad backend to uradi, ova dva polja se brisu.
    amount: number;
    currency: string;
};

export type CreatePaymentResponse = {
    paymentId: string;
    referenceNumber: string;
    qrCodeImageBase64: string;
    uplatnicaPdfBase64: string;
};

export type PendingPayment = {
    id: string;
    userId: string;
    courseId: string | null;
    productType: ProductType;
    amount: number;
    currency: string;
    referenceNumber: string;
    createdAt: string;
};

/** Admin i profesor. */
export function getPendingPayments() {
    return apiRequest<PendingPayment[]>("payments", "/payments/pending", { auth: true });
}

/** Samo admin. */
export function confirmPayment(paymentId: string) {
    return apiRequest<{ message: string }>("payments", `/payments/${paymentId}/confirm`, {
        method: "POST",
        auth: true,
    });
}

export function cancelPayment(paymentId: string) {
    return apiRequest<{ message: string }>("payments", `/payments/${paymentId}/cancel`, {
        method: "POST",
        auth: true,
    });
}

export function createPayment(request: CreatePaymentRequest) {
    return apiRequest<CreatePaymentResponse>("payments", "/payments", {
        method: "POST",
        body: request,
        auth: true,
    });
}
