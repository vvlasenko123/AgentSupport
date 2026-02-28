import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";

const normalizeBaseUrl = (url: string) => (url.endsWith("/") ? url : `${url}/`);

const DEFAULT_BASE_URL = import.meta.env.DEV
  ? "http://localhost:8000/api/v1/"
  : "/api/v1/";

const API_BASE_URL = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL || DEFAULT_BASE_URL);

type RetryRequestConfig = InternalAxiosRequestConfig & { _retry?: boolean };

export const axiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: {
    "Content-Type": "application/json",
  },
});

// Keep default export compatibility for legacy imports.
export default axiosInstance;

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: unknown) => void;
}> = [];

const processQueue = (error: unknown, token?: string) => {
  failedQueue.forEach((prom) => {
    if (error) prom.reject(error);
    else if (token) prom.resolve(token);
  });
  failedQueue = [];
};

const clearAuth = () => {
  localStorage.removeItem("access_token");
  localStorage.removeItem("refresh_token");
  localStorage.removeItem("user_role");
  localStorage.removeItem("user_id");
  localStorage.removeItem("user_name");
  window.dispatchEvent(new Event("agent-auth-changed"));
};

axiosInstance.interceptors.request.use((config) => {
  const token = localStorage.getItem("access_token");
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

axiosInstance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const status = error.response?.status;
    const originalRequest = (error.config || {}) as RetryRequestConfig;

    if (status !== 401) return Promise.reject(error);

    const refreshToken = localStorage.getItem("refresh_token");
    const isRefreshCall =
      typeof originalRequest.url === "string" && originalRequest.url.includes("auth/refresh/");

    if (!refreshToken || isRefreshCall || originalRequest._retry) {
      clearAuth();
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        failedQueue.push({
          resolve: (newToken: string) => {
            originalRequest.headers = originalRequest.headers ?? {};
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
            resolve(axiosInstance(originalRequest));
          },
          reject,
        });
      });
    }

    isRefreshing = true;

    try {
      const refreshRes = await axios.post(`${API_BASE_URL}auth/refresh/`, { refresh: refreshToken });
      const newAccess = (refreshRes.data as { access?: string })?.access;

      if (!newAccess) {
        throw new Error("Missing access token in refresh response");
      }

      localStorage.setItem("access_token", newAccess);
      axiosInstance.defaults.headers.common.Authorization = `Bearer ${newAccess}`;
      processQueue(null, newAccess);

      originalRequest.headers = originalRequest.headers ?? {};
      originalRequest.headers.Authorization = `Bearer ${newAccess}`;

      return axiosInstance(originalRequest);
    } catch (refreshError) {
      processQueue(refreshError);
      clearAuth();
      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  },
);
