const MOODS = {
  Happy: "#f2b233", Excited: "#ef5d46", Peaceful: "#3fb8a6", Nostalgic: "#a0709b",
  Grateful: "#6aa84f", Sad: "#5a7fd1", Proud: "#4b3fd1"
};
const MONTHS = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];

const $ = (s) => document.querySelector(s);
const timeline = $("#timeline"), dialog = $("#dialog"), form = $("#form");
let editingId = null, uploadedUrl = null, searchTimer;


/* ---------- helpers ---------- */
const esc = (t) => String(t ?? "").replace(/[&<>"']/g, c => ({ "&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;" }[c]));

function toast(msg) {
  const t = $("#toast");
  t.textContent = msg; t.classList.add("show");
  setTimeout(() => t.classList.remove("show"), 2200);
}

async function api(url, opts = {}) {
  const res = await fetch(url, opts);
  if (res.status === 401) { location.href = "login.html"; throw new Error("Please log in."); }
  if (res.status === 204) return null;
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(data.error || "Something went wrong. Try again.");
  return data;
}

/* ---------- load & render ---------- */
async function load() {
  const p = new URLSearchParams();
  if ($("#search").value.trim()) p.set("search", $("#search").value.trim());
  if ($("#moodFilter").value) p.set("mood", $("#moodFilter").value);
  if ($("#favOnly").checked) p.set("favorite", "true");

  try {
    const [items] = await Promise.all([api("api/memories?" + p), loadStats()]);
    render(items);
  } catch (e) {
    timeline.innerHTML = `<div class="empty"><strong>Couldn't load memories</strong>${esc(e.message)}</div>`;
  }
}

async function loadStats() {
  const s = await api("api/stats");
  const since = s.since ? ` · since ${new Date(s.since).getFullYear()}` : "";
  $("#stats").textContent = `${s.total} ${s.total === 1 ? "memory" : "memories"} · ${s.favorites} favorite${s.favorites === 1 ? "" : "s"}${since}`;
}

function render(items) {
  if (!items.length) {
    const filtered = $("#search").value || $("#moodFilter").value || $("#favOnly").checked;
    timeline.innerHTML = `<div class="empty"><strong>${filtered ? "No memories match" : "Your story starts here"}</strong>${filtered ? "Try a different search or filter." : "Choose “Write a memory” to add the first one."}</div>`;
    return;
  }
  let html = "", lastYear = null;
  for (const m of items) {
    const d = new Date(m.memoryDate);
    const y = d.getFullYear();
    if (y !== lastYear) { html += `<h2 class="year">${y}</h2>`; lastYear = y; }
    const color = MOODS[m.mood] || "#4b3fd1";
    html += `
      <article class="entry">
        <div class="stamp"><span class="day">${d.getDate()}</span><span class="mon">${MONTHS[d.getMonth()]}</span></div>
        <div class="card" style="--dot:${color}">
          ${m.imageUrl ? `<img class="photo" src="${esc(m.imageUrl)}" alt="Photo for ${esc(m.title)}" loading="lazy">` : ""}
          <h3>${esc(m.title)}</h3>
          <p class="story">${esc(m.story)}</p>
          <div class="meta">
            <span class="tag mood">${esc(m.mood)}</span>
            ${m.category ? `<span class="tag">${esc(m.category)}</span>` : ""}
            <span class="tools">
              <button class="icon-btn fav ${m.isFavorite ? "on" : ""}" data-act="fav" data-id="${m.id}" aria-pressed="${m.isFavorite}">${m.isFavorite ? "Favorited" : "Favorite"}</button>
              <button class="icon-btn" data-act="edit" data-id="${m.id}">Edit</button>
              <button class="icon-btn del" data-act="del" data-id="${m.id}">Delete</button>
            </span>
          </div>
        </div>
      </article>`;
  }
  timeline.innerHTML = html;
}

/* ---------- card actions ---------- */
timeline.addEventListener("click", async (e) => {
  const btn = e.target.closest("[data-act]");
  if (!btn) return;
  const id = btn.dataset.id;
  try {
    if (btn.dataset.act === "fav") { await api(`api/memories/${id}/favorite`, { method: "PATCH" }); load(); }
    if (btn.dataset.act === "edit") openDialog(await api(`api/memories/${id}`));
    if (btn.dataset.act === "del" && confirm("Delete this memory? This can't be undone.")) {
      await api(`api/memories/${id}`, { method: "DELETE" });
      toast("Memory deleted"); load();
    }
  } catch (err) { toast(err.message); }
});

/* ---------- dialog / form ---------- */
function openDialog(m = null) {
  editingId = m?.id ?? null;
  uploadedUrl = m?.imageUrl ?? null;
  form.reset();
  $("#formError").textContent = "";
  $("#dialogTitle").textContent = m ? "Edit memory" : "Write a memory";
  $("#saveBtn").textContent = "Save memory";
  form.title.value = m?.title ?? "";
  form.story.value = m?.story ?? "";
  form.memoryDate.value = (m?.memoryDate ?? new Date().toISOString()).slice(0, 10);
  form.mood.value = m?.mood ?? "Happy";
  form.category.value = m?.category ?? "";
  const prev = $("#preview");
  prev.hidden = !uploadedUrl; if (uploadedUrl) prev.src = uploadedUrl;
  dialog.showModal();
  form.title.focus();
}

$("#photo").addEventListener("change", async (e) => {
  const file = e.target.files[0];
  if (!file) return;
  const fd = new FormData(); fd.append("file", file);
  $("#formError").textContent = "";
  try {
    const r = await api("api/upload", { method: "POST", body: fd });
    uploadedUrl = r.url;
    $("#preview").src = r.url; $("#preview").hidden = false;
  } catch (err) { $("#formError").textContent = err.message; e.target.value = ""; }
});

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  const body = {
    title: form.title.value, story: form.story.value, memoryDate: form.memoryDate.value,
    mood: form.mood.value, category: form.category.value, imageUrl: uploadedUrl
  };
  $("#saveBtn").disabled = true;
  try {
    await api(editingId ? `api/memories/${editingId}` : "api/memories", {
      method: editingId ? "PUT" : "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    dialog.close();
    toast(editingId ? "Changes saved" : "Memory saved");
    load();
  } catch (err) { $("#formError").textContent = err.message; }
  finally { $("#saveBtn").disabled = false; }
});

$("#cancelBtn").addEventListener("click", () => dialog.close());
$("#newBtn").addEventListener("click", () => openDialog());

/* ---------- filters ---------- */
for (const mood of Object.keys(MOODS)) {
  $("#moodFilter").add(new Option(mood, mood));
  $("#moodSelect").add(new Option(mood, mood));
}
$("#search").addEventListener("input", () => { clearTimeout(searchTimer); searchTimer = setTimeout(load, 250); });
$("#moodFilter").addEventListener("change", load);
$("#favOnly").addEventListener("change", load);

/* ---------- user / logout ---------- */
$("#logoutBtn").addEventListener("click", async () => {
  await fetch("api/auth/logout", { method: "POST" });
  location.href = "login.html";
});

async function init() {
  try {
    const me = await api("api/auth/me");
    $("#userName").textContent = me.name;
  } catch { return; }          // not logged in: api() already redirected
  load();
}
init();
