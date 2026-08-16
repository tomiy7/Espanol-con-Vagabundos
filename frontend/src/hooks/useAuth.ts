"use client";

import { useEffect, useState } from "react";

type User = {
    username?: string;
    firstName?: string;
    lastName?: string;
};

function decodeToken(token: string): User | null {
    try {
        const payload = JSON.parse(atob(token.split(".")[1]));

        return {
            username:
                payload.username ||
                payload.unique_name ||
                payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"],

            firstName:
                payload.firstName ||
                payload.given_name ||
                payload[
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname"
                    ],

            lastName:
                payload.lastName ||
                payload.family_name ||
                payload[
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname"
                    ],
        };
    } catch {
        return null;
    }
}

export function useAuth() {
    const [user, setUser] = useState<User | null>(null);
    const [loading, setLoading] = useState(true);

    // function checkAuth() {
    //     const token = localStorage.getItem("accessToken");
    //
    //     if (!token) {
    //         setUser(null);
    //         setLoading(false);
    //         return;
    //     }
    //
    //     const decodedUser = decodeToken(token);
    //
    //     setUser(decodedUser);
    //     setLoading(false);
    // }
    function checkAuth() {
        console.log("AUTH CHECK");

        const token = localStorage.getItem("accessToken");

        console.log("TOKEN:", token);

        if (!token) {
            setUser(null);
            setLoading(false);
            return;
        }

        const decodedUser = decodeToken(token);

        console.log("DECODED USER:", decodedUser);

        setUser(decodedUser);
        setLoading(false);
    }
    //
    // useEffect(() => {
    //     checkAuth();
    //
    //     function handleAuthChange() {
    //         checkAuth();
    //     }
    //
    //     window.addEventListener("auth-change", handleAuthChange);
    //
    //     return () => {
    //         window.removeEventListener("auth-change", handleAuthChange);
    //     };
    // }, []);
    useEffect(() => {
        checkAuth();

        function handleAuthChange() {
            console.log("AUTH CHANGE EVENT!");
            checkAuth();
        }

        window.addEventListener("auth-change", handleAuthChange);

        return () => {
            window.removeEventListener("auth-change", handleAuthChange);
        };
    }, []);
    function logout() {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("refreshToken");

        setUser(null);

        window.dispatchEvent(new Event("auth-change"));
    }

    return {
        user,
        isAuthenticated: !!user,
        loading,
        logout,
    };
}