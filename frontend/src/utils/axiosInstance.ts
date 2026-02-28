import axios from "axios";

const normalizeBaseUrl = (url: string) => (url.endsWith("/") ? url : `${url}/`);

const DEFAULT_BASE_URL = import.meta.env.DEV
  ? "http://localhost:8000/api/v1/"
  : "/api/v1/";

const API_BASE_URL = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL || DEFAULT_BASE_URL);

export const axiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: {
    "Content-Type": "application/json",
  },
});

export default axiosInstance;

axiosInstance.interceptors.request.use((config) => {
  const token = localStorage.getItem("access_token");
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
