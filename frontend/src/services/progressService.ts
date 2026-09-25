export interface LessonProgress {
  lessonId: number;
  isCompleted: boolean;
  completedAt: string | null;
}

const API_URL = "http://127.0.0.1:5246/api";

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem("authToken");

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
    throw new Error(
      "Nie udało się zapisać postępu."
    );
  }
}
