import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import rehypeSanitize from "rehype-sanitize";

import type { Lesson } from "../types/lesson";
import {
  getDropboxMaterialLink,
  getLesson,
  getLessons,
} from "../services/lessonService";
import {
  getCourseProgress,
  setLessonCompleted,
} from "../services/progressService";
import { getDropboxTrialAccess } from "../services/dropboxAccessService";

interface LessonPageProps {
  courseId: number;
  lessonId: number;
  onBack: () => void;
  onLessonChange?: (lessonId: number) => void;
}

function getMaterialHeading(name: string): string {
  const withoutExtension = name.replace(/\.[^.]+$/, "");
  const heading = withoutExtension
    .replace(/[_\-.]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();

  return heading.length > 0
    ? heading[0].toLocaleUpperCase("pl-PL") + heading.slice(1)
    : name;
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
  const [materialLinks, setMaterialLinks] = useState<
    Record<number, string>
  >({});
  const [failedMaterialIds, setFailedMaterialIds] = useState<number[]>([]);
  const [trialExpired, setTrialExpired] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (courseId !== -1) {
      return;
    }

    let cancelled = false;
    let expiryTimer: number | undefined;

    async function monitorTrialAccess() {
      try {
        const access = await getDropboxTrialAccess();
        if (cancelled) return;

        if (!access.hasAccess) {
          setTrialExpired(true);
          return;
        }

        if (!access.expiresAt) {
          return;
        }

        const timeRemaining = Date.parse(access.expiresAt) - Date.now();
        if (timeRemaining <= 0) {
          setTrialExpired(true);
          return;
        }

        expiryTimer = window.setTimeout(
          () => setTrialExpired(true),
          timeRemaining
        );
      } catch (accessError) {
        console.error(
          "Nie udało się sprawdzić dostępu próbnego:",
          accessError
        );
      }
    }

    void monitorTrialAccess();
    return () => {
      cancelled = true;
      if (expiryTimer !== undefined) {
        window.clearTimeout(expiryTimer);
      }
    };
  }, [courseId]);

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
        setError(
          err instanceof Error
            ? err.message
            : "Nie udało się pobrać lekcji."
        );
      } finally {
        setLoading(false);
      }
    }

    loadData();
  }, [courseId, lessonId]);

  useEffect(() => {
    let cancelled = false;

    async function loadMaterialLinks() {
      if (courseId !== -1 || !lesson?.materials?.length) {
        setMaterialLinks({});
        setFailedMaterialIds([]);
        return;
      }

      setMaterialLinks({});
      setFailedMaterialIds([]);
      const links = await Promise.all(
        lesson.materials.map(async (material) => {
          try {
            return {
              id: material.id,
              url: await getDropboxMaterialLink(material.url),
            };
          } catch (linkError) {
            console.error(
              `Nie udało się pobrać materiału ${material.name}:`,
              linkError
            );
            return { id: material.id, url: null };
          }
        })
      );

      if (!cancelled) {
        setFailedMaterialIds(
          links.filter((item) => item.url === null).map((item) => item.id)
        );
        setMaterialLinks(
          Object.fromEntries(
            links
              .filter(
                (item): item is { id: number; url: string } =>
                  item.url !== null
              )
              .map((item) => [item.id, item.url])
          )
        );
      }
    }

    void loadMaterialLinks();
    return () => {
      cancelled = true;
    };
  }, [courseId, lesson]);

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

  if (trialExpired) {
    return (
      <main className="container">
        <button
          type="button"
          className="back-button"
          onClick={onBack}
        >
          ← Powrót do kursu
        </button>
        <h1>Dostęp próbny wygasł</h1>
        <p className="status">
          24-godzinny okres próbny dobiegł końca. Aby kontynuować
          szkolenie, należy kupić dostęp.
        </p>
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
            <p>Nie dodano jeszcze treści do tej lekcji.</p>
          )}
          {lesson.materials?.map((material) => {
            const link = materialLinks[material.id];
            if (!link) {
              if (failedMaterialIds.includes(material.id)) {
                return (
                  <p className="status error" key={material.id}>
                    {material.kind === "file"
                      ? `Nie udało się załadować materiału: ${material.name}.`
                      : "Nie udało się załadować materiału."}
                  </p>
                );
              }

              return (
                <p className="status" key={material.id}>
                  {material.kind === "file"
                    ? `Ładowanie materiału: ${material.name}...`
                    : "Ładowanie materiału..."}
                </p>
              );
            }

            if (material.kind === "image") {
              return (
                <figure className="lesson-material" key={material.id}>
                  <h3>{getMaterialHeading(material.name)}</h3>
                  <img src={link} alt={lesson.title} loading="lazy" />
                </figure>
              );
            }

            if (material.kind === "video") {
              return (
                <figure className="lesson-material" key={material.id}>
                  <h3>{getMaterialHeading(material.name)}</h3>
                  <video
                    controls
                    preload="metadata"
                    aria-label={lesson.title}
                  >
                    <source src={link} type={material.contentType} />
                    Twoja przeglądarka nie obsługuje odtwarzania wideo.
                  </video>
                </figure>
              );
            }

            return (
              <p className="lesson-material-link" key={material.id}>
                <a href={link} target="_blank" rel="noreferrer">
                  Otwórz plik: {material.name}
                </a>
              </p>
            );
          })}
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
