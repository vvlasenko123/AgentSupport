import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import Header from "./components/Header/Header";
import Footer from "./components/Footer/Footer";
import AppealsList from "./pages/AppealsList/AppealsList";
import AppealPage from "./pages/AppealPage/AppealPage";
import StatisticsPage from "./pages/StatisticsPage/StatisticsPage";
import NotFound from "./pages/NotFound/NotFound";
import "./App.scss";

function App() {
  return (
    <BrowserRouter basename="/">
      <div className="app-shell">
        <Header />
        <main className="app-content">
          <Routes>
            <Route path="/" element={<Navigate to="/appeals" replace />} />
            <Route path="/appeals" element={<AppealsList />} />
            <Route path="/appeals/:id" element={<AppealPage />} />
            <Route path="/statistics" element={<StatisticsPage />} />
            <Route path="*" element={<NotFound />} />
          </Routes>
        </main>
        <Footer />
      </div>
    </BrowserRouter>
  );
}

export default App;
