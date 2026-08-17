(() => {
  const select = document.getElementById("start-choice");
  const home = document.querySelector("body.home");
  const panels = {
    hard: document.getElementById("hard"),
    agents: document.getElementById("agents"),
  };

  function parseHash() {
    const raw = (location.hash || "").replace(/^#/, "");
    if (raw === "hard" || raw === "the-hard-way") return "hard";
    if (raw === "agents" || raw === "ai-agents" || raw === "agent-skill") return "agents";
    return "";
  }

  function show(id, opts) {
    const setHash = !opts || opts.hash !== false;
    const next = id || "";

    Object.entries(panels).forEach(([key, el]) => {
      if (!el) return;
      const on = key === next;
      el.hidden = !on;
      el.classList.toggle("is-open", on);
    });

    if (home) home.classList.toggle("home-open", Boolean(next));
    if (select && select.value !== next) select.value = next;

    if (setHash) {
      const url = next ? `#${next}` : `${location.pathname}${location.search}`;
      history.replaceState(null, "", url);
    }
  }

  if (select) {
    select.addEventListener("change", () => show(select.value));
  }

  show(parseHash(), { hash: false });
  window.addEventListener("hashchange", () => show(parseHash(), { hash: false }));
})();
