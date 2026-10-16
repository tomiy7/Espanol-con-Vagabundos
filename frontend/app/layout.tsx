import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import Header from "./Header";
import Footer from "./Footer";

const geistSans = Geist({
    variable: "--font-geist-sans",
    subsets: ["latin"],
});

const geistMono = Geist_Mono({
    variable: "--font-geist-mono",
    subsets: ["latin"],
});

export const metadata: Metadata = {
    title: {
        // Pocetna (i sve strane bez svog naslova)
        default: "Español con Vagabundos",
        // Ostale strane: "Prijava | Español con Vagabundos"
        template: "%s | Español con Vagabundos",
    },
    description: "Español con Vagabundos – kursevi španskog jezika",
};

export default function RootLayout({
                                       children,
                                   }: Readonly<{
    children: React.ReactNode;
}>) {
    return (
        <html lang="sr">
        <body
            className={`${geistSans.variable} ${geistMono.variable} antialiased`}
        >
        <Header />
        {children}
        <Footer />
        </body>
        </html>
    );
}