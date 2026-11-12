(() => {
  const config = window.ACEG || { serverAddress: "", links: {} };

  document.querySelectorAll("[data-server-address]").forEach((el) => {
    el.textContent = config.serverAddress;
  });

  document.querySelectorAll("[data-link]").forEach((el) => {
    const url = config.links[el.dataset.link];
    if (url && url !== "#") {
      el.href = url;
      el.target = "_blank";
      el.rel = "noopener";
    }
  });

  document.querySelectorAll("[data-copy]").forEach((button) => {
    const label = button.querySelector(".copy__label");
    button.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(config.serverAddress);
        label.textContent = "Copied";
      } catch {
        label.textContent = "Press Ctrl+C";
        const range = document.createRange();
        range.selectNodeContents(button.querySelector("code"));
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
      }
      button.classList.add("is-copied");
      setTimeout(() => {
        label.textContent = "Copy";
        button.classList.remove("is-copied");
      }, 1800);
    });
  });

  const nav = document.getElementById("nav");
  const burger = document.getElementById("burger");
  const setMenu = (open) => {
    nav.classList.toggle("is-open", open);
    burger.setAttribute("aria-expanded", String(open));
  };
  burger.addEventListener("click", () => setMenu(!nav.classList.contains("is-open")));
  document.querySelectorAll("#navLinks a").forEach((a) => a.addEventListener("click", () => setMenu(false)));

  const onScroll = () => nav.classList.toggle("is-scrolled", window.scrollY > 12);
  window.addEventListener("scroll", onScroll, { passive: true });
  onScroll();

  const revealed = document.querySelectorAll(".reveal");
  if ("IntersectionObserver" in window) {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("is-visible");
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.12 }
    );
    revealed.forEach((el) => observer.observe(el));
  } else {
    revealed.forEach((el) => el.classList.add("is-visible"));
  }

  document.getElementById("year").textContent = new Date().getFullYear();
})();
