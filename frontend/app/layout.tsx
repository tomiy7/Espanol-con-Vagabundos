import type { Metadata } from "next";
import { Poppins } from "next/font/google";
import "./globals.css";
import Header from "./Header";
import Footer from "./Footer";

// Poppins je font koji globals.css vec trazi (--font-poppins)
const poppins = Poppins({
    variable: "--font-poppins",
    subsets: ["latin", "latin-ext"],
    weight: ["400", "500", "600", "700"],
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
        <body className={`${poppins.variable} antialiased`}>
        <Header />
        {children}
        <Footer />
        </body>
        </html>
    );
}