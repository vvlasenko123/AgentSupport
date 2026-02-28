import { axiosInstance } from "./axiosInstance";
import type { AppealDetails, AppealListItem, ComplaintDto } from "../types/appeal";

const formatIsoDate = (value: string): string => {
  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleDateString("ru-RU");
};

const toListItem = (item: ComplaintDto): AppealListItem => ({
  id: item.id,
  title: (item.issueSummary || item.objectName || "Обращение без названия").slice(0, 120),
  date: formatIsoDate(item.submissionDate),
});

const toDetails = (item: ComplaintDto): AppealDetails => ({
  id: item.id,
  date: formatIsoDate(item.submissionDate),
  fullName: item.fio,
  objectName: item.objectName,
  phone: item.phoneNumber,
  email: item.email,
  serialNumbers: item.serialNumbers.join(", "),
  deviceType: item.deviceType,
  emotionalTone: item.emotionalTone,
  issueSummary: item.issueSummary,
});

export const appealsApi = {
  async getAppealsList(): Promise<AppealListItem[]> {
    const { data } = await axiosInstance.get<ComplaintDto[]>("agent/complaints");
    return data.map(toListItem);
  },

  async getAppealById(id: string): Promise<AppealDetails> {
    const { data } = await axiosInstance.get<ComplaintDto>(`agent/complaints/${id}`);
    return toDetails(data);
  },
};
