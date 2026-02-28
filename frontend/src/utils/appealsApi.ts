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
  title: (item.issueSummary || item.objectName || "РћР±СЂР°С‰РµРЅРёРµ Р±РµР· РЅР°Р·РІР°РЅРёСЏ").slice(0, 120),
  date: formatIsoDate(item.submissionDate), 
  status: (item.status || "РќРµРёР·РІРµСЃС‚РЅРѕ"),
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
  async getComplaintsRaw(): Promise<ComplaintDto[]> {
    const { data } = await axiosInstance.get<ComplaintDto[]>("agent/complaints");
    return data;
  },

  async getAppealsList(): Promise<AppealListItem[]> {
    const data = await this.getComplaintsRaw();
    return data.map(toListItem);
  },

  async getAppealById(id: string): Promise<AppealDetails> {
    const { data } = await axiosInstance.get<ComplaintDto>(`agent/complaints/${id}`);
    return toDetails(data);
  },
};
