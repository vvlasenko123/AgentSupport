export type EmotionalTone = "Негатив" | "Нейтрально" | "Позитив";
export type SortOrder = "default" | "asc" | "desc";

export interface AppealsResponse {
  posts: DummyPost[];
  total: number;
  skip: number;
  limit: number;
}

export interface DummyPost {
  id: number;
  title: string;
  body: string;
  tags: string[];
  reactions: {
    likes: number;
    dislikes: number;
  };
  views: number;
  userId: number;
}

export interface DummyUser {
  id: number;
  firstName: string;
  lastName: string;
  maidenName: string;
  email: string;
  phone: string;
  company: {
    title: string;
    name: string;
  };
}

export interface AppealListItem {
  id: number;
  title: string;
  date: string;
}

export interface AppealDetails {
  id: number;
  date: string;
  fullName: string;
  objectName: string;
  phone: string;
  email: string;
  serialNumbers: string;
  deviceType: string;
  emotionalTone: EmotionalTone;
  issueSummary: string;
}
