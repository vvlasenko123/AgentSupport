import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/realms": {
        target: "http://keycloak:8080", // container_name: keycloak
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
