import { useEffect, useState } from "react";

import type { Course } from "../types/course";
import type { Lesson } from "../types/lesson";
import { getLessons } from "../services/lessonService";

interface CoursePageProps {
  course: Course;
  onBack: () => void;
  onLessonClick: (lessonId: number) => void;
}

export function CoursePage({
  course,
  onBack,
  onLessonClick,
}: CoursePageProps) {
  const [lessons, setLessons] = useState<Lesson[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadLessons() {
      try {
        const data = await getLessons(course.id);
        setLessons(data);
      } catch (err) {
        console.error(err);
        setError("Nie udało się pobrać lekcji.");
      } finally {
        setLoading(false);
      }
    }

    loadLessons();
  }, [course.id]);

  return (
    <main className="container course-page">
      <button
        type="button"
        className="back-button"
        onClick={onBack}
      >
        ← Powrót do kursów
      </button>

      {course.imageUrl && (
        <img
          src={course.imageUrl}
          alt={course.title}
          className="course-page-image"
        />
      )}

      <span className="course-page-category">
        {course.category}
      </span>

      <h1>{course.title}</h1>

      {course.description && (
        <p className="course-page-description">
          {course.description}
        </p>
      )}

      <section className="lessons-section">
        <h2>Lekcje kursu</h2>

        {loading && (
          <p className="status">
            Ładowanie lekcji...
          </p>
        )}

        {error && (
          <p className="status error">
            {error}
          </p>
        )}

        {!loading &&
          !error &&
          lessons.length === 0 && (
            <p className="status">
              Ten kurs nie ma jeszcze żadnych lekcji.
            </p>
          )}

        {!loading &&
          !error &&
          lessons.length > 0 && (
            <div className="lessons-list">
              {lessons.map((lesson) => (
                <article
                  key={lesson.id}
                  className="lesson-card"
                  onClick={() => onLessonClick(lesson.id)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(event) => {
                    if (
                      event.key === "Enter" ||
                      event.key === " "
                    ) {
                      onLessonClick(lesson.id);
                    }
                  }}
                >
                  <div className="lesson-number">
                    {String(lesson.order).padStart(2, "0")}
                  </div>

                  <div className="lesson-content">
                    <h3>{lesson.title}</h3>

                    {lesson.description && (
                      <p>{lesson.description}</p>
                    )}
                  </div>

                  <div className="lesson-arrow">
                    →
                  </div>
                </article>
              ))}
            </div>
          )}
      </section>
    </main>
  );
}
