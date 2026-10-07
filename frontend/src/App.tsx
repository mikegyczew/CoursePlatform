import { useEffect, useState } from "react";
import "./App.css";

import { CourseCard } from "./components/CourseCard";
import { CoursePage } from "./pages/CoursePage";
import { LessonPage } from "./pages/LessonPage";
import { LoginPage } from "./pages/LoginPage";
import { AdminPage } from "./pages/AdminPage";
import { getCourses } from "./services/courseService";
import { ThemeToggle, type Theme } from "./components/ThemeToggle";
import type { DropboxTrialAccess } from "./services/dropboxAccessService";
import {
  AUTH_REQUIRED_EVENT,
  getAuthUser,
  logout,
  type AuthUser,
} from "./services/authService";
import type { Course } from "./types/course";

const THEME_STORAGE_KEY = "coursePlatformTheme";

function getInitialTheme(): Theme {
  return localStorage.getItem(THEME_STORAGE_KEY) === "light"
    ? "light"
    : "dark";
}

interface AppHeaderProps {
  user: AuthUser | null;
  theme: Theme;
  onToggleTheme: () => void;
  onLogout: () => void;
  showAdmin: boolean;
  onToggleAdmin: () => void;
  courseAccess?: DropboxTrialAccess | null;
}

function AppHeader({
  user,
  theme,
  onToggleTheme,
  onLogout,
  showAdmin,
  onToggleAdmin,
  courseAccess,
}: AppHeaderProps) {
  return (
    <header className="header">
      <div className="container header-content">
        <nav className="header-toolbar" aria-label="Nawigacja użytkownika">
          <div className="header-toolbar-left">
            {user?.role === "SuperAdmin" && (
              <button type="button" onClick={onToggleAdmin}>
                {showAdmin ? "Kursy" : "Panel administratora"}
              </button>
            )}
            {courseAccess?.hasAccess && (
              <span className="course-access-badge">
                {courseAccess.expiresAt
                  ? `Dostęp do ${new Date(courseAccess.expiresAt).toLocaleString("pl-PL")}`
                  : "Bezterminowy dostęp"}
              </span>
            )}
          </div>
          <div className="header-toolbar-right">
            {user && <span className="user-name">{user.name}</span>}
            <ThemeToggle theme={theme} onToggle={onToggleTheme} />
            {user && (
              <button type="button" onClick={onLogout}>Wyloguj</button>
            )}
          </div>
        </nav>
        <div className="header-brand">
          <h1>Platforma kursów online</h1>
          <p>
            {showAdmin
              ? "Panel superadministratora"
              : "Pierwszy krok do lepszego ja"}
          </p>
        </div>
      </div>
    </header>
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
  const [courseAccess, setCourseAccess] =
    useState<DropboxTrialAccess | null>(null);

  const [selectedLessonId, setSelectedLessonId] =
    useState<number | null>(null);
  const [showAdmin, setShowAdmin] = useState(false);

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
      setCourseAccess(null);
      setSelectedLessonId(null);
      setShowAdmin(false);
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
        theme={theme}
        onToggleTheme={() =>
          setTheme((current) =>
            current === "dark" ? "light" : "dark"
          )
        }
        onLogin={() => setLoggedIn(true)}
      />
    );
  }

  function handleLogout() {
    logout();
    setLoggedIn(false);
    setSelectedCourse(null);
    setCourseAccess(null);
    setSelectedLessonId(null);
    setShowAdmin(false);
  }

  if (showAdmin && user?.role === "SuperAdmin") {
    return (
      <div className="app">
        <AppHeader
          user={user}
          theme={theme}
          onToggleTheme={() =>
            setTheme((current) =>
              current === "dark" ? "light" : "dark"
            )
          }
          onLogout={handleLogout}
          showAdmin={showAdmin}
          onToggleAdmin={() => setShowAdmin(false)}
        />
        <AdminPage onBack={() => setShowAdmin(false)} />
      </div>
    );
  }

  if (
    selectedCourse &&
    selectedLessonId !== null
  ) {
    return (
      <div className="app">
        <AppHeader
          user={user}
          theme={theme}
          onToggleTheme={() =>
            setTheme((current) =>
              current === "dark" ? "light" : "dark"
            )
          }
          onLogout={handleLogout}
          showAdmin={false}
          onToggleAdmin={() => setShowAdmin(true)}
        />

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
        <AppHeader
          user={user}
          theme={theme}
          onToggleTheme={() =>
            setTheme((current) =>
              current === "dark" ? "light" : "dark"
            )
          }
          onLogout={handleLogout}
          showAdmin={false}
          onToggleAdmin={() => setShowAdmin(true)}
          courseAccess={courseAccess}
        />

        <CoursePage
          course={selectedCourse}
          onBack={() => {
            setSelectedCourse(null);
            setCourseAccess(null);
          }}
          onLessonClick={(lessonId) =>
            setSelectedLessonId(lessonId)
          }
          onAccessChange={setCourseAccess}
        />
      </div>
    );
  }

  return (
    <div className="app">
      <AppHeader
        user={user}
        theme={theme}
        onToggleTheme={() =>
          setTheme((current) =>
            current === "dark" ? "light" : "dark"
          )
        }
        onLogout={handleLogout}
        showAdmin={false}
        onToggleAdmin={() => setShowAdmin(true)}
      />

      <main className="container">
        <section className="courses-section">
          <div className="courses-heading">
            <h2>Dostępne kursy</h2>
          </div>

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
                      {
                        setCourseAccess(null);
                        setSelectedCourse(course);
                      }
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
