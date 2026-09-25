import type { Course } from "../types/course";

const API_URL = "http://127.0.0.1:5246/api";

export async function getCourses(): Promise<Course[]> {
  const response = await fetch(`${API_URL}/courses`);

  if (!response.ok) {
    throw new Error("Nie udało się pobrać kursów.");
  }

  return response.json();
}
