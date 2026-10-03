import type { Course } from "../types/course";

const API_BASE = (import.meta.env.VITE_API_URL ?? "").trim();
const API_URL = API_BASE
  ? `${API_BASE.replace(/\/$/, "")}/api`
  : "/api";

export async function getCourses(): Promise<Course[]> {
  const response = await fetch(`${API_URL}/courses`);

  if (!response.ok) {
    throw new Error("Nie udało się pobrać kursów.");
  }

  return response.json();
}
