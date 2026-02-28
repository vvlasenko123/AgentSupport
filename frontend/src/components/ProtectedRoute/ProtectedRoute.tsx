import { type ReactNode, useEffect, useState } from "react";
import { Navigate } from "react-router-dom";
import { isTokenExpired, refreshAccessToken } from "../../utils/auth";

type ProtectedRouteProps = {
  children: ReactNode;
};

type GuardState = "checking" | "allow" | "deny";

const clearAuth = () => {
  localStorage.removeItem("access_token");
  localStorage.removeItem("refresh_token");
  localStorage.removeItem("user_name");
};

function ProtectedRoute({ children }: ProtectedRouteProps) {
  const [state, setState] = useState<GuardState>("checking");

  useEffect(() => {
    let isActive = true;

    const verify = async () => {
      const token = localStorage.getItem("access_token");
      if (!token) {
        if (isActive) setState("deny");
        return;
      }

      if (!isTokenExpired(token)) {
        if (isActive) setState("allow");
        return;
      }

      const refreshed = await refreshAccessToken();
      if (refreshed && !isTokenExpired(refreshed)) {
        if (isActive) setState("allow");
      } else {
        clearAuth();
        if (isActive) setState("deny");
      }
    };

    verify();

    return () => {
      isActive = false;
    };
  }, []);

  if (state === "checking") {
    return null;
  }

  if (state === "deny") {
    return <Navigate to="/unauthorized" replace />;
  }

  return children;
}

export default ProtectedRoute;
