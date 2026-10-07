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
  onAccessChange: (access: DropboxTrialAccess | null) => void;
}

const priceFormatter = new Intl.NumberFormat("pl-PL", {
  style: "currency",
  currency: "PLN",
});

export function CoursePage({
  course,
  onBack,
  onLessonClick,
  onAccessChange,
}: CoursePageProps) {
  const [lessons, setLessons] = useState<Lesson[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [trialAccess, setTrialAccess] =
    useState<DropboxTrialAccess | null>(null);
  const [purchasePlans, setPurchasePlans] =
    useState<DropboxPurchasePlans | null>(null);
  const [purchasePlansError, setPurchasePlansError] =
    useState<string | null>(null);
  const [coupon, setCoupon] = useState("");
  const [couponError, setCouponError] = useState<string | null>(null);
  const [couponSectionOpen, setCouponSectionOpen] = useState(false);
  const [purchaseOptionsOpen, setPurchaseOptionsOpen] = useState(false);
  const [redeemingCoupon, setRedeemingCoupon] = useState(false);
  const [purchaseError, setPurchaseError] = useState<string | null>(null);
  const [processingPlan, setProcessingPlan] =
    useState<DropboxPurchasePlan | null>(null);
  const [purchaseConfirmed, setPurchaseConfirmed] = useState(false);

  useEffect(() => {
    onAccessChange(trialAccess);
  }, [onAccessChange, trialAccess]);

  useEffect(() => {
    let cancelled = false;

    async function loadLessons() {
      try {
        if (course.id < 0) {
          const [accessResult, plansResult] = await Promise.allSettled([
            getDropboxTrialAccess(course.id),
            getDropboxPurchasePlans(),
          ]);
          if (cancelled) return;
          if (plansResult.status === "fulfilled") {
            setPurchasePlans(plansResult.value);
            setPurchasePlansError(null);
          } else {
            setPurchasePlansError(
              plansResult.reason instanceof Error
                ? plansResult.reason.message
                : "Nie udało się pobrać dostępnych planów."
            );
          }
          if (accessResult.status === "rejected") {
            throw accessResult.reason;
          }

          const access = accessResult.value;
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
      course.id >= 0
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

  useEffect(() => {
    if (!purchaseConfirmed) {
      return;
    }

    let cancelled = false;
    const transitionTimer = window.setTimeout(() => {
      setLoading(true);
      void Promise.all([
        getDropboxTrialAccess(course.id),
        getLessons(course.id),
      ])
        .then(([access, courseLessons]) => {
          if (cancelled) return;
          setTrialAccess(access);
          setLessons(courseLessons);
          setError(null);
        })
        .catch((err: unknown) => {
          if (cancelled) return;
          setError(
            err instanceof Error
              ? err.message
              : "Nie udało się pobrać lekcji."
          );
        })
        .finally(() => {
          if (cancelled) return;
          setPurchaseConfirmed(false);
          setLoading(false);
        });
    }, 1800);

    return () => {
      cancelled = true;
      window.clearTimeout(transitionTimer);
    };
  }, [course.id, purchaseConfirmed]);

  async function handleRedeemCoupon(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();
    setCouponError(null);
    setRedeemingCoupon(true);

    try {
      const access = await redeemDropboxCoupon(course.id, coupon);
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
      const access = await startDropboxPurchase(course.id, type);
      setTrialAccess(access);
      setPurchaseConfirmed(true);
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
    course.id >= 0
    || (trialAccess?.hasAccess === true && !purchaseConfirmed);

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

      {course.id < 0 && loading && (
        <p className="status">Sprawdzanie dostępu do szkolenia...</p>
      )}

      {purchaseConfirmed && (
        <section className="lessons-section coupon-access-section">
          <p className="status">
            Zakup został zaakceptowany, a dostęp do szkolenia aktywowano.
            Tryb testowy: opłata nie została pobrana. Za chwilę otworzymy kurs.
          </p>
        </section>
      )}

      {course.id < 0
        && !loading
        && !purchaseConfirmed
        && (trialAccess || error) && (
        <section className="lessons-section coupon-access-section">
            {error && (
              <p className="status error">
                Nie udało się sprawdzić dostępu do szkolenia: {error}
              </p>
            )}
            {trialAccess && (
              <h2>
                {trialAccess.hasAccess
                  ? "Masz dostęp do szkolenia"
                  : trialAccess.hasRedeemedCoupon
                    ? "Dostęp wygasł"
                    : "Odblokuj szkolenie"}
              </h2>
            )}
            {trialAccess && (
              <div className="coupon-access">
                {!trialAccess.hasAccess && (
                  <>
                    <p className="status">
                      {trialAccess.hasRedeemedCoupon
                        ? "Wykorzystany kupon wygasł. Wprowadź nowy kupon lub kup dostęp."
                        : "Wprowadź otrzymany kupon albo kup dostęp do szkolenia."}
                    </p>
                    <CouponForm
                      coupon={coupon}
                      onCouponChange={setCoupon}
                      onSubmit={handleRedeemCoupon}
                      couponError={couponError}
                      redeemingCoupon={redeemingCoupon}
                    />
                  </>
                )}
                {trialAccess.hasAccess && trialAccess.expiresAt && (
                  <>
                    <button
                      type="button"
                      className="secondary-button"
                      aria-expanded={couponSectionOpen}
                      onClick={() =>
                        setCouponSectionOpen((isOpen) => !isOpen)
                      }
                    >
                      {couponSectionOpen
                        ? "Ukryj pole kuponu"
                        : "Masz dodatkowy kupon? Wpisz go"}
                    </button>
                    {couponSectionOpen && (
                      <CouponForm
                        coupon={coupon}
                        onCouponChange={setCoupon}
                        onSubmit={handleRedeemCoupon}
                        couponError={couponError}
                        redeemingCoupon={redeemingCoupon}
                      />
                    )}
                  </>
                )}
              </div>
            )}

            {purchasePlans
              && trialAccess
              && (
                purchasePlans.weekAvailable
                || purchasePlans.monthAvailable
                || purchasePlans.foreverAvailable
              )
              && (!trialAccess.hasAccess || purchaseOptionsOpen)
              && (
              <div className="purchase-options">
                <h3>Wybierz okres dostępu</h3>
                {trialAccess.hasAccess && (
                  <p className="status">
                    Możesz dokupić dostęp także podczas aktywnego okresu.
                    Nowy płatny okres zostanie doliczony po obecnym dostępie.
                  </p>
                )}
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
            {trialAccess
              && trialAccess.hasAccess
              && trialAccess.expiresAt
              && purchasePlans
              && (
                purchasePlans.weekAvailable
                || purchasePlans.monthAvailable
                || purchasePlans.foreverAvailable
              ) && (
                <button
                  type="button"
                  className="secondary-button"
                  aria-expanded={purchaseOptionsOpen}
                  onClick={() =>
                    setPurchaseOptionsOpen((isOpen) => !isOpen)
                  }
                >
                  {purchaseOptionsOpen
                    ? "Ukryj opcje przedłużenia"
                    : "Przedłuż lub dokup dostęp"}
                </button>
              )}
            {purchasePlansError && (
              <p className="status error">
                Nie udało się pobrać cen i planów zakupu: {purchasePlansError}
              </p>
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

interface CouponFormProps {
  coupon: string;
  onCouponChange: (coupon: string) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  couponError: string | null;
  redeemingCoupon: boolean;
}

function CouponForm({
  coupon,
  onCouponChange,
  onSubmit,
  couponError,
  redeemingCoupon,
}: CouponFormProps) {
  return (
    <>
      <form className="coupon-form" onSubmit={onSubmit}>
        <label htmlFor="dropbox-coupon">Kod kuponu</label>
        <input
          id="dropbox-coupon"
          type="text"
          value={coupon}
          onChange={(event) => onCouponChange(event.target.value)}
          autoComplete="off"
          required
        />
        <button
          type="submit"
          disabled={redeemingCoupon || !coupon.trim()}
        >
          {redeemingCoupon ? "Sprawdzanie kuponu..." : "Aktywuj kupon"}
        </button>
      </form>
      {couponError && <p className="status error">{couponError}</p>}
    </>
  );
}
