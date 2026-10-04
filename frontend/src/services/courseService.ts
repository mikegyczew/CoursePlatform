import type { Course } from "../types/course";

const API_BASE = (import.meta.env.VITE_API_URL ?? "").trim();
const API_URL = API_BASE
  ? `${API_BASE.replace(/\/$/, "")}/api`
  : "/api";

export async function getCourses(): Promise<Course[]> {
  const [coursesResponse, dropboxCoursesResponse] = await Promise.all([
    fetch(`${API_URL}/courses`),
    fetch(`${API_URL}/dropbox/courses`),
  ]);

  if (!coursesResponse.ok || !dropboxCoursesResponse.ok) {
    throw new Error("Nie udało się pobrać kursów. Sprawdź połączenie z Dropboxem.");
  }

  const [courses, dropboxCourses] = await Promise.all([
    coursesResponse.json() as Promise<Course[]>,
    dropboxCoursesResponse.json() as Promise<Course[]>,
  ]);

  return [...courses, ...dropboxCourses];
}
