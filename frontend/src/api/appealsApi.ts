import { axiosInstance } from "./axiosInstance";
import type {
  AppealDetails,
  AppealListItem,
  AppealsResponse,
  DummyPost,
  DummyUser,
  EmotionalTone,
} from "../types/appeal";

const mapTone = (likes: number, dislikes: number): EmotionalTone => {
  if (likes > dislikes) {
    return "Позитив";
  }
  if (likes < dislikes) {
    return "Негатив";
  }
  return "Нейтрально";
};

const toListItem = (post: DummyPost): AppealListItem => ({
  id: post.id,
  title: post.title,
  date: formatDateByAppealId(post.id),
});

const formatDateByAppealId = (id: number): string => {
  const generatedDate = new Date(Date.now() - id * 86_400_000);
  return generatedDate.toLocaleDateString("ru-RU");
};

const buildSerialNumbers = (postId: number, userId: number): string => {
  const first = `SN-${String(10_000 + postId).padStart(6, "0")}`;
  const second = `MD-${String(20_000 + userId * 17 + postId).padStart(6, "0")}`;
  return `${first}, ${second}`;
};

const getDeviceType = (post: DummyPost): string => {
  if (post.tags.length === 0) {
    return "Универсальный измерительный прибор";
  }

  const normalizedTag = post.tags[0]
    .replace("-", " ")
    .replace(/\b\w/g, (symbol) => symbol.toUpperCase());

  return `Серия ${normalizedTag}`;
};

const mergeAppealDetails = (post: DummyPost, user: DummyUser): AppealDetails => ({
  id: post.id,
  date: formatDateByAppealId(post.id),
  fullName: `${user.lastName} ${user.firstName} ${user.maidenName}`,
  objectName: user.company.name,
  phone: user.phone,
  email: user.email,
  serialNumbers: buildSerialNumbers(post.id, post.userId),
  deviceType: getDeviceType(post),
  emotionalTone: mapTone(post.reactions.likes, post.reactions.dislikes),
  issueSummary: post.body,
});

export const appealsApi = {
  async getAppealsList(limit = 40): Promise<AppealListItem[]> {
    const { data } = await axiosInstance.get<AppealsResponse>(`/posts?limit=${limit}`);
    return data.posts.map(toListItem);
  },

  async getAppealById(id: number): Promise<AppealDetails> {
    const { data: post } = await axiosInstance.get<DummyPost>(`/posts/${id}`);
    const { data: user } = await axiosInstance.get<DummyUser>(`/users/${post.userId}`);
    return mergeAppealDetails(post, user);
  },
};
