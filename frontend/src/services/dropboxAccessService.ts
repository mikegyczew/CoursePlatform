import {
  getAuthToken,
  handleUnauthorized,
} from "./authService";

const API_BASE = (import.meta.env.VITE_API_URL ?? "").trim();
const API_URL = API_BASE
  ? `${API_BASE.replace(/\/$/, "")}/api`
  : "/api";

export interface DropboxTrialAccess {
  hasRedeemedCoupon: boolean;
  hasAccess: boolean;
  expiresAt: string | null;
}

export type DropboxPurchasePlan = "week" | "month" | "forever";

export interface DropboxPurchasePlans {
  weekAvailable: boolean;
  monthAvailable: boolean;
  foreverAvailable: boolean;
  weekPricePln: number;
  monthPricePln: number;
  foreverPricePln: number;
}

async function requestAccess(
  url: string,
  method = "GET",
  body?: object
): Promise<DropboxTrialAccess> {
  const token = getAuthToken();
  if (!token) {
    throw new Error("Zaloguj się, aby aktywować dostęp do szkolenia.");
  }

  const response = await fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    if (response.status === 409) {
      throw new Error("Ten kupon został już wykorzystany.");
    }

    const problem = await response.json().catch(() => null) as
      | { title?: string }
      | null;
    throw new Error(
      problem?.title ?? "Nie udało się sprawdzić dostępu do szkolenia."
    );
  }

  return response.json();
}

export function getDropboxTrialAccess(
  courseId: number
): Promise<DropboxTrialAccess> {
  return requestAccess(`${API_URL}/dropbox/access?courseId=${courseId}`);
}

export function redeemDropboxCoupon(
  courseId: number,
  coupon: string
): Promise<DropboxTrialAccess> {
  return requestAccess(
    `${API_URL}/dropbox/access/redeem`,
    "POST",
    { coupon, courseId }
  );
}

export async function getDropboxPurchasePlans(): Promise<DropboxPurchasePlans> {
  const token = getAuthToken();
  if (!token) {
    throw new Error("Zaloguj się, aby sprawdzić metody zakupu.");
  }

  const response = await fetch(`${API_URL}/dropbox/access/plans`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    throw new Error("Nie udało się sprawdzić dostępnych metod zakupu.");
  }

  return response.json();
}

export async function startDropboxPurchase(
  courseId: number,
  type: DropboxPurchasePlan
): Promise<DropboxTrialAccess> {
  const token = getAuthToken();
  if (!token) {
    throw new Error("Zaloguj się, aby kupić dostęp do szkolenia.");
  }

  const response = await fetch(`${API_URL}/dropbox/access/purchase`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ type, courseId }),
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as
      | { title?: string }
      | null;
    throw new Error(
      problem?.title ?? "Nie udało się aktywować dostępu."
    );
  }

  return response.json();
}
