(() => {
  const panels = {
    hard: document.getElementById("panel-hard"),
    agents: document.getElementById("panel-agents"),
  };

  const graphHost = document.getElementById("start-graph");
  const fallback = document.getElementById("start-fallback");
  const home = document.querySelector("body.home");

  const HASH = {
    hard: "hard",
    agents: "agents",
  };

  function panelIdFromHash() {
    const raw = (location.hash || "").replace(/^#/, "");
    if (raw === "hard" || raw === "the-hard-way") return "hard";
    if (raw === "agents" || raw === "ai-agents" || raw === "agent-skill") return "agents";
    return null;
  }

  function showPanel(id, opts) {
    const setHash = !opts || opts.hash !== false;
    const scroll = !opts || opts.scroll !== false;
    const alreadyOpen = id && panels[id] && !panels[id].hidden;
    const next = opts && opts.toggle && alreadyOpen ? null : id;

    Object.entries(panels).forEach(([key, el]) => {
      if (!el) return;
      el.hidden = key !== next;
    });

    if (home) home.classList.toggle("home-open", Boolean(next));

    document.querySelectorAll("[data-start-node]").forEach((n) => {
      const on = n.getAttribute("data-start-node") === next;
      n.classList.toggle("is-selected", on);
      if (n.hasAttribute("aria-pressed")) n.setAttribute("aria-pressed", on ? "true" : "false");
    });

    document.querySelectorAll("[data-panel]").forEach((btn) => {
      const on = btn.getAttribute("data-panel") === next;
      btn.classList.toggle("is-selected", on);
      btn.setAttribute("aria-pressed", on ? "true" : "false");
    });

    if (setHash) {
      const hash = next ? HASH[next] : "";
      const url = hash ? `#${hash}` : location.pathname + location.search;
      history.replaceState(null, "", url);
    }

    if (scroll && next && panels[next]) {
      panels[next].scrollIntoView({ behavior: "smooth", block: "start" });
    }

    draw();
  }

  document.querySelectorAll("[data-panel]").forEach((btn) => {
    btn.addEventListener("click", () => showPanel(btn.getAttribute("data-panel"), { toggle: true }));
  });

  function layout(width) {
    const w = Math.max(280, Math.min(width, 640));
    const narrow = w < 500;
    if (narrow) {
      return {
        width: w,
        height: 300,
        nodes: [
          { id: "start", label: "Get Started ...", x: w / 2, y: 36, role: "root" },
          { id: "hard", label: 'the "hard" way', x: w / 2, y: 142, role: "leaf" },
          { id: "agents", label: "with your AI agents", x: w / 2, y: 248, role: "leaf" },
        ],
      };
    }
    return {
      width: w,
      height: 210,
      nodes: [
        { id: "start", label: "Get Started ...", x: w / 2, y: 42, role: "root" },
        { id: "hard", label: 'the "hard" way', x: w * 0.26, y: 162, role: "leaf" },
        { id: "agents", label: "with your AI agents", x: w * 0.74, y: 162, role: "leaf" },
      ],
    };
  }

  const links = [
    { source: "start", target: "hard" },
    { source: "start", target: "agents" },
  ];

  function currentSelection() {
    if (panels.hard && !panels.hard.hidden) return "hard";
    if (panels.agents && !panels.agents.hidden) return "agents";
    return null;
  }

  function draw() {
    if (!graphHost || typeof d3 === "undefined") return;

    const width =
      graphHost.clientWidth ||
      (graphHost.parentElement && graphHost.parentElement.clientWidth) ||
      560;
    const { width: w, height: h, nodes } = layout(width);
    const byId = Object.fromEntries(nodes.map((n) => [n.id, n]));
    const selected = currentSelection();

    graphHost.replaceChildren();
    const svg = d3
      .select(graphHost)
      .append("svg")
      .attr("class", "start-svg")
      .attr("viewBox", `0 0 ${w} ${h}`)
      .attr("width", "100%")
      .attr("role", "img")
      .attr("aria-label", "Get Started connected to the hard way and with your AI agents");

    svg
      .append("g")
      .attr("class", "start-links")
      .selectAll("line")
      .data(links)
      .join("line")
      .attr("x1", (d) => byId[d.source].x)
      .attr("y1", (d) => byId[d.source].y)
      .attr("x2", (d) => byId[d.target].x)
      .attr("y2", (d) => byId[d.target].y);

    const g = svg
      .append("g")
      .selectAll("g")
      .data(nodes)
      .join("g")
      .attr("class", (d) => {
        const bits = ["start-node", `start-node--${d.role}`];
        if (d.id === selected) bits.push("is-selected");
        return bits.join(" ");
      })
      .attr("data-start-node", (d) => d.id)
      .attr("transform", (d) => `translate(${d.x},${d.y})`)
      .attr("tabindex", (d) => (d.role === "leaf" ? "0" : null))
      .attr("role", (d) => (d.role === "leaf" ? "button" : null))
      .attr("aria-pressed", (d) => (d.role === "leaf" ? (d.id === selected ? "true" : "false") : null))
      .attr("aria-label", (d) => d.label);

    const labels = g
      .append("text")
      .attr("text-anchor", "middle")
      .attr("dominant-baseline", "middle")
      .text((d) => d.label);

    g.each(function (d) {
      const text = d3.select(this).select("text").node();
      const box = text.getBBox();
      const padX = d.role === "root" ? 18 : 14;
      const padY = d.role === "root" ? 12 : 10;
      d3.select(this)
        .insert("rect", "text")
        .attr("x", box.x - padX)
        .attr("y", box.y - padY)
        .attr("width", box.width + padX * 2)
        .attr("height", box.height + padY * 2)
        .attr("rx", d.role === "root" ? 18 : 14);
    });

    g.filter((d) => d.role === "leaf")
      .style("cursor", "pointer")
      .on("click", (event, d) => {
        event.preventDefault();
        showPanel(d.id, { toggle: true });
      })
      .on("keydown", (event, d) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          showPanel(d.id, { toggle: true });
        }
      });

    g.filter((d) => d.role === "root").on("click", (event) => {
      event.preventDefault();
      showPanel(null, { toggle: false });
    });

    void labels;
  }

  if (typeof d3 !== "undefined" && graphHost) {
    if (fallback) fallback.hidden = true;
    draw();
    window.addEventListener("resize", () => {
      window.clearTimeout(draw.tid);
      draw.tid = window.setTimeout(draw, 80);
    });
  }

  const initial = panelIdFromHash();
  if (initial) showPanel(initial, { hash: false, scroll: false, toggle: false });
})();
