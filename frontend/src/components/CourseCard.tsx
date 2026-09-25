import type { Course } from "../types/course";

interface CourseCardProps {
  course: Course;
  onClick: () => void;
}

export function CourseCard({
  course,
  onClick,
}: CourseCardProps) {
  return (
    <article
      className="course-card"
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          onClick();
        }
      }}
    >
      {course.imageUrl && (
        <img
          src={course.imageUrl}
          alt={course.title}
          className="course-card-image"
        />
      )}

      <div className="course-card-content">
        <span className="course-card-category">
          {course.category}
        </span>

        <h2>{course.title}</h2>

        {course.description && (
          <p>{course.description}</p>
        )}
      </div>

      <div className="course-card-arrow">
        →
      </div>
    </article>
  );
}
