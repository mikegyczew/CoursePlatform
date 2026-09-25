import { useState } from "react";
import {
  login,
  register,
  saveAuth,
} from "../services/authService";

interface LoginPageProps {
  onLogin: () => void;
}

export function LoginPage({ onLogin }: LoginPageProps) {
  const [registerMode, setRegisterMode] = useState(false);

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(
    event: React.FormEvent
  ) {
    event.preventDefault();

    try {
      setLoading(true);
      setError(null);

      const result = registerMode
        ? await register(name, email, password)
        : await login(email, password);

      saveAuth(result);
      onLogin();
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

  return (
    <main className="login-page">
      <div className="login-card">
        <div className="login-header">
          <h1>Platforma kursów online</h1>
          <p>Pierwszy krok do lepszego ja</p>
        </div>

        <h2>
          {registerMode
            ? "Utwórz konto"
            : "Zaloguj się"}
        </h2>

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
            />
          </label>

          {error && (
            <p className="login-error">
              {error}
            </p>
          )}

          <button
            type="submit"
            className="login-button"
            disabled={loading}
          >
            {loading
              ? "Proszę czekać..."
              : registerMode
                ? "Utwórz konto"
                : "Zaloguj się"}
          </button>
        </form>

        <button
          type="button"
          className="login-switch"
          onClick={() => {
            setRegisterMode(!registerMode);
            setError(null);
          }}
        >
          {registerMode
            ? "Mam już konto — zaloguj się"
            : "Nie mam konta — zarejestruj się"}
        </button>
      </div>
    </main>
  );
}
