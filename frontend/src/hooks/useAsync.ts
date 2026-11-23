"use client";

// Ucitavanje podataka u client komponenti: { data, error, loading, reload }.
// key: kad se promeni (npr. id grupe), podaci se ucitavaju ponovo.

import { useCallback, useEffect, useRef, useState } from "react";

type AsyncState<T> = {
    data: T | undefined;
    error: unknown;
    loading: boolean;
};

export function useAsync<T>(loader: () => Promise<T>, key: string = "") {
    const [state, setState] = useState<AsyncState<T>>({
        data: undefined,
        error: undefined,
        loading: true,
    });
    const [version, setVersion] = useState(0);
    const loaderRef = useRef(loader);

    useEffect(() => {
        loaderRef.current = loader;
    });

    useEffect(() => {
        let active = true;

        loaderRef.current().then(
            (data) => {
                if (active) setState({ data, error: undefined, loading: false });
            },
            (error: unknown) => {
                if (active) setState((current) => ({ ...current, error, loading: false }));
            }
        );

        return () => {
            active = false;
        };
    }, [version, key]);

    const reload = useCallback(() => {
        setState((current) => ({ ...current, loading: true }));
        setVersion((current) => current + 1);
    }, []);

    return { ...state, reload };
}
