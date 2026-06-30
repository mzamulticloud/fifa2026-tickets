import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import "./index.css";
import { msalInstance } from "@/lib/authV2";

// Story 2.3 / F3 — MSAL exige initialize() antes de qualquer uso (msal-browser v3+).
// Inicializa e processa um eventual redirect de login antes de montar a app.
// O fluxo v1 (bcrypt+JWT) não depende disto e continua funcionando normalmente.
msalInstance
  .initialize()
  .then(() => msalInstance.handleRedirectPromise())
  .catch((error) => {
    // Falha de init do MSAL não deve derrubar a app (v1 segue funcional).
    console.error("Falha ao inicializar MSAL (login v2):", error);
  })
  .finally(() => {
    createRoot(document.getElementById("root")!).render(<App />);
  });
