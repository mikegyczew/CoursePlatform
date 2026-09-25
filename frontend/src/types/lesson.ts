export interface Lesson {
  id: number;
  title: string;
  description: string | null;
  content: string | null;
  order: number;
  courseId: number;
}
