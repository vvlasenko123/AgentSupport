export type EmotionalTone = "Негатив" | "Нейтрально" | "Позитив";
export type SortOrder = "default" | "asc" | "desc";

export interface ComplaintDto {
  id: string;
  submissionDate: string;
  fio: string;
  objectName: string;
  phoneNumber: string;
  email: string;
  serialNumbers: string[];
  deviceType: string;
  emotionalTone: string;
  issueSummary: string;
}

export interface AppealListItem {
  id: string;
  title: string;
  date: string;
}

export interface AppealDetails {
  id: string;
  date: string;
  fullName: string;
  objectName: string;
  phone: string;
  email: string;
  serialNumbers: string;
  deviceType: string;
  emotionalTone: string;
  issueSummary: string;
}
