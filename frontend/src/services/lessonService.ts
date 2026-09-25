import type { Lesson } from "../types/lesson";

const API_URL = "http://127.0.0.1:5246/api";

export async function getLessons(
  courseId: number
): Promise<Lesson[]> {
  const response = await fetch(
    `${API_URL}/courses/${courseId}/lessons`
  );

  if (!response.ok) {
    throw new Error("Nie udało się pobrać lekcji.");
  }

  return response.json();
}

export async function getLesson(
  courseId: number,
  lessonId: number
): Promise<Lesson> {
  const response = await fetch(
    `${API_URL}/courses/${courseId}/lessons/${lessonId}`
  );

  if (!response.ok) {
    throw new Error("Nie udało się pobrać lekcji.");
  }

  return response.json();
}
