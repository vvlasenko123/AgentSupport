import { axiosInstance } from "./axiosInstance";

export type TelegramAuthPayload = {
  id: number;
  first_name?: string;
  last_name?: string;
  username?: string;
  photo_url?: string;
  auth_date: number;
  hash: string;
};

type AuthUser = {
  id: string | number;
  role?: string;
  first_name?: string;
  username?: string;
};

type TgAuthResponse = {
  access?: string;
  refresh?: string;
  user?: AuthUser;
};

const TG_LOGIN_ENDPOINT = import.meta.env.VITE_TG_LOGIN_ENDPOINT || "auth/login/";

export async function loginWithTelegram(payload: TelegramAuthPayload) {
  const { data } = await axiosInstance.post<TgAuthResponse>(TG_LOGIN_ENDPOINT, payload);

  if (data.access) localStorage.setItem("access_token", data.access);
  if (data.refresh) localStorage.setItem("refresh_token", data.refresh);

  if (data.user) {
    localStorage.setItem("user_id", String(data.user.id));
    if (data.user.role) localStorage.setItem("user_role", data.user.role);

    const displayName =
      (data.user.first_name && data.user.first_name.trim()) || data.user.username || "Пользователь";

    localStorage.setItem("user_name", displayName);
  }

  window.dispatchEvent(new Event("agent-auth-changed"));
  return data;
}
