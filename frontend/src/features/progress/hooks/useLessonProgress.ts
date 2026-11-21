
"use client";

import {
    useCallback,
    useEffect,
    useMemo,
    useState,
} from "react";

import { useAuth } from "../../../hooks/useAuth";

import {
    isProgressChangeFor,
    LESSON_PROGRESS_EVENT,
    readLessonProgress,
    writeLessonProgress,
} from "../local-progress";

export type LessonProgressSummary = {
    completedCount: number;
    totalCount: number;
    percentage: number;
};

export type LessonSectionsMap = Record<string, string[]>;

export function useLessonProgress(
    courseId: string,
    lessonSections: LessonSectionsMap
) {
    const { user, loading: authLoading } = useAuth();

    const userKey = user?.id || user?.username || null;

    const [completedSectionIds, setCompletedSectionIds] =
        useState<string[]>([]);

    const [loading, setLoading] = useState(true);

    // Stabilan ključ sprečava ponovno registrovanje listenera
    // pri svakom renderu.
    const sectionsKey = JSON.stringify(
        Object.entries(lessonSections)
            .map(([lessonId, sectionIds]) => [
                lessonId,
                [...sectionIds].sort(),
            ] as [string, string[]])
            .sort((a, b) => a[0].localeCompare(b[0]))
    );

    const stableLessonSections = useMemo(
        () =>
            Object.fromEntries(
                JSON.parse(sectionsKey) as [string, string[]][]
            ) as LessonSectionsMap,
        [sectionsKey]
    );

    const allSectionIds = useMemo(
        () =>
            Array.from(
                new Set(
                    Object.values(stableLessonSections).flat()
                )
            ),
        [stableLessonSections]
    );

    useEffect(() => {
        if (authLoading) {
            setLoading(true);
            return;
        }

        if (!userKey) {
            setCompletedSectionIds([]);
            setLoading(false);
            return;
        }

        const refresh = () => {
            const saved = readLessonProgress(userKey, courseId);

            const availableIds = new Set(allSectionIds);

            const validSectionIds =
                saved.completedSectionIds.filter((id) =>
                    availableIds.has(id)
                );

            // Migracija stare verzije:
            // ako je korisnik ranije označio celu lekciju,
            // njene sekcije se prenose kao završene.
            for (const lessonId of saved.completedLessonIds) {
                validSectionIds.push(
                    ...(stableLessonSections[lessonId] ?? [])
                );
            }

            setCompletedSectionIds(
                Array.from(new Set(validSectionIds))
            );
        };

        refresh();
        setLoading(false);

        const handleProgressChange = (event: Event) => {
            if (isProgressChangeFor(event, userKey, courseId)) {
                refresh();
            }
        };

        const handleStorage = (event: StorageEvent) => {
            if (
                event.key === null ||
                event.key.includes(encodeURIComponent(courseId))
            ) {
                refresh();
            }
        };

        window.addEventListener(
            LESSON_PROGRESS_EVENT,
            handleProgressChange
        );

        window.addEventListener("storage", handleStorage);

        return () => {
            window.removeEventListener(
                LESSON_PROGRESS_EVENT,
                handleProgressChange
            );

            window.removeEventListener(
                "storage",
                handleStorage
            );
        };
    }, [
        allSectionIds,
        authLoading,
        courseId,
        stableLessonSections,
        userKey,
    ]);

    const saveSectionIds = useCallback(
        (nextIds: string[]) => {
            if (!userKey) {
                return false;
            }

            const uniqueIds = Array.from(new Set(nextIds));

            const saved = writeLessonProgress(
                userKey,
                courseId,
                uniqueIds
            );

            if (saved) {
                setCompletedSectionIds(uniqueIds);
            }

            return saved;
        },
        [courseId, userKey]
    );

    const markSectionCompleted = useCallback(
        (sectionId: string) => {
            if (!allSectionIds.includes(sectionId)) {
                return false;
            }

            if (completedSectionIds.includes(sectionId)) {
                return true;
            }

            return saveSectionIds([
                ...completedSectionIds,
                sectionId,
            ]);
        },
        [
            allSectionIds,
            completedSectionIds,
            saveSectionIds,
        ]
    );

    const markSectionIncomplete = useCallback(
        (sectionId: string) => {
            return saveSectionIds(
                completedSectionIds.filter(
                    (id) => id !== sectionId
                )
            );
        },
        [completedSectionIds, saveSectionIds]
    );

    const completedSet = useMemo(
        () => new Set(completedSectionIds),
        [completedSectionIds]
    );

    // Progres za svaku pojedinačnu lekciju.
    const lessonProgressById = useMemo(() => {
        const result: Record<
            string,
            LessonProgressSummary
        > = {};

        for (const [lessonId, sectionIds] of Object.entries(
            stableLessonSections
        )) {
            const totalCount = sectionIds.length;

            const completedCount = sectionIds.filter(
                (id) => completedSet.has(id)
            ).length;

            result[lessonId] = {
                completedCount,
                totalCount,
                percentage:
                    totalCount > 0
                        ? Math.round(
                            (completedCount / totalCount) * 100
                        )
                        : 0,
            };
        }

        return result;
    }, [completedSet, stableLessonSections]);

    // Ukupan progres kursa računa se preko svih sekcija.
    const totalCount = allSectionIds.length;

    const completedCount = allSectionIds.filter(
        (id) => completedSet.has(id)
    ).length;

    const percentage =
        totalCount > 0
            ? Math.round(
                (completedCount / totalCount) * 100
            )
            : 0;

    const completedLessonCount = Object.values(
        lessonProgressById
    ).filter(
        (progress) =>
            progress.totalCount > 0 &&
            progress.percentage === 100
    ).length;

    return {
        completedSectionIds,
        lessonProgressById,
        completedCount,
        totalCount,
        percentage,
        completedLessonCount,
        totalLessonCount: Object.keys(
            stableLessonSections
        ).length,
        loading: authLoading || loading,
        canTrackProgress: Boolean(userKey),
        markSectionCompleted,
        markSectionIncomplete,
    };
}
