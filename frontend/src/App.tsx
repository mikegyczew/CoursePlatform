import { useEffect, useState } from "react";
import "./App.css";

import { CourseCard } from "./components/CourseCard";
import { CoursePage } from "./pages/CoursePage";
import { LessonPage } from "./pages/LessonPage";
import { getCourses } from "./services/courseService";
import type { Course } from "./types/course";

function App() {
  const [courses, setCourses] = useState<Course[]>([]);
  const [selectedCourse, setSelectedCourse] = useState<Course | null>(null);
  const [selectedLessonId, setSelectedLessonId] = useState<number | null>(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
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
  }, []);

  if (selectedCourse && selectedLessonId !== null) {
    return (
      <div className="app">
        <header className="header">
          <div className="container">
            <h1>Platforma kursów online</h1>
            <p>Pierwszy krok do lepszego ja</p>
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
          <div className="container">
            <h1>Platforma kursów online</h1>
            <p>Pierwszy krok do lepszego ja</p>
          </div>
        </header>

        <CoursePage
          course={selectedCourse}
          onBack={() => setSelectedCourse(null)}
          onLessonClick={(lessonId) => setSelectedLessonId(lessonId)}
        />
      </div>
    );
  }

  return (
    <div className="app">
      <header className="header">
        <div className="container">
          <h1>Platforma kursów online</h1>
          <p>Pierwszy krok do lepszego ja</p>
        </div>
      </header>

      <main className="container">
        <section className="courses-section">
          <h2>Dostępne kursy</h2>

          {loading && (
            <p className="status">Ładowanie kursów...</p>
          )}

          {error && (
            <p className="status error">{error}</p>
          )}

          {!loading && !error && courses.length === 0 && (
            <p className="status">Brak dostępnych kursów.</p>
          )}

          {!loading && !error && courses.length > 0 && (
            <div className="courses-grid">
              {courses.map((course) => (
                <CourseCard
                  key={course.id}
                  course={course}
                  onClick={() => setSelectedCourse(course)}
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
