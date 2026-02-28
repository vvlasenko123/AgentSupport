import { type ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { jwtDecode } from "jwt-decode";

type JwtPayload = {
  exp?: number;
};

type ProtectedRouteProps = {
  children: ReactNode;
};

const clearAuth = () => {
  localStorage.removeItem("access_token");
  localStorage.removeItem("refresh_token");
  localStorage.removeItem("user_name");
};

const isExpired = (exp?: number) => {
  if (!exp) return true;
  return Date.now() >= exp * 1000;
};

function ProtectedRoute({ children }: ProtectedRouteProps) {
  const token = localStorage.getItem("access_token");

  if (!token) {
    return <Navigate to="/unauthorized" replace />;
  }

  try {
    const payload = jwtDecode<JwtPayload>(token);

    if (isExpired(payload.exp)) {
      clearAuth();
      return <Navigate to="/unauthorized" replace />;
    }

    return children;
  } catch {
    clearAuth();
    return <Navigate to="/unauthorized" replace />;
  }
}

export default ProtectedRoute;
