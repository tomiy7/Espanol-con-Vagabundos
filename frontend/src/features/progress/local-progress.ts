
export type LessonProgress = {
    completedSectionIds: string[];
    /** Legacy data from the first progress implementation. */
    completedLessonIds: string[];
    updatedAt: string | null;
};

const STORAGE_PREFIX = "ev:lesson-progress:v1";

export const LESSON_PROGRESS_EVENT = "ev:lesson-progress-change";

type ProgressChangeDetail = {
    userKey: string;
    courseId: string;
};

function getStorageKey(userKey: string, courseId: string): string {
    return `${STORAGE_PREFIX}:${encodeURIComponent(userKey)}:${encodeURIComponent(courseId)}`;
}

export function readLessonProgress(
    userKey: string,
    courseId: string
): LessonProgress {
    const emptyProgress: LessonProgress = {
        completedSectionIds: [],
        completedLessonIds: [],
        updatedAt: null,
    };

    if (typeof window === "undefined") {
        return emptyProgress;
    }

    try {
        const raw = window.localStorage.getItem(
            getStorageKey(userKey, courseId)
        );

        if (!raw) {
            return emptyProgress;
        }

        const parsed: unknown = JSON.parse(raw);

        if (typeof parsed !== "object" || parsed === null) {
            return emptyProgress;
        }

        const data = parsed as {
            completedSectionIds?: unknown;
            completedLessonIds?: unknown;
            updatedAt?: unknown;
        };

        return {
            completedSectionIds: Array.isArray(data.completedSectionIds)
                ? Array.from(
                    new Set(
                        data.completedSectionIds.filter(
                            (id): id is string => typeof id === "string"
                        )
                    )
                )
                : [],

            completedLessonIds: Array.isArray(data.completedLessonIds)
                ? Array.from(
                    new Set(
                        data.completedLessonIds.filter(
                            (id): id is string => typeof id === "string"
                        )
                    )
                )
                : [],

            updatedAt:
                typeof data.updatedAt === "string"
                    ? data.updatedAt
                    : null,
        };
    } catch {
        return emptyProgress;
    }
}

export function writeLessonProgress(
    userKey: string,
    courseId: string,
    completedSectionIds: string[]
): boolean {
    if (typeof window === "undefined") {
        return false;
    }

    const progress: LessonProgress = {
        completedSectionIds: Array.from(
            new Set(completedSectionIds)
        ),
        completedLessonIds: [],
        updatedAt: new Date().toISOString(),
    };

    try {
        window.localStorage.setItem(
            getStorageKey(userKey, courseId),
            JSON.stringify(progress)
        );

        window.dispatchEvent(
            new CustomEvent<ProgressChangeDetail>(
                LESSON_PROGRESS_EVENT,
                {
                    detail: {
                        userKey,
                        courseId,
                    },
                }
            )
        );

        return true;
    } catch {
        return false;
    }
}

export function isProgressChangeFor(
    event: Event,
    userKey: string,
    courseId: string
): boolean {
    const detail = (
        event as CustomEvent<ProgressChangeDetail>
    ).detail;

    return (
        detail?.userKey === userKey &&
        detail?.courseId === courseId
    );
}
