(() => {
  const home = document.querySelector("body.home");
  const links = Array.from(document.querySelectorAll("[data-way]"));
  const panels = {
    traditional: document.getElementById("traditional"),
    agentic: document.getElementById("agentic"),
  };

  function parseHash() {
    const raw = (location.hash || "").replace(/^#/, "");
    if (raw === "traditional" || raw === "traditional-setup" || raw === "hard" || raw === "the-hard-way") {
      return "traditional";
    }
    if (
      raw === "agentic" ||
      raw === "agentic-setup" ||
      raw === "agents" ||
      raw === "ai-agents" ||
      raw === "agent-skill"
    ) {
      return "agentic";
    }
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
