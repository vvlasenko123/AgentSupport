import axios from "axios";

export type LoginCredentials = {
  username: string;
  password: string;
};

type TokenResponse = {
  access_token: string;
  refresh_token?: string;
  token_type: string;
  expires_in: number;
};

const AUTH_TOKEN_URL =
  import.meta.env.VITE_AUTH_TOKEN_URL || "/realms/master/protocol/openid-connect/token";
const AUTH_CLIENT_ID = import.meta.env.VITE_AUTH_CLIENT_ID || "admin-cli";

export async function loginWithPassword(credentials: LoginCredentials) {
  const params = new URLSearchParams({
    grant_type: "password",
    client_id: AUTH_CLIENT_ID,
    username: credentials.username,
    password: credentials.password,
  });

  const { data } = await axios.post<TokenResponse>(AUTH_TOKEN_URL, params, {
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
    },
  });

  localStorage.setItem("access_token", data.access_token);
  if (data.refresh_token) {
    localStorage.setItem("refresh_token", data.refresh_token);
  }
  localStorage.setItem("user_name", credentials.username);

  window.dispatchEvent(new Event("agent-auth-changed"));
  return data;
}

export function logout() {
  localStorage.removeItem("access_token");
  localStorage.removeItem("refresh_token");
  localStorage.removeItem("user_name");
  window.dispatchEvent(new Event("agent-auth-changed"));
}

export function isAuthorized() {
  return !!localStorage.getItem("access_token");
}
