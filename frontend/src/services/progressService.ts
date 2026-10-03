import {
  getAuthToken,
  handleUnauthorized,
} from "./authService";

export interface LessonProgress {
  lessonId: number;
  isCompleted: boolean;
  completedAt: string | null;
}

const API_BASE = (import.meta.env.VITE_API_URL ?? "").trim();
const API_URL = API_BASE
  ? `${API_BASE.replace(/\/$/, "")}/api`
  : "/api";

function getAuthHeaders(): HeadersInit {
  const token = getAuthToken();

  if (!token) {
    throw new Error(
      "Brak tokena JWT. Użytkownik nie jest zalogowany."
    );
  }

  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
}

export async function getCourseProgress(
  courseId: number
): Promise<LessonProgress[]> {
  const response = await fetch(
    `${API_URL}/courses/${courseId}/progress`,
    {
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    const responseText = await response.text();

    if (response.status === 401) {
      handleUnauthorized();
    }

    console.error(
      "GET progress:",
      response.status,
      responseText
    );

    throw new Error(
      "Nie udało się pobrać postępu kursu."
    );
  }

  return response.json();
}

export async function setLessonCompleted(
  courseId: number,
  lessonId: number,
  completed: boolean
): Promise<void> {
  const response = await fetch(
    `${API_URL}/courses/${courseId}/progress/${lessonId}`,
    {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify({
        lessonId,
        isCompleted: completed,
      }),
    }
  );

  if (!response.ok) {
    const responseText = await response.text();

    if (response.status === 401) {
      handleUnauthorized();
    }

    console.error(
      "PUT progress:",
      response.status,
      responseText
    );

    throw new Error(
      "Nie udało się zapisać postępu."
    );
  }
}
