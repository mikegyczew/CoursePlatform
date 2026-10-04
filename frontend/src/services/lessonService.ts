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
    if (response.status === 403) {
      const problem = await response.json().catch(() => null) as
        | { title?: string }
        | null;

      if (problem?.title === "Dropbox coupon required.") {
        throw new Error(
          "Aby otworzyć szkolenie Dropbox, najpierw wprowadź kupon."
        );
      }

      if (problem?.title === "Dropbox trial expired.") {
        throw new Error(
          "Dostęp próbny do szkolenia wygasł. Aby kontynuować, należy kupić dostęp."
        );
      }

      throw new Error(
        "Dropbox nie pozwala na dostęp do folderu. Sprawdź uprawnienia aplikacji i folderu."
      );
    }

    if (response.status === 404) {
      throw new Error(
        "Nie znaleziono folderu kursu w Dropboxie. Sprawdź Dropbox__RootFolder, np. /Ekonomia."
      );
    }

    if (response.status === 503) {
      const problem = await response.json().catch(() => null) as
        | { title?: string; detail?: string }
        | null;

      if (problem?.title?.includes("permissions")) {
        throw new Error(
          "Brakuje uprawnień Dropbox API. Włącz wymagane zakresy i połącz aplikację ponownie, używając nowego refresh tokenu."
        );
      }

      if (problem?.title?.includes("authorization")) {
        throw new Error(
          "Autoryzacja Dropbox wygasła lub jest nieprawidłowa. Połącz aplikację ponownie i zaktualizuj refresh token."
        );
      }

      throw new Error(
        "Dropbox nie jest skonfigurowany w backendzie."
      );
    }

    if (response.status === 502) {
      throw new Error(
        "Dropbox odrzucił żądanie. Sprawdź logi backendu."
      );
    }

    throw new Error("Nie udało się pobrać materiałów z Dropboxa.");
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
