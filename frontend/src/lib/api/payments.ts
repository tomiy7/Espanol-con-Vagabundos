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

export function createPayment(request: CreatePaymentRequest) {
    return apiRequest<CreatePaymentResponse>("payments", "/payments", {
        method: "POST",
        body: request,
        auth: true,
    });
}
