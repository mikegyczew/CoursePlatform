import { useState } from "react";
import {
  confirmEmail,
  login,
  register,
  requestPasswordReset,
  resetPassword,
  saveAuth,
} from "../services/authService";
import { ThemeToggle, type Theme } from "../components/ThemeToggle";

interface LoginPageProps {
  onLogin: () => void;
  theme: Theme;
  onToggleTheme: () => void;
}

export function LoginPage({
  onLogin,
  theme,
  onToggleTheme,
}: LoginPageProps) {
  const [registerMode, setRegisterMode] = useState(false);
  const [confirmationToken, setConfirmationToken] = useState(
    () => new URLSearchParams(window.location.hash.slice(1)).get("confirmEmail")
  );
  const [resetToken, setResetToken] = useState(
    () => new URLSearchParams(window.location.hash.slice(1)).get("resetPassword")
  );

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [forgotMode, setForgotMode] = useState(false);
  const [confirmationPassword, setConfirmationPassword] = useState("");
  const [confirmationPasswordRepeat, setConfirmationPasswordRepeat] =
    useState("");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function handleSubmit(
    event: React.FormEvent
  ) {
    event.preventDefault();

    try {
      setLoading(true);
      setError(null);
      setNotice(null);

      if (forgotMode) {
        await requestPasswordReset(email);
        setNotice(
          "Jeśli konto z tym adresem istnieje, wyślemy link do zmiany hasła. Sprawdź też folder spam."
        );
      } else if (registerMode) {
        await register(name, email);
        setNotice(
          "Zgłoszenie wysyłki linku potwierdzającego zostało przyjęte. Sprawdź skrzynkę odbiorczą i folder spam. Jeśli wiadomość nie dotrze, jej status sprawdzimy w Mailjet. Po otwarciu linku ustawisz hasło i dokończysz rejestrację."
        );
      } else {
        const result = await login(email, password);
        saveAuth(result);
        onLogin();
      }
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystąpił błąd."
      );
    } finally {
      setLoading(false);
    }
  }

  async function handleConfirmEmail(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setNotice(null);

    if (confirmationPassword !== confirmationPasswordRepeat) {
      setError("Podane hasła nie są takie same.");
      return;
    }

    try {
      setLoading(true);
      if (!confirmationToken) {
        throw new Error("Brakuje tokenu potwierdzającego.");
      }

      const result = await confirmEmail(
        confirmationToken,
        confirmationPassword
      );
      saveAuth(result);
      window.history.replaceState({}, "", window.location.pathname);
      setConfirmationToken(null);
      onLogin();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Nie udało się potwierdzić adresu email."
      );
    } finally {
      setLoading(false);
    }
  }

  async function handleResetPassword(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setNotice(null);

    if (confirmationPassword !== confirmationPasswordRepeat) {
      setError("Podane hasła nie są takie same.");
      return;
    }

    try {
      setLoading(true);
      if (!resetToken) {
        throw new Error("Brakuje tokenu do zmiany hasła.");
      }

      await resetPassword(resetToken, confirmationPassword);
      window.history.replaceState({}, "", window.location.pathname);
      setResetToken(null);
      setConfirmationPassword("");
      setConfirmationPasswordRepeat("");
      setForgotMode(false);
      setRegisterMode(false);
      setNotice("Hasło zostało zmienione. Możesz się zalogować.");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Nie udało się zmienić hasła."
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-page">
      <div className="login-toolbar">
        <ThemeToggle theme={theme} onToggle={onToggleTheme} />
      </div>
      <div className="login-card">
        <div className="login-header">
          <h1>Platforma kursów online</h1>
          <p>Pierwszy krok do lepszego ja</p>
        </div>

        <h2>
          {resetToken
            ? "Ustaw nowe hasło"
            : confirmationToken
            ? "Potwierdź email i ustaw hasło"
            : forgotMode
              ? "Przypomnij hasło"
            : registerMode
              ? "Utwórz konto"
              : "Zaloguj się"}
        </h2>

        {resetToken ? (
          <form onSubmit={handleResetPassword}>
            <label>
              Nowe hasło
              <input
                type="password"
                value={confirmationPassword}
                onChange={(event) =>
                  setConfirmationPassword(event.target.value)
                }
                required
                minLength={6}
                maxLength={100}
                autoComplete="new-password"
              />
            </label>
            <label>
              Powtórz nowe hasło
              <input
                type="password"
                value={confirmationPasswordRepeat}
                onChange={(event) =>
                  setConfirmationPasswordRepeat(event.target.value)
                }
                required
                minLength={6}
                maxLength={100}
                autoComplete="new-password"
              />
            </label>
            {error && <p className="login-error">{error}</p>}
            <button
              type="submit"
              className="login-button"
              disabled={loading}
            >
              {loading ? "Proszę czekać..." : "Zmień hasło"}
            </button>
          </form>
        ) : confirmationToken ? (
          <form onSubmit={handleConfirmEmail}>
            <p className="login-notice">
              Potwierdź adres email i ustaw hasło, aby utworzyć konto.
            </p>
            <label>
              Hasło
              <input
                type="password"
                value={confirmationPassword}
                onChange={(event) =>
                  setConfirmationPassword(event.target.value)
                }
                required
                minLength={6}
                maxLength={100}
                autoComplete="new-password"
              />
            </label>
            <label>
              Powtórz hasło
              <input
                type="password"
                value={confirmationPasswordRepeat}
                onChange={(event) =>
                  setConfirmationPasswordRepeat(event.target.value)
                }
                required
                minLength={6}
                maxLength={100}
                autoComplete="new-password"
              />
            </label>
            {error && <p className="login-error">{error}</p>}
            <button
              type="submit"
              className="login-button"
              disabled={loading}
            >
              {loading ? "Proszę czekać..." : "Potwierdź i utwórz konto"}
            </button>
          </form>
        ) : (
          <form onSubmit={handleSubmit}>
            {forgotMode && (
              <p className="login-hint">
                Reset e-mailem dotyczy kont użytkowników. Hasło
                superadministratora zmienia się w ustawieniach hostingu.
              </p>
            )}
            {registerMode && (
              <label>
                Imię
                <input
                  type="text"
                  value={name}
                  onChange={(event) =>
                    setName(event.target.value)
                  }
                  required
                  minLength={2}
                />
              </label>
            )}

            <label>
              Email
              <input
                type="email"
                value={email}
                onChange={(event) =>
                  setEmail(event.target.value)
                }
                required
              />
            </label>

            {!registerMode && !forgotMode && (
              <label>
                Hasło
                <input
                  type="password"
                  value={password}
                  onChange={(event) =>
                    setPassword(event.target.value)
                  }
                  required
                  minLength={6}
                  maxLength={100}
                  autoComplete="current-password"
                />
              </label>
            )}

            {error && <p className="login-error">{error}</p>}

            {notice && <p className="login-notice">{notice}</p>}

            <button
              type="submit"
              className="login-button"
              disabled={loading}
            >
              {loading
                ? "Proszę czekać..."
                : forgotMode
                  ? "Wyślij link do zmiany hasła"
                  : registerMode
                  ? "Wyślij link potwierdzający"
                  : "Zaloguj się"}
            </button>
            {!registerMode && !forgotMode && (
              <button
                type="button"
                className="login-switch"
                onClick={() => {
                  setForgotMode(true);
                  setError(null);
                  setNotice(null);
                }}
              >
                Nie pamiętasz hasła?
              </button>
            )}
          </form>
        )}

        {!confirmationToken && !resetToken && (
          <button
            type="button"
            className="login-switch"
            onClick={() => {
              if (forgotMode) {
                setRegisterMode(false);
                setForgotMode(false);
              } else {
                setRegisterMode(!registerMode);
              }
              setError(null);
              setNotice(null);
            }}
          >
            {forgotMode
              ? "Wróć do logowania"
              : registerMode
                ? "Mam już konto — zaloguj się"
                : "Nie mam konta — zarejestruj się"}
          </button>
        )}
      </div>
    </main>
  );
}
