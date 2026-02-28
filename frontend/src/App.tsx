import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import Header from "./components/Header/Header";
import Footer from "./components/Footer/Footer";
import ProtectedRoute from "./components/ProtectedRoute/ProtectedRoute";
import Unauthorized from "./components/Unauthorized/Unauthorized";

import AppealPage from "./pages/AppealPage/AppealPage";
import AppealsList from "./pages/AppealsList/AppealsList";
import HomePage from "./pages/HomePage/HomePage";
import NotFound from "./pages/NotFound/NotFound";
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
            <Route path="/home" element={<HomePage />} />

            <Route path="/unauthorized" element={<Unauthorized />} />

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

            <Route path="*" element={<NotFound />} />
          </Routes>
        </main>
        <Footer />
      </div>
    </BrowserRouter>
  );
}

export default App;
