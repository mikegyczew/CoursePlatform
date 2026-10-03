export interface Lesson {
  id: number;
  title: string;
  description: string | null;
  content: string | null;
  order: number;
  courseId: number;
  materials?: LessonMaterial[];
}

export interface LessonMaterial {
  id: number;
  name: string;
  kind: "image" | "video" | "file";
  contentType: string;
  url: string;
}
