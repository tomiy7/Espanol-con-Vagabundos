// frontend/app/api/contact/route.ts
// Salje poruku iz kontakt forme na mejl. Adresa i kljuc su samo na serveru (env).
import { NextResponse } from "next/server";

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function escapeHtml(value: string) {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

export async function POST(req: Request) {
    const body = await req.json().catch(() => null);

    if (!body) {
        return NextResponse.json({ message: "Neispravan zahtev." }, { status: 400 });
    }

    // Honeypot: ako je popunjeno, tiho se pravimo da je uspelo
    if (body.website) {
        return NextResponse.json({ ok: true });
    }

    const name = String(body.name ?? "").trim();
    const email = String(body.email ?? "").trim();
    const message = String(body.message ?? "").trim();

    if (name.length < 2 || name.length > 80) {
        return NextResponse.json({ message: "Unesi ime (2–80 karaktera)." }, { status: 400 });
    }
    if (!EMAIL_RE.test(email) || email.length > 254) {
        return NextResponse.json({ message: "Unesi ispravan email." }, { status: 400 });
    }
    if (message.length < 10 || message.length > 2000) {
        return NextResponse.json(
            { message: "Poruka mora imati između 10 i 2000 karaktera." },
            { status: 400 }
        );
    }

    const apiKey = process.env.RESEND_API_KEY;
    const to = process.env.CONTACT_TO_EMAIL;

    if (!apiKey || !to) {
        return NextResponse.json(
            { message: "Slanje poruka trenutno nije podešeno." },
            { status: 500 }
        );
    }

    const res = await fetch("https://api.resend.com/emails", {
        method: "POST",
        headers: {
            Authorization: `Bearer ${apiKey}`,
            "Content-Type": "application/json",
        },
        body: JSON.stringify({
            from: "Español con Vagabundos <onboarding@resend.dev>",
            to: [to],
            reply_to: email,
            subject: `Nova poruka sa sajta: ${name}`,
            html: `
                <p><strong>Ime:</strong> ${escapeHtml(name)}</p>
                <p><strong>Email:</strong> ${escapeHtml(email)}</p>
                <p><strong>Poruka:</strong></p>
                <p>${escapeHtml(message).replace(/\n/g, "<br>")}</p>
            `,
        }),
    });

    if (!res.ok) {
        return NextResponse.json(
            { message: "Slanje nije uspelo. Pokušaj ponovo kasnije." },
            { status: 502 }
        );
    }

    return NextResponse.json({ ok: true });
}