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
  title: "Español con Vagabundos",
  description: "Naučite španski uz pravo iskustvo",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
      <html
          lang="sr"
          className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
      >
      <body className="min-h-full flex flex-col">
      <Header />
      <div className="flex flex-col flex-1">{children}</div>
      <Footer />
      </body>
      </html>
  );
}