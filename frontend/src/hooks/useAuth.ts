"use client";

import { useEffect, useState } from "react";

type User = {
    username?: string;
    firstName?: string;
    lastName?: string;
    gender?: string;
};

function decodeToken(token: string): User | null {
    try {
        const base64Url = token.split(".")[1];

        const base64 = base64Url
            .replace(/-/g, "+")
            .replace(/_/g, "/");

        const payload = JSON.parse(
            decodeURIComponent(
                window
                    .atob(base64)
                    .split("")
                    .map(
                        (c) =>
                            "%" +
                            (
                                "00" +
                                c.charCodeAt(0).toString(16)
                            ).slice(-2)
                    )
                    .join("")
            )
        );

        console.log("JWT PAYLOAD:", payload);

        return {
            username:
                payload.username ||
                payload.unique_name ||
                payload[
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
                    ],

            firstName:
                payload.firstName ||
                payload.firstname ||
                payload.given_name ||
                payload[
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname"
                    ],

            lastName:
                payload.lastName ||
                payload.lastname ||
                payload.family_name ||
                payload[
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname"
                    ],

            gender:
                payload.gender ||
                payload.Gender ||
                payload[
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/gender"
                    ]
        };
    } catch (error) {
        console.error("Error decoding token:", error);

        return null;
    }
}

export function useAuth() {
    const [user, setUser] =
        useState<User | null>(null);

    const [loading, setLoading] =
        useState(true);

    function checkAuth() {
        const token =
            localStorage.getItem("accessToken");

        if (!token) {
            setUser(null);
            setLoading(false);

            return;
        }

        const decodedUser =
            decodeToken(token);

        console.log(
            "DECODED USER:",
            decodedUser
        );

        setUser(decodedUser);
        setLoading(false);
    }

    useEffect(() => {
        checkAuth();

        function handleAuthChange() {
            checkAuth();
        }

        window.addEventListener(
            "auth-change",
            handleAuthChange
        );

        return () => {
            window.removeEventListener(
                "auth-change",
                handleAuthChange
            );
        };
    }, []);

    function logout() {
        localStorage.removeItem(
            "accessToken"
        );

        localStorage.removeItem(
            "refreshToken"
        );

        setUser(null);

        window.dispatchEvent(
            new Event("auth-change")
        );
    }

    return {
        user,
        isAuthenticated: !!user,
        loading,
        logout
    };
}