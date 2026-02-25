import axios from "axios";

export const axiosInstance = axios.create({
  baseURL: "https://dummyjson.com",
  timeout: 15000,
  headers: {
    "Content-Type": "application/json",
  },
});
