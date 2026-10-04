import { useEffect, useState, type FormEvent } from "react";

import type { Course } from "../types/course";
import type { Lesson } from "../types/lesson";
import {
  getDropboxTrialAccess,
  redeemDropboxCoupon,
  type DropboxTrialAccess,
} from "../services/dropboxAccessService";
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
  const [trialAccess, setTrialAccess] =
    useState<DropboxTrialAccess | null>(null);
  const [coupon, setCoupon] = useState("");
  const [couponError, setCouponError] = useState<string | null>(null);
  const [redeemingCoupon, setRedeemingCoupon] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadLessons() {
      try {
        if (course.id === -1) {
          const access = await getDropboxTrialAccess();
          if (cancelled) return;
          setTrialAccess(access);
          if (!access.hasAccess) return;
        }

        const data = await getLessons(course.id);
        if (cancelled) return;
        setLessons(data);
      } catch (err) {
        if (cancelled) return;
        console.error(err);
        setError(
          err instanceof Error
            ? err.message
            : "Nie udało się pobrać lekcji."
        );
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadLessons();

    return () => {
      cancelled = true;
    };
  }, [course.id]);

  useEffect(() => {
    if (
      course.id !== -1
      || !trialAccess?.hasAccess
      || !trialAccess.expiresAt
    ) {
      return;
    }

    const timeRemaining = Date.parse(trialAccess.expiresAt) - Date.now();
    const expiryTimer = window.setTimeout(() => {
      setTrialAccess((current) =>
        current
          ? { ...current, hasAccess: false }
          : current
      );
    }, Math.max(0, timeRemaining));

    return () => window.clearTimeout(expiryTimer);
  }, [course.id, trialAccess]);

  async function handleRedeemCoupon(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();
    setCouponError(null);
    setRedeemingCoupon(true);

    try {
      const access = await redeemDropboxCoupon(coupon);
      setTrialAccess(access);
      setCoupon("");
      setLoading(true);
      try {
        setLessons(await getLessons(course.id));
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Nie udało się pobrać lekcji."
        );
      }
    } catch (err) {
      setCouponError(
        err instanceof Error
          ? err.message
          : "Nie udało się aktywować kuponu."
      );
    } finally {
      setLoading(false);
      setRedeemingCoupon(false);
    }
  }

  const canListLessons =
    course.id !== -1 || trialAccess?.hasAccess === true;

  return (
    <main className="container course-page">
      <button
        type="button"
        className="back-button"
        onClick={onBack}
      >
        ← Powrót do kursów
      </button>
      <br />
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

      {course.id === -1 && loading && (
        <p className="status">Sprawdzanie dostępu do szkolenia...</p>
      )}

      {course.id === -1 &&
        !loading &&
        !error &&
        trialAccess &&
        !trialAccess.hasAccess && (
          <section className="lessons-section">
            {!trialAccess.hasRedeemedCoupon ? (
              <>
                <h2>Odblokuj szkolenie</h2>
                <p className="status">
                  Wprowadź kupon, aby uzyskać 24-godzinny dostęp próbny.
                  Kupon można aktywować tylko raz na konto.
                </p>
                <form
                  className="coupon-form"
                  onSubmit={handleRedeemCoupon}
                >
                  <label htmlFor="dropbox-coupon">
                    Kod kuponu
                  </label>
                  <input
                    id="dropbox-coupon"
                    type="text"
                    value={coupon}
                    onChange={(event) => setCoupon(event.target.value)}
                    autoComplete="off"
                    required
                  />
                  <button
                    type="submit"
                    disabled={redeemingCoupon || !coupon.trim()}
                  >
                    {redeemingCoupon
                      ? "Sprawdzanie kuponu..."
                      : "Aktywuj kupon"}
                  </button>
                </form>
                {couponError && (
                  <p className="status error">{couponError}</p>
                )}
              </>
            ) : (
              <>
                <h2>Dostęp próbny wygasł</h2>
                <p className="status">
                  24-godzinny okres próbny dobiegł końca. Aby kontynuować
                  szkolenie, należy kupić dostęp.
                </p>
              </>
            )}
          </section>
        )}

      {course.id === -1 &&
        trialAccess?.hasAccess &&
        trialAccess.expiresAt && (
          <p className="status">
            Dostęp próbny jest aktywny do{" "}
            {new Date(trialAccess.expiresAt).toLocaleString("pl-PL")}.
          </p>
        )}

      {canListLessons && (
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
      )}
    </main>
  );
}
