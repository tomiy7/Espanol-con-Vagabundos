import type { Metadata } from "next";
import { Poppins } from "next/font/google";
import "./globals.css";
import Header from "./Header";
import Footer from "./Footer";

const poppins = Poppins({
    variable: "--font-poppins",
    subsets: ["latin"],
    weight: ["400", "500", "600", "700"],
});

export const metadata: Metadata = {
    title: "Español con Vagabundos",
    description: "Naučite španski uz pravo iskustvo",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
    return (
        <html
            lang="sr"
            className={`${poppins.variable} h-full`}
        >
        <body className="min-h-full flex flex-col">
        <Header />

        <div className="flex flex-col flex-1">
            {children}
        </div>

        <Footer />
        </body>
        </html>
    );
}