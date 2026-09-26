import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import rehypeSanitize from "rehype-sanitize";

import type { Lesson } from "../types/lesson";
import { getLesson, getLessons } from "../services/lessonService";
import {
  getCourseProgress,
  setLessonCompleted,
} from "../services/progressService";

interface LessonPageProps {
  courseId: number;
  lessonId: number;
  onBack: () => void;
  onLessonChange?: (lessonId: number) => void;
}

export function LessonPage({
  courseId,
  lessonId,
  onBack,
  onLessonChange,
}: LessonPageProps) {
  const [lesson, setLesson] = useState<Lesson | null>(null);
  const [lessons, setLessons] = useState<Lesson[]>([]);
  const [completedLessons, setCompletedLessons] = useState<number[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadData() {
      try {
        setLoading(true);
        setError(null);

        // Lekcja i lista lekcji są niezależne od logowania.
        const [lessonData, lessonsData] = await Promise.all([
          getLesson(courseId, lessonId),
          getLessons(courseId),
        ]);

        setLesson(lessonData);
        setLessons(lessonsData);

        // Progress wymaga JWT, więc jego błąd nie może zablokować lekcji.
        try {
          const progressData = await getCourseProgress(courseId);

          setCompletedLessons(
            progressData
              .filter((item) => item.isCompleted)
              .map((item) => item.lessonId)
          );
        } catch (progressError) {
          console.warn(
            "Nie udało się pobrać postępu użytkownika:",
            progressError
          );

          setCompletedLessons([]);
        }
      } catch (err) {
        console.error(err);
        setError("Nie udało się pobrać lekcji.");
      } finally {
        setLoading(false);
      }
    }

    loadData();
  }, [courseId, lessonId]);

  async function markCompleted() {
    if (completedLessons.includes(lessonId)) {
      return;
    }

    try {
      await setLessonCompleted(courseId, lessonId, true);

      setCompletedLessons((current) => [
        ...current,
        lessonId,
      ]);
    } catch (err) {
      console.error(err);
      setError(
        "Nie udało się zapisać ukończenia lekcji."
      );
    }
  }

  async function undoCompleted() {
    try {
      await setLessonCompleted(courseId, lessonId, false);

      setCompletedLessons((current) =>
        current.filter((id) => id !== lessonId)
      );
    } catch (err) {
      console.error(err);
      setError(
        "Nie udało się cofnąć ukończenia lekcji."
      );
    }
  }

  function goToLesson(id: number) {
    onLessonChange?.(id);
  }

  if (loading) {
    return (
      <main className="container">
        <p className="status">Ładowanie lekcji...</p>
      </main>
    );
  }

  if (error || !lesson) {
    return (
      <main className="container">
        <button
          type="button"
          className="back-button"
          onClick={onBack}
        >
          ← Powrót do kursu
        </button>

        <p className="status error">
          {error ?? "Nie znaleziono lekcji."}
        </p>
      </main>
    );
  }

  const currentIndex = lessons.findIndex(
    (item) => item.id === lessonId
  );

  const previousLesson =
    currentIndex > 0
      ? lessons[currentIndex - 1]
      : null;

  const nextLesson =
    currentIndex >= 0 &&
    currentIndex < lessons.length - 1
      ? lessons[currentIndex + 1]
      : null;

  const totalLessons = lessons.length;

  const completedCount = completedLessons.filter((id) =>
    lessons.some((item) => item.id === id)
  ).length;

  const progress =
    totalLessons > 0
      ? Math.round((completedCount / totalLessons) * 100)
      : 0;

  const isCompleted = completedLessons.includes(lessonId);

  const isCourseCompleted =
    totalLessons > 0 &&
    completedCount === totalLessons;

  return (
    <main className="container lesson-page">
      <button
        type="button"
        className="back-button"
        onClick={onBack}
      >
        ← Powrót do kursu
      </button>

      <div className="lesson-header">
        <span className="lesson-number-large">
          LEKCJA {String(lesson.order).padStart(2, "0")}
        </span>

        <br />

        <h1 className="lesson-title">
          {lesson.title}
        </h1>

        {lesson.description && (
          <p className="lesson-description">
            {lesson.description}
          </p>
        )}
      </div>

      <section className="course-progress">
        <div className="course-progress-header">
          <div>
            <span className="course-progress-label">
              POSTĘP KURSU
            </span>

            <span className="course-progress-count">
              {completedCount} / {totalLessons} lekcji
            </span>
          </div>

          <strong>{progress}%</strong>
        </div>

        <div className="course-progress-track">
          <div
            className="course-progress-fill"
            style={{ width: `${progress}%` }}
          />
        </div>
      </section>

      <div className="lesson-content-wrapper">
        <article className="lesson-body">
          {lesson.content ? (
            <ReactMarkdown rehypePlugins={[rehypeSanitize]}>
              {lesson.content}
            </ReactMarkdown>
          ) : (
            <p>Ta lekcja nie ma jeszcze treści.</p>
          )}
        </article>
      </div>

      <div className="lesson-complete">
        {!isCompleted ? (
          <button
            type="button"
            className="complete-button"
            onClick={markCompleted}
          >
            ✓ Oznacz jako ukończoną
          </button>
        ) : (
          <div className="completed-actions">
            <div className="lesson-completed">
              ✓ Lekcja ukończona
            </div>

            <button
              type="button"
              className="undo-complete-button"
              onClick={undoCompleted}
            >
              ↶ Cofnij ukończenie
            </button>
          </div>
        )}
      </div>

      {isCourseCompleted && (
        <div className="course-finished">
          🎉 Kurs ukończony!
        </div>
      )}

      <div className="lesson-navigation">
        {previousLesson ? (
          <button
            type="button"
            className="lesson-nav-button"
            onClick={() => goToLesson(previousLesson.id)}
          >
            <span>← Poprzednia lekcja</span>

            <strong>
              {previousLesson.title}
            </strong>
          </button>
        ) : (
          <div />
        )}

        {nextLesson ? (
          <button
            type="button"
            className="lesson-nav-button next"
            onClick={() => goToLesson(nextLesson.id)}
          >
            <span>Następna lekcja →</span>

            <strong>
              {nextLesson.title}
            </strong>
          </button>
        ) : (
          <div />
        )}
      </div>
    </main>
  );
}
