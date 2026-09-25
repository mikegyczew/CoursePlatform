export interface AuthResponse {
  token: string;
  userId: number;
  email: string;
  name: string;
}

const API_URL = "http://127.0.0.1:5246/api";

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
  email: string,
  password: string
): Promise<AuthResponse> {
  const response = await fetch(`${API_URL}/auth/register`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      name,
      email,
      password,
    }),
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || "Nie udało się utworzyć konta.");
  }

  return response.json();
}

export function saveAuth(data: AuthResponse) {
  localStorage.setItem("authToken", data.token);
  localStorage.setItem("authUser", JSON.stringify({
    userId: data.userId,
    email: data.email,
    name: data.name,
  }));
}

export function getAuthToken(): string | null {
  return localStorage.getItem("authToken");
}

export function getAuthUser() {
  const data = localStorage.getItem("authUser");

  if (!data) {
    return null;
  }

  try {
    return JSON.parse(data);
  } catch {
    return null;
  }
}

export function logout() {
  localStorage.removeItem("authToken");
  localStorage.removeItem("authUser");
}
