import { useEffect, useState } from "react";
import "./App.css";

import { CourseCard } from "./components/CourseCard";
import { CoursePage } from "./pages/CoursePage";
import { LessonPage } from "./pages/LessonPage";
import { LoginPage } from "./pages/LoginPage";
import { getCourses } from "./services/courseService";
import {
  AUTH_REQUIRED_EVENT,
  getAuthUser,
  logout,
} from "./services/authService";
import type { Course } from "./types/course";

type Theme = "dark" | "light";

const THEME_STORAGE_KEY = "coursePlatformTheme";

function getInitialTheme(): Theme {
  return localStorage.getItem(THEME_STORAGE_KEY) === "light"
    ? "light"
    : "dark";
}

function ThemeToggle({
  theme,
  onToggle,
}: {
  theme: Theme;
  onToggle: () => void;
}) {
  const label =
    theme === "dark" ? "Włącz jasny motyw" : "Włącz ciemny motyw";

  return (
    <button
      type="button"
      className="theme-toggle"
      onClick={onToggle}
      aria-label={label}
      title={label}
    >
      {theme === "dark" ? (
        <svg
          aria-hidden="true"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2m0 16v2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M2 12h2m16 0h2M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42" />
        </svg>
      ) : (
        <svg
          aria-hidden="true"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M20.9 13A9 9 0 0 1 11 3.1 9 9 0 1 0 20.9 13Z" />
        </svg>
      )}
    </button>
  );
}

function App() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);
  const [loggedIn, setLoggedIn] = useState(
    Boolean(localStorage.getItem("authToken"))
  );

  const [courses, setCourses] = useState<Course[]>([]);
  const [selectedCourse, setSelectedCourse] =
    useState<Course | null>(null);

  const [selectedLessonId, setSelectedLessonId] =
    useState<number | null>(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] =
    useState<string | null>(null);

  const user = getAuthUser();

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  }, [theme]);

  useEffect(() => {
    function handleAuthRequired() {
      setLoggedIn(false);
      setSelectedCourse(null);
      setSelectedLessonId(null);
    }

    window.addEventListener(
      AUTH_REQUIRED_EVENT,
      handleAuthRequired
    );

    return () => {
      window.removeEventListener(
        AUTH_REQUIRED_EVENT,
        handleAuthRequired
      );
    };
  }, []);

  useEffect(() => {
    if (!loggedIn) {
      setLoading(false);
      return;
    }

    async function loadCourses() {
      try {
        const data = await getCourses();
        setCourses(data);
      } catch (err) {
        console.error(err);
        setError("Nie udało się pobrać kursów.");
      } finally {
        setLoading(false);
      }
    }

    loadCourses();
  }, [loggedIn]);

  if (!loggedIn) {
    return (
      <LoginPage
        onLogin={() => setLoggedIn(true)}
      />
    );
  }

  function handleLogout() {
    logout();
    setLoggedIn(false);
    setSelectedCourse(null);
    setSelectedLessonId(null);
  }

  if (
    selectedCourse &&
    selectedLessonId !== null
  ) {
    return (
      <div className="app">
        <header className="header">
          <div className="container header-content">
            <div>
              <h1>Platforma kursów online</h1>
              <p>Pierwszy krok do lepszego ja</p>
            </div>

            <div className="user-area">
              <span>{user?.name}</span>

              <ThemeToggle
                theme={theme}
                onToggle={() =>
                  setTheme((current) =>
                    current === "dark" ? "light" : "dark"
                  )
                }
              />

              <button
                type="button"
                onClick={handleLogout}
              >
                Wyloguj
              </button>
            </div>
          </div>
        </header>

        <LessonPage
          courseId={selectedCourse.id}
          lessonId={selectedLessonId}
          onBack={() => setSelectedLessonId(null)}
          onLessonChange={setSelectedLessonId}
        />
      </div>
    );
  }

  if (selectedCourse) {
    return (
      <div className="app">
        <header className="header">
          <div className="container header-content">
            <div>
              <h1>Platforma kursów online</h1>
              <p>Pierwszy krok do lepszego ja</p>
            </div>

            <div className="user-area">
              <span>{user?.name}</span>

              <ThemeToggle
                theme={theme}
                onToggle={() =>
                  setTheme((current) =>
                    current === "dark" ? "light" : "dark"
                  )
                }
              />

              <button
                type="button"
                onClick={handleLogout}
              >
                Wyloguj
              </button>
            </div>
          </div>
        </header>

        <CoursePage
          course={selectedCourse}
          onBack={() => setSelectedCourse(null)}
          onLessonClick={(lessonId) =>
            setSelectedLessonId(lessonId)
          }
        />
      </div>
    );
  }

  return (
    <div className="app">
      <header className="header">
        <div className="container header-content">
          <div>
            <h1>Platforma kursów online</h1>
            <p>Pierwszy krok do lepszego ja</p>
          </div>

          <div className="user-area">
            <span>{user?.name}</span>

            <ThemeToggle
              theme={theme}
              onToggle={() =>
                setTheme((current) =>
                  current === "dark" ? "light" : "dark"
                )
              }
            />

            <button
              type="button"
              onClick={handleLogout}
            >
              Wyloguj
            </button>
          </div>
        </div>
      </header>

      <main className="container">
        <section className="courses-section">
          <h2>Dostępne kursy</h2>

          {loading && (
            <p className="status">
              Ładowanie kursów...
            </p>
          )}

          {error && (
            <p className="status error">
              {error}
            </p>
          )}

          {!loading &&
            !error &&
            courses.length === 0 && (
              <p className="status">
                Brak dostępnych kursów.
              </p>
            )}

          {!loading &&
            !error &&
            courses.length > 0 && (
              <div className="courses-grid">
                {courses.map((course) => (
                  <CourseCard
                    key={course.id}
                    course={course}
                    onClick={() =>
                      setSelectedCourse(course)
                    }
                  />
                ))}
              </div>
            )}
        </section>
      </main>
    </div>
  );
}

export default App;
