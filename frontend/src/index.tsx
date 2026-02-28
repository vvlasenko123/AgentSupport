import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.scss'
import App from './App.tsx'

const rootElement = document.getElementById("root") as HTMLElement;
const reactRoot = createRoot(rootElement);

reactRoot.render(
  <StrictMode>
    <App />
  </StrictMode>
);
