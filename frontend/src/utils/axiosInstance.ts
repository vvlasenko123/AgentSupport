import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { refreshAccessToken } from "./auth";

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

export default axiosInstance;

let refreshPromise: Promise<string | null> | null = null;

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
    const originalRequest = (error.config || {}) as RetryRequestConfig;

    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    if (!refreshPromise) {
      refreshPromise = refreshAccessToken().finally(() => {
        refreshPromise = null;
      });
    }

    const newToken = await refreshPromise;

    if (!newToken) {
      return Promise.reject(error);
    }

    originalRequest.headers = originalRequest.headers ?? {};
    originalRequest.headers.Authorization = `Bearer ${newToken}`;

    return axiosInstance(originalRequest);
  },
);
