import { getAuthToken, handleUnauthorized } from "./authService";

const API_BASE = (import.meta.env.VITE_API_URL ?? "").trim();
const API_URL = API_BASE
  ? `${API_BASE.replace(/\/$/, "")}/api`
  : "/api";

export interface AdminDashboard {
  users: AdminUser[];
  pendingRegistrations: AdminPendingRegistration[];
  courses: AdminCourse[];
  accessGrants: AdminAccessGrant[];
  lessonProgress: AdminLessonProgress[];
}

export interface AdminPendingRegistration {
  email: string;
  name: string;
  createdAt: string;
  expiresAt: string;
}

export interface AdminUser {
  id: number;
  email: string;
  name: string;
  emailConfirmed: boolean;
  createdAt: string;
}

export interface AdminCourse {
  id: number;
  title: string;
  category: string;
  lessonCount: number;
}

export interface AdminAccessGrant {
  id: number;
  userId: number;
  email: string;
  coursePath: string | null;
  type: string;
  redeemedAt: string;
  expiresAt: string | null;
}

export interface AdminLessonProgress {
  userId: number;
  email: string;
  course: string;
  lesson: string;
  isCompleted: boolean;
  completedAt: string | null;
}

async function request<T>(path: string, method = "GET"): Promise<T> {
  const token = getAuthToken();
  if (!token) {
    throw new Error("Sesja administratora wygasła. Zaloguj się ponownie.");
  }

  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}` },
  });

  if (response.status === 401) handleUnauthorized();
  if (response.status === 403) {
    throw new Error("Brak uprawnień superadministratora.");
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as
      | { title?: string }
      | null;
    throw new Error(problem?.title ?? "Nie udało się wykonać operacji.");
  }

  if (response.status === 204) return undefined as T;
  return response.json();
}

export function getAdminDashboard(): Promise<AdminDashboard> {
  return request("/admin/dashboard");
}

export function deleteAdminUser(userId: number): Promise<void> {
  return request(`/admin/users/${userId}`, "DELETE");
}
