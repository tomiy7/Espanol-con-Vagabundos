"use client";

// Korisnici (iz Auth-a, samo admin) i kursevi (iz Directus-a), da paneli prikazuju
// imena umesto ID-jeva.

import { useAsync } from "../../hooks/useAsync";
import { getAllUsers, getUserDisplayName, type UserDetail } from "../../lib/api/auth";
import { getCourseOptions, type CourseOption } from "../../lib/api/courses";

export type Directory = {
    users: UserDetail[];
    courses: CourseOption[];
    userById: (id: string | null) => UserDetail | undefined;
    userName: (id: string | null) => string;
    courseName: (id: string | null) => string;
};

async function loadDirectory(): Promise<Directory> {
    const [users, courses] = await Promise.all([getAllUsers(), getCourseOptions()]);

    const usersById = new Map(users.map((user) => [user.id, user]));
    const coursesById = new Map(courses.map((course) => [course.id, course]));

    return {
        users,
        courses,
        userById: (id) => (id ? usersById.get(id) : undefined),
        userName: (id) => {
            const user = id ? usersById.get(id) : undefined;
            return user ? getUserDisplayName(user) : "Nepoznat korisnik";
        },
        courseName: (id) => (id ? coursesById.get(id)?.name : undefined) ?? "Nepoznat kurs",
    };
}

export function useDirectory() {
    return useAsync(loadDirectory);
}
