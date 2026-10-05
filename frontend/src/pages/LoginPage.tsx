import { useState } from "react";
import {
  confirmEmail,
  login,
  register,
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

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
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

      if (registerMode) {
        await register(name, email);
        setNotice(
          "Wysłaliśmy link potwierdzający na podany adres email. Po otwarciu linku ustawisz hasło i dokończysz rejestrację."
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

  return (
    <main className="login-page">
      <ThemeToggle theme={theme} onToggle={onToggleTheme} />
      <div className="login-card">
        <div className="login-header">
          <h1>Platforma kursów online</h1>
          <p>Pierwszy krok do lepszego ja</p>
        </div>

        <h2>
          {confirmationToken
            ? "Potwierdź email i ustaw hasło"
            : registerMode
              ? "Utwórz konto"
              : "Zaloguj się"}
        </h2>

        {confirmationToken ? (
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

            {!registerMode && (
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
                : registerMode
                  ? "Wyślij link potwierdzający"
                  : "Zaloguj się"}
            </button>
          </form>
        )}

        {!confirmationToken && (
          <button
            type="button"
            className="login-switch"
            onClick={() => {
              setRegisterMode(!registerMode);
              setError(null);
              setNotice(null);
            }}
          >
            {registerMode
              ? "Mam już konto — zaloguj się"
              : "Nie mam konta — zarejestruj się"}
          </button>
        )}
      </div>
    </main>
  );
}
