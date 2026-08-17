(() => {
  const home = document.querySelector("body.home");
  const links = Array.from(document.querySelectorAll("[data-way]"));
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

  function show(id) {
    const next = id || "";

    Object.entries(panels).forEach(([key, el]) => {
      if (!el) return;
      el.classList.toggle("is-open", key === next);
    });

    links.forEach((a) => {
      const on = a.getAttribute("data-way") === next;
      a.classList.toggle("is-current", on);
      if (on) a.setAttribute("aria-current", "page");
      else a.removeAttribute("aria-current");
    });

    if (home) home.classList.toggle("home-open", Boolean(next));
  }

  links.forEach((a) => {
    a.addEventListener("click", () => {
      const way = a.getAttribute("data-way") || "";
      // Let the hash update, then reveal. :target also covers no-JS.
      window.setTimeout(() => {
        show(way);
        const panel = panels[way];
        if (panel) panel.scrollIntoView({ behavior: "smooth", block: "start" });
      }, 0);
    });
  });

  show(parseHash());
  window.addEventListener("hashchange", () => show(parseHash()));
})();
