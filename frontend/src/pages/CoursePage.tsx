import { useEffect, useState, type FormEvent } from "react";

import type { Course } from "../types/course";
import type { Lesson } from "../types/lesson";
import {
  getDropboxPurchasePlans,
  getDropboxTrialAccess,
  redeemDropboxCoupon,
  startDropboxPurchase,
  type DropboxTrialAccess,
  type DropboxPurchasePlan,
  type DropboxPurchasePlans,
} from "../services/dropboxAccessService";
import { getLessons } from "../services/lessonService";

interface CoursePageProps {
  course: Course;
  onBack: () => void;
  onLessonClick: (lessonId: number) => void;
}

const priceFormatter = new Intl.NumberFormat("pl-PL", {
  style: "currency",
  currency: "PLN",
});

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
  const [purchasePlans, setPurchasePlans] =
    useState<DropboxPurchasePlans | null>(null);
  const [coupon, setCoupon] = useState("");
  const [couponError, setCouponError] = useState<string | null>(null);
  const [redeemingCoupon, setRedeemingCoupon] = useState(false);
  const [purchaseError, setPurchaseError] = useState<string | null>(null);
  const [processingPlan, setProcessingPlan] =
    useState<DropboxPurchasePlan | null>(null);
  const [purchaseConfirmed, setPurchaseConfirmed] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadLessons() {
      try {
        if (course.id === -1) {
          const [access, plans] = await Promise.all([
            getDropboxTrialAccess(),
            getDropboxPurchasePlans(),
          ]);
          if (cancelled) return;
          setTrialAccess(access);
          setPurchasePlans(plans);
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

  async function handlePurchase(type: DropboxPurchasePlan) {
    setPurchaseError(null);
    setProcessingPlan(type);

    try {
      const access = await startDropboxPurchase(type);
      setTrialAccess(access);
      setPurchaseConfirmed(true);
      setLoading(true);
      try {
        setLessons(await getLessons(course.id));
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Nie udało się pobrać lekcji."
        );
      } finally {
        setLoading(false);
      }
    } catch (err) {
      setPurchaseError(
        err instanceof Error
          ? err.message
          : "Nie udało się aktywować dostępu."
      );
    } finally {
      setProcessingPlan(null);
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

      {course.id === -1 && !loading && !error && trialAccess && (
        <section className="lessons-section coupon-access-section">
            <h2>
              {trialAccess.hasAccess
                ? "Masz dostęp do szkolenia"
                : trialAccess.hasRedeemedCoupon
                  ? "Dostęp wygasł"
                  : "Odblokuj szkolenie"}
            </h2>
            {!trialAccess.hasAccess && (
              <>
                <p className="status">
                  {trialAccess.hasRedeemedCoupon
                    ? "Wykorzystany kupon wygasł. Wprowadź nowy kupon lub kup dostęp."
                    : "Wprowadź otrzymany kupon albo kup dostęp do szkolenia."}
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
            )}

            {trialAccess.expiresAt && trialAccess.hasAccess && (
              <p className="status">
                Dostęp jest aktywny do{" "}
                {new Date(trialAccess.expiresAt).toLocaleString("pl-PL")}.
                Możesz przedłużyć go kolejnym zakupem.
              </p>
            )}

            {purchaseConfirmed && (
              <p className="status">
                Testowa płatność została zaakceptowana, a kupon automatycznie
                aktywowany. Żadna opłata nie została pobrana.
              </p>
            )}

            {purchasePlans && (
              <div className="purchase-options">
                <h3>Wybierz okres dostępu</h3>
                <p className="status">
                  Tryb testowy: płatność jest akceptowana automatycznie i nie
                  pobieramy opłaty.
                </p>
                {purchasePlans?.weekAvailable && (
                  <button
                    type="button"
                    disabled={processingPlan !== null}
                    onClick={() => void handlePurchase("week")}
                  >
                    {processingPlan === "week"
                      ? "Aktywuję dostęp..."
                      : `Aktywuj dostęp na tydzień · ${priceFormatter.format(purchasePlans.weekPricePln)}`}
                  </button>
                )}
                {purchasePlans?.monthAvailable && (
                  <button
                    type="button"
                    disabled={processingPlan !== null}
                    onClick={() => void handlePurchase("month")}
                  >
                    {processingPlan === "month"
                      ? "Aktywuję dostęp..."
                      : `Aktywuj dostęp na miesiąc · ${priceFormatter.format(purchasePlans.monthPricePln)}`}
                  </button>
                )}
                {purchasePlans?.foreverAvailable && (
                  <button
                    type="button"
                    disabled={processingPlan !== null}
                    onClick={() => void handlePurchase("forever")}
                  >
                    {processingPlan === "forever"
                      ? "Aktywuję dostęp..."
                      : `Aktywuj dostęp bezterminowy · ${priceFormatter.format(purchasePlans.foreverPricePln)}`}
                  </button>
                )}
              </div>
            )}
            {purchaseError && (
              <p className="status error">{purchaseError}</p>
            )}
          </section>
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
