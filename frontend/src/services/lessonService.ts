import type { Lesson } from "../types/lesson";
import { getAuthToken, handleUnauthorized } from "./authService";

const API_BASE = (import.meta.env.VITE_API_URL ?? "").trim();
const API_URL = API_BASE
  ? `${API_BASE.replace(/\/$/, "")}/api`
  : "/api";
const DROPBOX_COURSE_ID = -1;

async function fetchDropboxJson<T>(url: string): Promise<T> {
  const token = getAuthToken();
  const response = await fetch(url, {
    headers: token
      ? { Authorization: `Bearer ${token}` }
      : undefined,
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    const message =
      response.status === 503
        ? "Dropbox nie jest skonfigurowany w backendzie."
        : response.status === 502
          ? "Backend nie może odczytać folderu Dropbox."
          : "Nie udało się pobrać materiałów z Dropboxa.";
    throw new Error(message);
  }

  return response.json();
}

export async function getLessons(
  courseId: number
): Promise<Lesson[]> {
  if (courseId === DROPBOX_COURSE_ID) {
    return fetchDropboxJson<Lesson[]>(
      `${API_URL}/dropbox/courses/${courseId}/lessons`
    );
  }

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
  if (courseId === DROPBOX_COURSE_ID) {
    return fetchDropboxJson<Lesson>(
      `${API_URL}/dropbox/courses/${courseId}/lessons/${lessonId}`
    );
  }

  const response = await fetch(
    `${API_URL}/courses/${courseId}/lessons/${lessonId}`
  );

  if (!response.ok) {
    throw new Error("Nie udało się pobrać lekcji.");
  }

  return response.json();
}

export async function getDropboxMaterialLink(
  url: string
): Promise<string> {
  const result = await fetchDropboxJson<{ url: string }>(
    `${API_BASE ? API_BASE.replace(/\/$/, "") : ""}${url}`
  );
  return result.url;
}
