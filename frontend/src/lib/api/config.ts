// Adrese servisa. process.env.NEXT_PUBLIC_* mora biti napisan doslovno,
// jer ga Next.js zamenjuje vrednoscu u vreme build-a.
export const API_URLS = {
    auth: process.env.NEXT_PUBLIC_AUTH_URL ?? "",
    courses: process.env.NEXT_PUBLIC_COURSES_URL ?? "",
    payments: process.env.NEXT_PUBLIC_PAYMENTS_URL ?? "",
    directus: process.env.NEXT_PUBLIC_DIRECTUS_URL ?? "",
} as const;

export type ApiService = keyof typeof API_URLS;
