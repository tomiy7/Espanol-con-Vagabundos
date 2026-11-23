import { apiRequest } from "./http";

export type CourseOption = {
    id: string;
    name: string;
};

/** Nazivi kurseva iz Directus-a (za liste i padajuce menije u panelima). */
export async function getCourseOptions() {
    const params = new URLSearchParams({
        fields: "id,name",
        sort: "name",
        limit: "-1",
    });

    const result = await apiRequest<{ data: CourseOption[] }>("directus", `/items/courses?${params}`);

    return result.data ?? [];
}
