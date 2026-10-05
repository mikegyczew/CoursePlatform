import { useCallback, useEffect, useState } from "react";
import {
  deleteAdminUser,
  getAdminDashboard,
  type AdminDashboard,
} from "../services/adminService";

interface AdminPageProps {
  onBack: () => void;
}

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
  dateStyle: "medium",
  timeStyle: "short",
});

function formatDate(value: string | null): string {
  return value ? dateFormatter.format(new Date(value)) : "Bezterminowy";
}

export function AdminPage({ onBack }: AdminPageProps) {
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setDashboard(await getAdminDashboard());
    } catch (loadError) {
      setError(
        loadError instanceof Error
          ? loadError.message
          : "Nie udało się wczytać panelu administratora."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    getAdminDashboard()
      .then((data) => {
        if (!cancelled) setDashboard(data);
      })
      .catch((loadError: unknown) => {
        if (!cancelled) {
          setError(
            loadError instanceof Error
              ? loadError.message
              : "Nie udało się wczytać panelu administratora."
          );
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  async function removeUser(userId: number, email: string) {
    if (
      !window.confirm(
        `Usunąć użytkownika ${email}? Usunięte zostaną też jego kupony i postęp.`
      )
    ) {
      return;
    }

    setDeletingUserId(userId);
    setError(null);
    try {
      await deleteAdminUser(userId);
      await reload();
    } catch (deleteError) {
      setError(
        deleteError instanceof Error
          ? deleteError.message
          : "Nie udało się usunąć użytkownika."
      );
    } finally {
      setDeletingUserId(null);
    }
  }

  return (
    <main className="container admin-page">
      <div className="admin-page-heading">
        <div>
          <h2>Panel superadministratora</h2>
          <p>Podgląd danych użytkowników, kursów, kuponów i postępów.</p>
        </div>
        <div className="admin-page-actions">
          <button
            type="button"
            onClick={() => void reload()}
            disabled={loading}
          >
            Odśwież
          </button>
          <button type="button" onClick={onBack}>Powrót</button>
        </div>
      </div>

      {error && <p className="status error">{error}</p>}
      {loading && <p className="status">Ładowanie danych...</p>}

      {!loading && dashboard && (
        <>
          <section className="admin-section">
            <h3>Użytkownicy ({dashboard.users.length})</h3>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Użytkownik</th>
                    <th>Email</th>
                    <th>Potwierdzony</th>
                    <th>Utworzono</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {dashboard.users.map((user) => (
                    <tr key={user.id}>
                      <td>{user.name}</td>
                      <td>{user.email}</td>
                      <td>{user.emailConfirmed ? "Tak" : "Nie"}</td>
                      <td>{formatDate(user.createdAt)}</td>
                      <td>
                        <button
                          type="button"
                          className="admin-delete-button"
                          disabled={deletingUserId === user.id}
                          onClick={() => void removeUser(user.id, user.email)}
                        >
                          {deletingUserId === user.id ? "Usuwanie..." : "Usuń"}
                        </button>
                      </td>
                    </tr>
                  ))}
                  {dashboard.users.length === 0 && (
                    <tr><td colSpan={5}>Brak użytkowników.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          <section className="admin-section">
            <h3>Oczekujące potwierdzenia ({dashboard.pendingRegistrations.length})</h3>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr><th>Użytkownik</th><th>Email</th><th>Rejestracja</th><th>Link wygasa</th></tr>
                </thead>
                <tbody>
                  {dashboard.pendingRegistrations.map((registration) => (
                    <tr key={registration.email}>
                      <td>{registration.name}</td>
                      <td>{registration.email}</td>
                      <td>{formatDate(registration.createdAt)}</td>
                      <td>{formatDate(registration.expiresAt)}</td>
                    </tr>
                  ))}
                  {dashboard.pendingRegistrations.length === 0 && (
                    <tr><td colSpan={4}>Brak oczekujących rejestracji.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          <section className="admin-section">
            <h3>Kursy ({dashboard.courses.length})</h3>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr><th>ID</th><th>Nazwa</th><th>Kategoria</th><th>Lekcje</th></tr>
                </thead>
                <tbody>
                  {dashboard.courses.map((course) => (
                    <tr key={course.id}>
                      <td>{course.id}</td>
                      <td>{course.title}</td>
                      <td>{course.category}</td>
                      <td>{course.lessonCount}</td>
                    </tr>
                  ))}
                  {dashboard.courses.length === 0 && (
                    <tr><td colSpan={4}>Brak lokalnych kursów.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          <section className="admin-section">
            <h3>Dostęp i kupony ({dashboard.accessGrants.length})</h3>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr><th>Email</th><th>Kurs</th><th>Typ</th><th>Aktywowano</th><th>Wygasa</th></tr>
                </thead>
                <tbody>
                  {dashboard.accessGrants.map((grant) => (
                    <tr key={grant.id}>
                      <td>{grant.email}</td>
                      <td>{grant.coursePath ?? "Dropbox"}</td>
                      <td>{grant.type}</td>
                      <td>{formatDate(grant.redeemedAt)}</td>
                      <td>{formatDate(grant.expiresAt)}</td>
                    </tr>
                  ))}
                  {dashboard.accessGrants.length === 0 && (
                    <tr><td colSpan={5}>Brak aktywacji kuponów.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          <section className="admin-section">
            <h3>Postęp lekcji ({dashboard.lessonProgress.length})</h3>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr><th>Email</th><th>Kurs</th><th>Lekcja</th><th>Status</th><th>Ukończono</th></tr>
                </thead>
                <tbody>
                  {dashboard.lessonProgress.map((progress, index) => (
                    <tr key={`${progress.userId}-${progress.course}-${progress.lesson}-${index}`}>
                      <td>{progress.email}</td>
                      <td>{progress.course}</td>
                      <td>{progress.lesson}</td>
                      <td>{progress.isCompleted ? "Ukończona" : "W toku"}</td>
                      <td>{formatDate(progress.completedAt)}</td>
                    </tr>
                  ))}
                  {dashboard.lessonProgress.length === 0 && (
                    <tr><td colSpan={5}>Brak zapisanych postępów.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </main>
  );
}
