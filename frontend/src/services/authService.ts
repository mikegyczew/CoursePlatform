export interface AuthResponse {
  token: string;
  userId: number;
  email: string;
  name: string;
  role: string;
}

export interface AuthUser {
  userId: number;
  email: string;
  name: string;
  role: string;
}

export const AUTH_REQUIRED_EVENT = "auth:required";

const API_BASE = (import.meta.env.VITE_API_URL ?? "").trim();
const API_URL = API_BASE
  ? `${API_BASE.replace(/\/$/, "")}/api`
  : "/api";

export async function login(
  email: string,
  password: string
): Promise<AuthResponse> {
  const response = await fetch(`${API_URL}/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      email,
      password,
    }),
  });

  if (!response.ok) {
    throw new Error("Nieprawidłowy email lub hasło.");
  }

  return response.json();
}

export async function register(
  name: string,
  email: string
): Promise<void> {
  const response = await fetch(`${API_URL}/auth/register`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      name,
      email,
    }),
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as
      | { title?: string }
      | null;
    throw new Error(
      problem?.title ?? "Nie udało się rozpocząć rejestracji."
    );
  }
}

export async function confirmEmail(
  token: string,
  password: string
): Promise<AuthResponse> {
  const response = await fetch(`${API_URL}/auth/confirm-email`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ token, password }),
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as
      | { title?: string }
      | null;
    throw new Error(
      problem?.title ?? "Nie udało się potwierdzić adresu email."
    );
  }

  return response.json();
}

export function saveAuth(data: AuthResponse) {
  localStorage.setItem("authToken", data.token);
  localStorage.setItem("authUser", JSON.stringify({
    userId: data.userId,
    email: data.email,
    name: data.name,
    role: data.role,
  }));
}

export function getAuthToken(): string | null {
  return localStorage.getItem("authToken");
}

export function getAuthUser(): AuthUser | null {
  const data = localStorage.getItem("authUser");

  if (!data) {
    return null;
  }

  try {
    const user: unknown = JSON.parse(data);
    if (
      typeof user !== "object"
      || user === null
      || !("userId" in user)
      || !("email" in user)
      || !("name" in user)
      || !("role" in user)
      || typeof user.userId !== "number"
      || typeof user.email !== "string"
      || typeof user.name !== "string"
      || typeof user.role !== "string"
    ) {
      return null;
    }

    return {
      userId: user.userId,
      email: user.email,
      name: user.name,
      role: user.role,
    };
  } catch {
    return null;
  }
}

export function logout() {
  localStorage.removeItem("authToken");
  localStorage.removeItem("authUser");
}

export function handleUnauthorized() {
  logout();
  window.dispatchEvent(new Event(AUTH_REQUIRED_EVENT));
}
