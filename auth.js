const form = document.querySelector("#authForm");
const errorBox = document.querySelector("#formError");
const submitBtn = document.querySelector("#submitBtn");
const mode = form.dataset.mode;               // "login" or "register"

// Already logged in? Go straight to the app.
fetch("api/auth/me").then(r => { if (r.ok) location.replace("index.html"); }).catch(() => {});

// Show / hide password
document.querySelector("#showPw").addEventListener("change", (e) => {
  form.querySelectorAll('input[type="password"], input[data-pw]').forEach(i => {
    i.type = e.target.checked ? "text" : "password";
    i.dataset.pw = "1";
  });
});

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  errorBox.textContent = "";

  const data = Object.fromEntries(new FormData(form));
  if (mode === "register" && data.password !== data.confirm) {
    errorBox.textContent = "Passwords do not match.";
    return;
  }

  const body = mode === "register"
    ? { name: data.name, email: data.email, password: data.password }
    : { email: data.email, password: data.password };

  submitBtn.disabled = true;
  const label = submitBtn.textContent;
  submitBtn.textContent = "Please wait…";

  try {
    const res = await fetch(`api/auth/${mode}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    const isJson = (res.headers.get("content-type") || "").includes("json");
    const json = isJson ? await res.json().catch(() => ({})) : {};
    if (!isJson) {
      throw new Error("This page is not running on the .NET server. Stop Live Server, run dotnet run, and open the URL shown in the terminal (like http://localhost:5000).");
    }
    if (!res.ok) throw new Error(json.error || "Something went wrong. Try again.");
    location.href = "index.html";
  } catch (err) {
    errorBox.textContent = err.message === "Failed to fetch"
      ? "Can't reach the server. Start the app with dotnet run and open its URL."
      : err.message;
    submitBtn.disabled = false;
    submitBtn.textContent = label;
  }
});
