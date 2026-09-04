/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./App.{js,jsx,ts,tsx}", "./src/**/*.{js,jsx,ts,tsx}"],
  presets: [require("nativewind/preset")],
  theme: {
    extend: {
      colors: {
        primary: "#005f37",
        "primary-container": "#0f7a4a",
        "on-primary-container": "#a6ffc6",
        background: "#f1fcf2",
        surface: "#f1fcf2",
        "surface-card": "#ffffff",
        "surface-base": "#F6F8F7",
        "surface-container-low": "#ebf7ed",
        "surface-container": "#e5f1e7",
        "surface-container-high": "#e0ebe1",
        "on-surface": "#141e18",
        "on-surface-variant": "#3f4941",
        "border-hairline": "#DCE5DF",
        "text-secondary": "#61706A",
        "status-success": "#15803D",
        "status-success-bg": "#DCFCE7",
        "status-warning": "#B7791F",
        "status-warning-bg": "#FEF3C7",
        "status-danger": "#DC2626",
        "status-danger-bg": "#FEE2E2",
        "status-info": "#2563EB",
        "status-info-bg": "#DBEAFE",
        "status-draft": "#64748B",
        "status-draft-bg": "#F1F5F9"
      }
    }
  },
  plugins: []
};
