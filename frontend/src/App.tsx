import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import Header from "./components/Header/Header";
import Footer from "./components/Footer/Footer";

import ProtectedRoute from "./components/ProtectedRoute/ProtectedRoute";

import NotFound from "./pages/NotFound/NotFound";
import Unauthorized from "./pages/Unauthorized/Unauthorized";

import HomePage from "./pages/HomePage/HomePage";

import AppealsList from "./pages/AppealsList/AppealsList";
import AppealPage from "./pages/AppealPage/AppealPage";
import StatisticsPage from "./pages/StatisticsPage/StatisticsPage";

import "./App.scss";

function App() {
  return (
    <BrowserRouter basename="/">
      <div className="app-shell">
        <Header />
        <main className="app-content">
          <Routes>
            <Route path="/" element={<Navigate to="/home" replace />} />
            <Route path="*" element={<NotFound />} />

            <Route path="/unauthorized" element={<Unauthorized />} />

            <Route path="/home" element={<HomePage />} />

            <Route
              path="/appeals"
              element={
                <ProtectedRoute>
                  <AppealsList />
                </ProtectedRoute>
              }
            />
            <Route
              path="/appeals/:id"
              element={
                <ProtectedRoute>
                  <AppealPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/statistics"
              element={
                <ProtectedRoute>
                  <StatisticsPage />
                </ProtectedRoute>
              }
            />
          </Routes>
        </main>
        <Footer />
      </div>
    </BrowserRouter>
  );
}

export default App;
