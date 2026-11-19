/* SimFramework · Paper Ocean — 共享外壳与交互 */
(function () {
  "use strict";

  var PATH = (location.pathname.split("/").pop() || "index.html").toLowerCase();

  var ICON = {
    grid: '<rect x="3" y="3" width="7.5" height="7.5" rx="1.6"/><rect x="13.5" y="3" width="7.5" height="7.5" rx="1.6"/><rect x="3" y="13.5" width="7.5" height="7.5" rx="1.6"/><rect x="13.5" y="13.5" width="7.5" height="7.5" rx="1.6"/>',
    flow: '<rect x="3" y="3" width="6" height="6" rx="1.4"/><rect x="15" y="15" width="6" height="6" rx="1.4"/><path d="M9 6h5a4 4 0 0 1 4 4v5"/><path d="M6 9v5a4 4 0 0 0 4 4h5"/>',
    pen: '<path d="M4 20h4L19.5 8.5a2.1 2.1 0 0 0-3-3L5 17v3Z"/><path d="M14.5 5.5 18.5 9.5"/>',
    users: '<circle cx="9" cy="8" r="3.4"/><path d="M2.8 20a6.2 6.2 0 0 1 12.4 0"/><path d="M16.5 5.6a3 3 0 0 1 0 5.8"/><path d="M17.5 14.4A5 5 0 0 1 21.2 19"/>',
    shield: '<path d="M12 3 5 6v5.5c0 4.4 2.9 7.6 7 9.5 4.1-1.9 7-5.1 7-9.5V6Z"/><path d="m9.2 12 2 2 3.6-3.8"/>',
    tree: '<rect x="9" y="3" width="6" height="5" rx="1.2"/><rect x="3" y="16" width="6" height="5" rx="1.2"/><rect x="15" y="16" width="6" height="5" rx="1.2"/><path d="M12 8v4M12 12H6v4M12 12h6v4"/>',
    sliders: '<path d="M4 7h9M17 7h3M4 12h3M11 12h9M4 17h9M17 17h3"/><circle cx="15" cy="7" r="2"/><circle cx="9" cy="12" r="2"/><circle cx="15" cy="17" r="2"/>',
    bell: '<path d="M18 9a6 6 0 1 0-12 0c0 5-2 6-2 6h16s-2-1-2-6"/><path d="M10 19a2.2 2.2 0 0 0 4 0"/>',
    plug: '<path d="M9 3v5M15 3v5"/><path d="M6 8h12v3a6 6 0 0 1-12 0Z"/><path d="M12 17v4"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="m16.5 16.5 4 4"/>',
    menu: '<path d="M4 7h16M4 12h16M4 17h10"/>',
    x: '<path d="M6 6l12 12M18 6 6 18"/>',
    copy: '<rect x="9" y="9" width="11" height="11" rx="2"/><path d="M15 5.5V5a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h.5"/>',
    check: '<path d="m5 12.5 4.5 4.5L19 6.5"/>',
    clock: '<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3.2 2"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    arrow: '<path d="M4 12h15"/><path d="m13 6 6 6-6 6"/>',
    chevron: '<path d="m9 5 7 7-7 7"/>',
    chevronDown: '<path d="m5 9 7 7 7-7"/>',
    download: '<path d="M12 4v11"/><path d="m7.5 10.5 4.5 4.5 4.5-4.5"/><path d="M4 19h16"/>',
    filter: '<path d="M4 6h16M7 12h10M10 18h4"/>',
    more: '<circle cx="6" cy="12" r="1.2"/><circle cx="12" cy="12" r="1.2"/><circle cx="18" cy="12" r="1.2"/>',
    mail: '<rect x="3" y="5" width="18" height="14" rx="2.2"/><path d="m4 7 8 6 8-6"/>',
    chat: '<path d="M20.5 12a8 8 0 0 1-11.7 7.1L4 20.5l1.4-4.7A8 8 0 1 1 20.5 12Z"/>',
    webhook: '<path d="M12 3v6"/><circle cx="12" cy="12" r="3"/><path d="m9.4 14.8-3.2 5.4M14.6 14.8l3.2 5.4"/><path d="M4.5 9.5 8 11.2M19.5 9.5 16 11.2"/>',
    key: '<circle cx="8" cy="15.5" r="4.5"/><path d="m11.4 12.1 8-8M16 4l3.5 3.5M13.5 6.5 16.5 9.5"/>',
    db: '<ellipse cx="12" cy="6" rx="7.5" ry="3"/><path d="M4.5 6v12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3V6"/><path d="M4.5 12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3"/>',
    doc: '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z"/><path d="M14 3v5h5M9 13h6M9 17h4"/>',
    alert: '<path d="M12 4.5 21 20H3Z"/><path d="M12 10v4.5M12 17.4v.2"/>',
    external: '<path d="M14 4h6v6M20 4l-8 8"/><path d="M18 14v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h5"/>',
    palette: '<path d="M12 3a9 9 0 1 0 0 18h1.5a2 2 0 0 0 1.5-3.3 2 2 0 0 1 1.5-3.2H19a3 3 0 0 0 3-3A9 9 0 0 0 12 3Z"/><circle cx="8" cy="9" r="1"/><circle cx="12" cy="7" r="1"/><circle cx="16" cy="10" r="1"/>',
    send: '<path d="M21 3 3 10.5l7 2.5 2.5 7L21 3Z"/><path d="m10 13 4-4"/>',
    building: '<rect x="4" y="4" width="10" height="17" rx="1.6"/><path d="M14 9h6v12h-6"/><path d="M7.5 8h3M7.5 12h3M7.5 16h3M17 13h.5M17 17h.5"/>',
    layers: '<path d="m12 3 9 5-9 5-9-5 9-5Z"/><path d="m3 13 9 5 9-5"/>',
    refresh: '<path d="M20 11a8 8 0 0 0-14-4.5L4 9"/><path d="M4 5v4h4"/><path d="M4 13a8 8 0 0 0 14 4.5L20 15"/><path d="M20 19v-4h-4"/>',
    link: '<path d="M10 13.5a4 4 0 0 0 5.7 0l2.8-2.8a4 4 0 0 0-5.7-5.7L11.5 6"/><path d="M14 10.5a4 4 0 0 0-5.7 0l-2.8 2.8a4 4 0 0 0 5.7 5.7l1.3-1.3"/>'
  };

  function icon(name, cls) {
    var body = ICON[name] || ICON.grid;
    return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"' +
      (cls ? ' class="' + cls + '"' : "") + ' aria-hidden="true">' + body + "</svg>";
  }

  var NAV = [
    { label: "概览", items: [{ id: "dashboard", href: "dashboard.html", label: "工作台", icon: "grid" }] },
    { label: "流程", items: [{ id: "approval", href: "approval.html", label: "审批中心", icon: "flow", badge: "12" }] },
    { label: "权限与组织", items: [
      { id: "users", href: "users.html", label: "用户管理", icon: "users" },
      { id: "roles", href: "roles.html", label: "角色权限", icon: "shield" },
      { id: "organization", href: "organization.html", label: "组织架构", icon: "tree" }
    ] },
    { label: "系统", items: [
      { id: "settings", href: "settings.html", label: "系统配置", icon: "sliders" },
      { id: "notifications", href: "notifications.html", label: "通知中心", icon: "bell" }
    ] },
  ];

  var PAGE = document.body.getAttribute("data-page") || "";
  var TITLE = document.body.getAttribute("data-title") || "工作台";
  var current = null;
  NAV.forEach(function (group) {
    group.items.forEach(function (item) { if (item.id === PAGE) current = item; });
  });

  function sidebarHTML() {
    var nav = NAV.map(function (group) {
      return '<div class="nav__label">' + group.label + "</div>" +
        group.items.map(function (item) {
          var active = item.id === PAGE;
          return '<a class="nav__item' + (active ? " is-active" : "") + '" href="' + item.href + '"' +
            (active ? ' aria-current="page"' : "") + ' data-od-id="nav-' + item.id + '">' +
            icon(item.icon) + "<span>" + item.label + "</span>" +
            (item.badge ? '<span class="nav__badge">' + item.badge + "</span>" : "") + "</a>";
        }).join("");
    }).join("");

    return '<div class="brand">' +
      '<span class="brand__mark">' + icon("layers") + "</span>" +
      '<span class="brand__text"><span class="brand__name">Sim<em>Framework</em><span class="brand__ver">v2.4</span></span>' +
      '<span class="brand__sub">模块化企业中台</span></span></div>' +
      '<nav class="nav" aria-label="主导航">' + nav + "</nav>" +
      '<div class="sidebar__foot">' +
        '<div class="quota"><div class="quota__top"><span class="quota__label">本月 API 调用</span>' +
        '<span class="quota__value">68%</span></div><div class="quota__track"><span class="quota__fill"></span></div>' +
        '<div class="tiny muted" style="margin-top:6px">1.36M / 2.0M 次</div></div>' +
        '<div class="sidebar__links">' +
        '<a href="design-system.html">' + icon("palette") + "设计规范</a>" +
        '<a href="index.html">' + icon("grid") + "改版总览</a></div>" +
      "</div>";
  }

  function topbarHTML() {
    var crumb = current ? '<li><span>控制台</span></li><li>' + icon("chevron") + "<strong>" + TITLE + "</strong></li>" : "<li><strong>" + TITLE + "</strong></li>";
    return '<button class="icon-btn" id="sidebar-toggle" type="button" aria-label="打开导航" aria-expanded="false" aria-controls="sidebar">' + icon("menu") + "</button>" +
      '<div class="topbar__crumb"><ol>' + crumb + "</ol>" +
      '<div class="topbar__title" data-od-id="page-title">' + TITLE + "</div></div>" +
      '<button class="topbar__search" type="button" id="palette-open" aria-label="搜索页面与操作">' + icon("search") +
      "<span>搜索页面、审批单、用户…</span><kbd>Ctrl</kbd><kbd>K</kbd></button>" +
      '<div class="topbar__actions">' +
      '<button class="icon-btn" type="button" data-help title="快捷键与帮助" aria-label="快捷键与帮助">' + icon("key") + "</button>" +
      '<div style="position:relative">' +
      '<button class="icon-btn" type="button" id="notif-btn" aria-label="通知，3 条未读" aria-expanded="false">' + icon("bell") +
      '<span class="icon-btn__dot" aria-hidden="true"></span></button>' +
      '<div class="popover" id="notif-pop" hidden>' +
      '<div class="popover__head"><b>通知</b><button class="link small" type="button" data-read-all>全部已读</button></div>' +
      '<div class="popover__list">' +
      '<a class="popover__item" href="approval.html"><span class="pill pill--peach">审批</span><div><b>「年度云服务续约」等待法务复核</b><span>王牧 · 2 小时前</span></div></a>' +
      '<a class="popover__item" href="notifications.html"><span class="pill pill--sky">投递</span><div><b>邮件渠道今日重试 2 次后成功</b><span>通知平台 · 3 小时前</span></div></a>' +
      "</div></div></div>" +
      '<button class="icon-btn" type="button" data-help title="帮助中心" aria-label="帮助中心">' + icon("alert") + "</button>" +
      '<span class="topbar__divider" aria-hidden="true"></span>' +
      '<div style="position:relative">' +
      '<button class="user-chip" type="button" id="user-btn" aria-expanded="false">' +
      '<span class="user-chip__avatar">林</span><span style="text-align:left"><span class="user-chip__name">林知远</span>' +
      '<span class="user-chip__role">平台管理员</span></span>' + icon("chevronDown") + "</button>" +
      '<div class="popover popover--user" id="user-pop" hidden>' +
      '<a class="popover__row" href="users.html">个人资料</a>' +
      '<a class="popover__row" href="roles.html">我的权限</a>' +
      '<a class="popover__row" href="settings.html">系统配置</a>' +
      '<button class="popover__row" type="button" data-signout>退出登录</button>' +
      "</div></div></div>";
  }

  function mountShell() {
    var shell = document.getElementById("shell");
    if (!shell || document.body.getAttribute("data-shell") !== "app") return;
    var main = shell.querySelector(".shell__main");
    var aside = document.createElement("aside");
    aside.className = "sidebar";
    aside.id = "sidebar";
    aside.innerHTML = sidebarHTML();
    shell.insertBefore(aside, main);
    var header = document.createElement("header");
    header.className = "topbar";
    header.id = "topbar";
    header.innerHTML = topbarHTML();
    main.insertBefore(header, main.firstChild);

    var scrim = document.createElement("div");
    scrim.className = "scrim";
    scrim.id = "nav-scrim";
    scrim.hidden = true;
    document.body.appendChild(scrim);

    var toggle = document.getElementById("sidebar-toggle");
    function closeNav() {
      aside.classList.remove("is-open");
      scrim.hidden = true;
      toggle.setAttribute("aria-expanded", "false");
    }
    toggle.addEventListener("click", function () {
      var open = !aside.classList.contains("is-open");
      aside.classList.toggle("is-open", open);
      scrim.hidden = !open;
      toggle.setAttribute("aria-expanded", String(open));
    });
    scrim.addEventListener("click", closeNav);
    aside.querySelectorAll("a").forEach(function (a) { a.addEventListener("click", closeNav); });

    var notifBtn = document.getElementById("notif-btn");
    var notifPop = document.getElementById("notif-pop");
    var userBtn = document.getElementById("user-btn");
    var userPop = document.getElementById("user-pop");

    function closePopovers(except) {
      [notifPop, userPop].forEach(function (pop) {
        if (pop && pop !== except) {
          pop.hidden = true;
          var btn = pop.id === "notif-pop" ? notifBtn : userBtn;
          if (btn) btn.setAttribute("aria-expanded", "false");
        }
      });
    }
    notifBtn.addEventListener("click", function (e) {
      e.stopPropagation();
      var open = notifPop.hidden;
      closePopovers(notifPop);
      notifPop.hidden = !open;
      notifBtn.setAttribute("aria-expanded", String(open));
    });
    userBtn.addEventListener("click", function (e) {
      e.stopPropagation();
      var open = userPop.hidden;
      closePopovers(userPop);
      userPop.hidden = !open;
      userBtn.setAttribute("aria-expanded", String(open));
    });
    document.addEventListener("click", function (e) {
      if (!e.target.closest(".popover") && !e.target.closest("#notif-btn") && !e.target.closest("#user-btn")) closePopovers();
    });
    var readAll = notifPop.querySelector("[data-read-all]");
    if (readAll) readAll.addEventListener("click", function () {
      notifBtn.querySelector(".icon-btn__dot").hidden = true;
      notifBtn.setAttribute("aria-label", "通知，无未读");
      notifPop.querySelectorAll(".popover__item").forEach(function (i) { i.classList.add("is-read"); });
      toast("已全部标记为已读");
    });
    notifPop.querySelectorAll(".popover__item").forEach(function (i) {
      i.addEventListener("click", function () { closePopovers(); });
    });
    document.querySelectorAll("[data-help]").forEach(function (b) {
      b.addEventListener("click", function () { toast("快捷键：Ctrl K 搜索 · Esc 关闭浮层"); });
    });
    document.querySelectorAll("[data-signout]").forEach(function (b) {
      b.addEventListener("click", function () { location.href = "login.html"; });
    });

    document.addEventListener("keydown", function (e) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        openPalette();
      }
    });
  }

  /* ---------------- 命令面板 ---------------- */
  var paletteEl = null;

  function paletteData() {
    var pages = NAV.map(function (g) { return g.items.map(function (i) {
      return { label: i.label, group: "页面", icon: i.icon, href: i.href, hint: i.href.replace(".html", "") };
    }); }).flat();
    var actions = [
      { label: "发起审批", group: "操作", icon: "plus", href: "approval.html?new=1", hint: "approval" },
      { label: "新建用户", group: "操作", icon: "users", href: "users.html?new=1", hint: "users" },
      { label: "查看设计规范", group: "操作", icon: "palette", href: "design-system.html", hint: "spec" },
      { label: "改版总览", group: "操作", icon: "grid", href: "index.html", hint: "index" }
    ];
    return pages.concat(actions);
  }

  function openPalette() {
    if (paletteEl) { paletteEl.hidden = false; }
    else {
      paletteEl = document.createElement("div");
      paletteEl.className = "palette";
      paletteEl.setAttribute("role", "dialog");
      paletteEl.setAttribute("aria-modal", "true");
      paletteEl.setAttribute("aria-label", "搜索页面与操作");
      paletteEl.innerHTML = '<div class="palette__input">' + icon("search") +
        '<input type="search" id="palette-q" placeholder="输入页面名、操作或审批单号…" aria-label="搜索" autocomplete="off">' +
        '<button class="icon-btn" type="button" id="palette-close" aria-label="关闭">' + icon("x") + "</button></div>" +
        '<div class="palette__list" id="palette-list"></div>' +
        '<div class="palette__foot"><span>↑ ↓ 选择</span><span>Enter 打开</span><span>Esc 关闭</span></div>';
      document.body.appendChild(paletteEl);
      var scrim = document.createElement("div");
      scrim.className = "scrim";
      scrim.id = "palette-scrim";
      document.body.appendChild(scrim);
      scrim.addEventListener("click", closePalette);
      paletteEl.querySelector("#palette-close").addEventListener("click", closePalette);
      paletteEl.querySelector("#palette-q").addEventListener("input", renderPalette);
      paletteEl.addEventListener("keydown", function (e) {
        var items = Array.prototype.slice.call(paletteEl.querySelectorAll(".palette__item"));
        var idx = items.findIndex(function (i) { return i.classList.contains("is-active"); });
        if (e.key === "ArrowDown") { e.preventDefault(); setActive(items, Math.min(idx + 1, items.length - 1)); }
        else if (e.key === "ArrowUp") { e.preventDefault(); setActive(items, Math.max(idx - 1, 0)); }
        else if (e.key === "Enter") {
          var pick = items[idx] || items[0];
          if (pick) { e.preventDefault(); location.href = pick.getAttribute("href"); }
        }
      });
      renderPalette();
      setTimeout(function () { paletteEl.querySelector("#palette-q").focus(); }, 10);
    }
    renderPalette();
    var q = paletteEl.querySelector("#palette-q");
    q.value = "";
    q.focus();
  }

  function setActive(items, i) {
    items.forEach(function (it) { it.classList.remove("is-active"); });
    if (items[i]) { items[i].classList.add("is-active"); try { items[i].scrollIntoView({ block: "nearest" }); } catch (e) {} }
  }

  function renderPalette() {
    if (!paletteEl) return;
    var q = (paletteEl.querySelector("#palette-q").value || "").trim().toLowerCase();
    var all = paletteData().filter(function (it) {
      return !q || it.label.toLowerCase().indexOf(q) > -1 || it.hint.indexOf(q) > -1;
    });
    var html = "";
    var last = "";
    all.forEach(function (it, i) {
      if (it.group !== last) { html += '<div class="palette__group">' + it.group + "</div>"; last = it.group; }
      html += '<a class="palette__item' + (i === 0 ? " is-active" : "") + '" href="' + it.href + '">' +
        icon(it.icon) + "<span>" + it.label + "</span><small>" + it.hint + "</small></a>";
    });
    if (!all.length) html = '<div class="empty">' + icon("search") + "<b>没有匹配项</b><span>换个关键词试试</span></div>";
    paletteEl.querySelector("#palette-list").innerHTML = html;
    paletteEl.querySelectorAll(".palette__item").forEach(function (a) {
      a.addEventListener("mouseenter", function () { setActive(Array.prototype.slice.call(paletteEl.querySelectorAll(".palette__item")), Array.prototype.slice.call(paletteEl.querySelectorAll(".palette__item")).indexOf(a)); });
    });
  }

  function closePalette() {
    if (!paletteEl) return;
    paletteEl.hidden = true;
    var s = document.getElementById("palette-scrim");
    if (s) s.hidden = true;
  }

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") {
      if (paletteEl && !paletteEl.hidden) { closePalette(); return; }
      closeTopOverlay();
    }
  });
  document.addEventListener("DOMContentLoaded", function () {
    var open = document.getElementById("palette-open");
    if (open) open.addEventListener("click", openPalette);
  });

  /* ---------------- 通用浮层 / toast ---------------- */
  var lastFocus = null;

  function openOverlay(el, trigger) {
    lastFocus = trigger || document.activeElement;
    var scrim = document.getElementById("overlay-scrim");
    if (!scrim) {
      scrim = document.createElement("div");
      scrim.className = "scrim";
      scrim.id = "overlay-scrim";
      document.body.appendChild(scrim);
      scrim.addEventListener("click", closeTopOverlay);
    }
    scrim.hidden = false;
    el.hidden = false;
    if (el.classList.contains("drawer")) el.classList.add("is-open");
    var focusable = el.querySelector("input, select, textarea, button, [href]");
    if (focusable) setTimeout(function () { focusable.focus(); }, 60);
    document.body.style.overflow = "hidden";
  }

  function closeTopOverlay() {
    var scrim = document.getElementById("overlay-scrim");
    var openEl = document.querySelector(".modal:not([hidden]), .drawer.is-open");
    var notif = document.getElementById("notif-pop");
    if (notif) notif.hidden = true;
    var user = document.getElementById("user-pop");
    if (user) user.hidden = true;
    if (!openEl) { if (scrim) scrim.hidden = true; return; }
    openEl.hidden = true;
    openEl.classList.remove("is-open");
    if (scrim) scrim.hidden = true;
    document.body.style.overflow = "";
    if (lastFocus && lastFocus.focus) lastFocus.focus();
  }

  var toastEl = null;
  var toastTimer = null;
  function toast(text, kind) {
    if (!toastEl) {
      toastEl = document.createElement("div");
      toastEl.className = "toast";
      toastEl.setAttribute("role", "status");
      toastEl.setAttribute("aria-live", "polite");
      document.body.appendChild(toastEl);
    }
    toastEl.className = "toast" + (kind === "warn" ? " toast--warn" : "");
    toastEl.innerHTML = icon(kind === "warn" ? "alert" : "check") + "<span>" + text + "</span>";
    requestAnimationFrame(function () { toastEl.classList.add("is-visible"); });
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove("is-visible"); }, 2600);
  }
  window.UI = { toast: toast, openOverlay: openOverlay, closeOverlay: closeTopOverlay, icon: icon };

  /* ---------------- 通用绑定 ---------------- */
  function bindCommon() {
    document.querySelectorAll("[data-toast]").forEach(function (b) {
      b.addEventListener("click", function () { toast(b.getAttribute("data-toast")); });
    });
    document.querySelectorAll("[data-copy]").forEach(function (b) {
      b.addEventListener("click", function () {
        var text = b.getAttribute("data-copy");
        function ok() { toast("已复制到剪贴板"); }
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(text).then(ok, ok);
        } else { ok(); }
      });
    });
    document.querySelectorAll("[data-close]").forEach(function (b) {
      b.addEventListener("click", closeTopOverlay);
    });

    // 通用标签页：容器 [data-tabs]，按钮 [role=tab][aria-controls]
    document.querySelectorAll("[data-tabs]").forEach(function (wrap) {
      var tabs = Array.prototype.slice.call(wrap.querySelectorAll('[role="tab"]'));
      function select(tab) {
        tabs.forEach(function (t) {
          var on = t === tab;
          t.setAttribute("aria-selected", String(on));
          t.tabIndex = on ? 0 : -1;
          var panel = document.getElementById(t.getAttribute("aria-controls"));
          if (panel) panel.hidden = !on;
        });
        wrap.dispatchEvent(new CustomEvent("tabchange", { detail: { id: tab.id } }));
      }
      tabs.forEach(function (t) {
        t.addEventListener("click", function () { select(t); });
        t.addEventListener("keydown", function (e) {
          var i = tabs.indexOf(t);
          if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
            e.preventDefault();
            var next = tabs[(i + (e.key === "ArrowRight" ? 1 : tabs.length - 1)) % tabs.length];
            next.focus(); select(next);
          }
        });
      });
      var initial = tabs.filter(function (t) { return t.getAttribute("aria-selected") === "true"; })[0] || tabs[0];
      if (initial) select(initial);
    });
  }

  /* ---------------- 工作台 ---------------- */
  function initDashboard() {
    document.querySelectorAll("[data-refresh-dashboard]").forEach(function (b) {
      b.addEventListener("click", function () {
        b.disabled = true;
        toast("正在同步工作台数据…");
        setTimeout(function () { b.disabled = false; toast("工作台数据已刷新"); }, 700);
      });
    });
  }

  /* ---------------- 审批中心 ---------------- */
  var APPROVALS = [
    {
      id: "AP-20261022-0184", type: "报销", icon: "doc", tone: "peach",
      title: "差旅报销 · 上海客户拜访", applicant: "陈嘉禾", dept: "市场部 · 品牌增长组",
      amount: "¥ 4,860.00", node: "财务复核", waited: "3 小时", due: "今天 18:00", urgent: true,
      submitted: "10-22 09:14", status: "待我审批", category: "待办",
      fields: [
        ["报销类型", "差旅费 · 市内交通"], ["费用归属", "市场部 / Q4 客户拜访"],
        ["发票张数", "6 张（已上传）"], ["预算科目", "MKT-TRAVEL-2026"]
      ],
      reason: "10 月 20 日赴上海拜访三位重点客户，含高铁二等座往返、两晚住宿与市内交通，票据齐全，均已粘贴上传。",
      flow: [
        ["提交申请", "陈嘉禾 · 市场部", "10-22 09:14", "done", "6 张发票已通过 OCR 校验"],
        ["直属主管审批", "周雨桐 · 市场部负责人", "10-22 10:02", "done", "同意，费用在 Q4 预算内"],
        ["财务复核", "你 · 平台财务共享", "等待中", "current", "关注超标项：住宿 ¥680/晚，超出标准 ¥80"],
        ["出纳付款", "待分配", "—", "wait", ""]
      ]
    },
    {
      id: "AP-20261022-0181", type: "采购", icon: "plug", tone: "sky",
      title: "采购申请 · 设计团队 4 台显示器", applicant: "林知远", dept: "产品设计中心",
      amount: "¥ 12,400.00", node: "采购复核", waited: "1 天", due: "10-23 12:00", urgent: true,
      submitted: "10-21 15:40", status: "待我审批", category: "待办",
      fields: [["采购类别", "IT 外设"], ["供应商", "已入围 · 晨光数码"], ["预计到货", "10-28"], ["资产归属", "设计中心 / PD-03"]],
      reason: "现有 4 台 24 寸显示器色域不足，影响品牌视觉交付。申请采购 27 寸 4K 显示器 4 台，单价 ¥3,100。",
      flow: [
        ["提交申请", "林知远 · 产品设计中心", "10-21 15:40", "done", ""],
        ["部门负责人", "苏晚 · 产品设计中心", "10-21 17:20", "done", "同意，纳入 Q4 设备更新"],
        ["采购复核", "你 · 采购与行政", "等待中", "current", "比价单已附，建议核对供应商入围有效期"],
        ["财务预算校验", "待分配", "—", "wait", ""]
      ]
    },
    {
      id: "AP-20261021-0176", type: "请假", icon: "clock", tone: "mint",
      title: "年假申请 · 10/24 – 10/26 共 3 天", applicant: "周雨桐", dept: "市场部",
      amount: "3 个工作日", node: "直属主管", waited: "5 小时", due: "10-23 18:00", urgent: false,
      submitted: "10-21 11:05", status: "待我审批", category: "待办",
      fields: [["假期类型", "带薪年假"], ["剩余额度", "7.5 天"], ["工作交接", "陈嘉禾"], ["紧急联系人", "王牧"]],
      reason: "10 月 24 日至 26 日休年假，期间客户拜访由陈嘉禾代管，重要审批已转交。",
      flow: [
        ["提交申请", "周雨桐 · 市场部", "10-21 11:05", "done", ""],
        ["直属主管审批", "你 · 市场部", "等待中", "current", "团队同期已有 1 人休假，请确认排班"],
        ["人事备案", "待分配", "—", "wait", ""]
      ]
    },
    {
      id: "AP-20261020-0169", type: "合同", icon: "doc", tone: "lilac",
      title: "合同用印 · 年度云服务续约", applicant: "王牧", dept: "信息技术部",
      amount: "¥ 386,000.00", node: "法务审核", waited: "2 天", due: "10-22 17:00", urgent: true,
      submitted: "10-20 16:22", status: "待我审批", category: "待办",
      fields: [["合同编号", "HT-2026-041"], ["相对方", "云枢科技（上海）"], ["合同期限", "2027-01-01 起 12 个月"], ["付款方式", "按季度预付"]],
      reason: "年度云资源续约，含生产集群 12 节点与对象存储 20TB，单价较去年下降 6.4%。",
      flow: [
        ["提交申请", "王牧 · 信息技术部", "10-20 16:22", "done", ""],
        ["技术负责人", "赵一鸣 · 数据平台", "10-20 18:05", "done", "资源规格确认无误"],
        ["法务审核", "你 · 法务合规", "等待中", "current", "请重点查看第 7 条数据出境与第 12 条 SLA 赔付"],
        ["用印归档", "待分配", "—", "wait", ""]
      ]
    },
    {
      id: "AP-20261020-0163", type: "权限", icon: "key", tone: "sun",
      title: "权限开通 · 生产数据库只读账号", applicant: "赵一鸣", dept: "数据平台",
      amount: "有效期 30 天", node: "安全审批", waited: "6 小时", due: "10-23 10:00", urgent: false,
      submitted: "10-20 10:30", status: "待我审批", category: "待办",
      fields: [["目标系统", "数据中台 / 生产库"], ["账号类型", "只读 · 30 天"], ["访问范围", "ods_* / dws_*"], ["审计要求", "全量 SQL 留痕"]],
      reason: "排查 10 月报表延迟问题，需要临时只读权限访问生产库，到期自动回收。",
      flow: [
        ["提交申请", "赵一鸣 · 数据平台", "10-20 10:30", "done", ""],
        ["数据负责人", "王牧 · 信息技术部", "10-20 14:10", "done", "同意，限定库表范围"],
        ["安全审批", "你 · 安全合规", "等待中", "current", "请确认审计日志已开启"],
        ["自动开通", "待分配", "—", "wait", "到期后由定时任务自动回收"]
      ]
    },
    {
      id: "AP-20261019-0155", type: "供应商", icon: "building", tone: "peach",
      title: "供应商准入 · 视觉素材库续签", applicant: "苏晚", dept: "产品设计中心",
      amount: "年度 ¥ 48,000", node: "合规复核", waited: "4 天", due: "已超时 1 天", urgent: true,
      submitted: "10-19 09:48", status: "待我审批", category: "待办",
      fields: [["供应商", "像素工坊"], ["准入类别", "设计资源"], ["合作年限", "第 2 年"], ["资质文件", "3 份 · 已核验"]],
      reason: "视觉素材库年度续签，覆盖 12 名设计师账号，含商用授权与二次创作许可。",
      flow: [
        ["提交申请", "苏晚 · 产品设计中心", "10-19 09:48", "done", ""],
        ["采购评估", "林知远 · 采购与行政", "10-19 15:30", "done", "价格与去年持平"],
        ["合规复核", "你 · 法务合规", "等待中", "current", "已超时 1 天，请优先处理"],
        ["建档入库", "待分配", "—", "wait", ""]
      ]
    },
    {
      id: "AP-20261018-0142", type: "报销", icon: "doc", tone: "peach",
      title: "差旅报销 · 北京行业峰会", applicant: "陈嘉禾", dept: "市场部",
      amount: "¥ 2,180.00", node: "已完成", waited: "—", due: "10-19 已完成", urgent: false,
      submitted: "10-18 08:20", status: "已通过", category: "已处理",
      fields: [["报销类型", "会议差旅"], ["费用归属", "市场部 / 行业活动"], ["发票张数", "4 张"], ["付款单号", "PAY-20261019-0071"]],
      reason: "参加北京行业峰会，含高铁往返与一晚住宿。",
      flow: [
        ["提交申请", "陈嘉禾 · 市场部", "10-18 08:20", "done", ""],
        ["直属主管审批", "周雨桐 · 市场部", "10-18 10:00", "done", "同意"],
        ["财务复核", "你 · 财务共享", "10-18 14:30", "done", "通过"],
        ["出纳付款", "财务共享", "10-19 09:12", "done", "已付款"]
      ]
    },
    {
      id: "AP-20261017-0139", type: "采购", icon: "plug", tone: "sky",
      title: "采购申请 · 会议室视频终端", applicant: "周雨桐", dept: "市场部",
      amount: "¥ 6,800.00", node: "已驳回", waited: "—", due: "10-18 已驳回", urgent: false,
      submitted: "10-17 13:05", status: "已驳回", category: "已处理",
      fields: [["采购类别", "会议室设备"], ["供应商", "待定"], ["数量", "1 套"], ["预算科目", "ADM-OFFICE-2026"]],
      reason: "3 号会议室视频终端老化，申请更换。",
      flow: [
        ["提交申请", "周雨桐 · 市场部", "10-17 13:05", "done", ""],
        ["采购复核", "你 · 采购与行政", "10-18 09:20", "done", "驳回：Q4 设备预算已冻结，建议并入 2027 年预算"],
        ["结束", "—", "10-18 09:20", "done", ""]
      ]
    }
  ];

  var approvalState = { filter: "待办", selected: 0, done: {} };

  function initApproval() {
    var listEl = document.getElementById("approval-list");
    if (!listEl) return;
    var detailEl = document.getElementById("approval-detail");
    var tabs = document.querySelectorAll("[data-approval-filter]");

    function visible() {
      return APPROVALS.filter(function (a) {
        var st = approvalState.done[a.id] || a.status;
        if (approvalState.filter === "待办") return st === "待我审批" || st === "已通过" && false;
        if (approvalState.filter === "已处理") return st !== "待我审批";
        return true;
      });
    }

    function renderList() {
      var items = visible();
      if (approvalState.selected >= items.length) approvalState.selected = 0;
      if (!items.length) {
        listEl.innerHTML = '<div class="empty">' + icon("check") + "<b>这里没有待办</b><span>今天的审批都处理完了</span></div>";
        detailEl.innerHTML = '<div class="empty">' + icon("flow") + "<b>选择一张审批单</b><span>右侧会显示业务单据与流程轨迹</span></div>";
        return;
      }
      listEl.innerHTML = items.map(function (a, i) {
        var st = approvalState.done[a.id] || a.status;
        var badge = st === "已通过" ? '<span class="pill pill--mint">已通过</span>' :
          st === "已驳回" ? '<span class="pill pill--peach">已驳回</span>' :
          a.urgent ? '<span class="pill pill--coral">加急</span>' : '<span class="pill">待处理</span>';
        return '<button class="appr-item" type="button" role="option" data-id="' + a.id + '" data-index="' + i + '"' +
          ' aria-selected="' + (i === approvalState.selected) + '" data-od-id="approval-item-' + (i + 1) + '">' +
          '<span class="appr-item__top"><span class="appr-item__type" style="background:var(--' + a.tone + ')">' + icon(a.icon) + "</span>" +
          '<span class="appr-item__title">' + a.title + "</span></span>" +
          '<span class="appr-item__meta"><span>' + a.applicant + " · " + a.dept.split(" · ")[0] + "</span><span class=mono>" + a.id.slice(-4) + "</span></span>" +
          '<span class="appr-item__foot">' + badge +
          '<span class="pill pill--outline">' + a.node + "</span>" +
          '<span class="sla' + (a.urgent ? " sla--hot" : "") + '">' + icon("clock") + a.waited + "</span></span></button>";
      }).join("");
      listEl.querySelectorAll(".appr-item").forEach(function (b) {
        b.addEventListener("click", function () {
          approvalState.selected = Number(b.getAttribute("data-index"));
          renderList();
          renderDetail(items[approvalState.selected]);
        });
      });
      renderDetail(items[approvalState.selected]);
      var counter = document.getElementById("approval-count");
      if (counter) counter.textContent = String(APPROVALS.filter(function (a) { return (approvalState.done[a.id] || a.status) === "待我审批"; }).length);
    }

    function renderDetail(a) {
      if (!a) return;
      var st = approvalState.done[a.id] || a.status;
      var done = st === "已通过";
      var rejected = st === "已驳回";
      detailEl.innerHTML =
        '<div class="detail-head"><div style="min-width:0">' +
        '<div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap">' +
        '<span class="pill ' + (done ? "pill--mint" : rejected ? "pill--peach" : "pill--coral") + '">' + st + "</span>" +
        '<span class="mono tiny muted">' + a.id + "</span></div>" +
        "<h2 style=\"margin-top:8px\">" + a.title + "</h2>" +
        '<div class="detail-meta"><span>申请人 <b>' + a.applicant + "</b></span><span>" + a.dept + "</span>" +
        "<span>提交于 <b>" + a.submitted + "</b></span><span>当前节点 <b>" + a.node + "</b></span></div></div>" +
        '<div style="display:flex;gap:8px;flex-wrap:wrap">' +
        '<button class="btn btn--sm" type="button" data-approval-more>' + icon("more") + "更多</button></div></div>" +

        '<div class="detail-grid" style="margin-top:18px">' +
        '<div class="kv"><dt>金额 / 规模</dt><dd class="num">' + a.amount + "</dd></div>" +
        '<div class="kv"><dt>已等待</dt><dd>' + a.waited + ' · <span class="' + (a.urgent ? "sla sla--hot" : "muted") + '">' + a.due + "</span></dd></div>" +
        a.fields.map(function (f) { return '<div class="kv"><dt>' + f[0] + "</dt><dd>" + f[1] + "</dd></div>"; }).join("") +
        '<div class="kv full"><dt>申请事由</dt><dd style="font-weight:500;line-height:1.75">' + a.reason + "</dd></div>" +
        "</div>" +

        '<div style="margin-top:22px"><div class="card__head"><div><h2>流程轨迹</h2><p>共 ' + a.flow.length + " 个节点</p></div>" +
        '<span class="pill pill--outline">' + (done || rejected ? "已结束" : "进行中") + "</span></div>" +
        '<div class="timeline">' + a.flow.map(function (f) {
          return '<div class="tl tl--' + f[3] + '"><span class="tl__dot">' + (f[3] === "done" ? icon("check") : f[3] === "current" ? icon("clock") : "") + "</span>" +
            '<div><div class="tl__title">' + f[0] + ' <span class="tiny muted" style="font-weight:500">' + f[2] + "</span></div>" +
            '<div class="tl__meta">' + f[1] + "</div>" + (f[4] ? '<div class="tl__note">' + f[4] + "</div>" : "") + "</div></div>";
        }).join("") + "</div></div>" +

        (done || rejected ? '<div class="note" style="margin-top:18px">' + icon("check") + "<span>该审批已" + (done ? "通过" : "驳回") + "，流程记录已归档，可在「已处理」中回看。</span></div>" :
          '<div style="margin-top:20px;padding-top:18px;border-top:1.5px dashed var(--line-mid)">' +
          '<div class="field"><label for="approval-comment">审批意见</label>' +
          '<textarea class="textarea" id="approval-comment" placeholder="填写同意理由或驳回原因，将同步给申请人…"></textarea></div>' +
          '<div style="display:flex;gap:9px;flex-wrap:wrap;margin-top:12px">' +
          '<button class="btn btn--primary" type="button" id="approval-approve">' + icon("check") + "同意</button>" +
          '<button class="btn" type="button" id="approval-reject">' + icon("x") + "驳回</button>" +
          '<button class="btn btn--ghost" type="button" data-toast="已打开转交面板">' + icon("arrow") + "转交</button>" +
          "</div></div>");

      var approve = detailEl.querySelector("#approval-approve");
      var reject = detailEl.querySelector("#approval-reject");
      if (approve) approve.addEventListener("click", function () { openAction(a, "approve"); });
      if (reject) reject.addEventListener("click", function () { openAction(a, "reject"); });
      detailEl.querySelectorAll("[data-approval-more]").forEach(function (b) {
        b.addEventListener("click", function () { toast("可下载审批单 PDF 或导出流程记录"); });
      });
    }

    function openAction(a, kind) {
      var modal = document.getElementById("action-modal");
      if (!modal) return;
      var isApprove = kind === "approve";
      modal.querySelector("[data-modal-title]").textContent = isApprove ? "确认同意该审批？" : "驳回该审批";
      modal.querySelector("[data-modal-desc]").textContent = a.title + " · " + a.id;
      var comment = modal.querySelector("#action-comment");
      comment.value = isApprove ? "同意，材料齐全，符合流程要求。" : "";
      comment.placeholder = isApprove ? "补充审批意见（可选）" : "请填写驳回原因，申请人会收到通知";
      modal.querySelector("[data-modal-submit]").className = "btn " + (isApprove ? "btn--primary" : "btn--danger");
      modal.querySelector("[data-modal-submit]").textContent = isApprove ? "确认同意" : "确认驳回";
      modal.dataset.id = a.id;
      modal.dataset.kind = kind;
      openOverlay(modal, document.activeElement);
    }

    var actionModal = document.getElementById("action-modal");
    if (actionModal) {
      actionModal.querySelector("[data-modal-submit]").addEventListener("click", function (e) {
        var id = actionModal.dataset.id;
        var kind = actionModal.dataset.kind;
        if (kind === "reject" && !actionModal.querySelector("#action-comment").value.trim()) {
          toast("请填写驳回原因", "warn");
          return;
        }
        approvalState.done[id] = kind === "approve" ? "已通过" : "已驳回";
        closeTopOverlay();
        renderList();
        var badge = document.querySelector('[data-od-id="nav-approval"] .nav__badge');
        if (badge) {
          var n = Math.max(0, Number(badge.textContent) - 1);
          badge.textContent = String(n);
          if (n === 0) badge.className = "nav__badge nav__badge--quiet";
        }
        toast(kind === "approve" ? "已同意，流程进入下一节点" : "已驳回，申请人将收到通知");
      });
    }

    tabs.forEach(function (t) {
      t.addEventListener("click", function () {
        tabs.forEach(function (x) { x.setAttribute("aria-selected", "false"); });
        t.setAttribute("aria-selected", "true");
        approvalState.filter = t.getAttribute("data-approval-filter");
        approvalState.selected = 0;
        renderList();
      });
    });

    listEl.setAttribute("role", "listbox");
    renderList();

    var params = new URLSearchParams(location.search);
    if (params.get("new") === "1") {
      document.querySelectorAll("[data-new-approval]").forEach(function (b) {
        openOverlay(document.getElementById("new-modal"), b);
      });
    }
  }

  /* ---------------- 用户管理 ---------------- */
  var USERS = [
    ["林知远", "lin.zhiyuan@sim.dev", "产品设计中心", ["平台管理员"], "启用", "10-22 09:41", "10.24.8.31", "sun"],
    ["周雨桐", "zhou.yutong@sim.dev", "市场部", ["部门负责人", "审批人"], "启用", "10-22 08:55", "10.24.8.12", "peach"],
    ["陈嘉禾", "chen.jiahe@sim.dev", "市场部", ["编辑"], "启用", "10-21 19:22", "10.24.9.44", "sky"],
    ["王牧", "wang.mu@sim.dev", "信息技术部", ["部门负责人", "系统管理员"], "启用", "10-22 07:10", "10.24.8.7", "mint"],
    ["赵一鸣", "zhao.yiming@sim.dev", "数据平台", ["开发者"], "启用", "10-20 23:48", "10.24.11.5", "lilac"],
    ["苏晚", "su.wan@sim.dev", "产品设计中心", ["编辑", "审批人"], "启用", "10-21 16:02", "10.24.9.18", "peach"],
    ["顾言", "gu.yan@sim.dev", "外部协作", ["访客"], "锁定", "10-12 11:30", "10.24.15.77", "sky"],
    ["何舟", "he.zhou@sim.dev", "财务共享", ["财务审核"], "停用", "09-28 14:05", "10.24.10.9", "mint"]
  ];

  function initUsers() {
    var body = document.getElementById("user-rows");
    if (!body) return;
    var filter = { q: "", status: "全部" };
    var selected = new Set();
    var statusChips = document.querySelectorAll("[data-user-status]");
    var search = document.getElementById("user-search");

    function rows() {
      return USERS.filter(function (u) {
        var okStatus = filter.status === "全部" || u[4] === filter.status;
        var q = filter.q.toLowerCase();
        var okQ = !q || (u[0] + u[1] + u[2]).toLowerCase().indexOf(q) > -1;
        return okStatus && okQ;
      });
    }

    function pill(status) {
      return status === "启用" ? '<span class="pill pill--mint pill--dot">启用</span>' :
        status === "锁定" ? '<span class="pill pill--sun pill--dot">锁定</span>' :
        '<span class="pill pill--peach pill--dot">停用</span>';
    }

    function render() {
      var list = rows();
      if (!list.length) {
        body.innerHTML = '<tr><td colspan="7"><div class="empty">' + icon("users") + "<b>没有匹配的用户</b><span>调整关键词或状态筛选</span></div></td></tr>";
      } else {
        body.innerHTML = list.map(function (u, i) {
          return '<tr data-user="' + u[1] + '" class="' + (selected.has(u[1]) ? "is-selected" : "") + '">' +
            '<td><input class="checkbox" type="checkbox" data-check="' + u[1] + '" aria-label="选择 ' + u[0] + '"' + (selected.has(u[1]) ? " checked" : "") + "></td>" +
            '<td><div class="table__person"><span class="avatar avatar--' + u[7] + '">' + u[0].slice(0, 1) + "</span><div><b>" + u[0] + "</b><span>" + u[1] + "</span></div></div></td>" +
            "<td>" + u[2] + "</td>" +
            '<td><div style="display:flex;gap:4px;flex-wrap:wrap">' + u[3].map(function (r, j) {
              return '<span class="pill ' + ["pill--sky", "pill--lilac", "pill--sun"][j % 3] + '">' + r + "</span>";
            }).join("") + "</div></td>" +
            "<td>" + pill(u[4]) + "</td>" +
            '<td><div class="small">' + u[5] + '</div><div class="mono tiny muted">' + u[6] + "</div></td>" +
            '<td><div class="cell-actions"><button class="icon-btn" type="button" data-open-user="' + u[1] + '" aria-label="查看 ' + u[0] + ' 详情">' + icon("chevron") + "</button></div></td></tr>";
        }).join("");
      }
      var total = document.getElementById("user-total");
      if (total) total.textContent = String(list.length);
      bindRowEvents();
      renderBulk();
    }

    function bindRowEvents() {
      body.querySelectorAll("[data-open-user]").forEach(function (b) {
        b.addEventListener("click", function (e) {
          e.stopPropagation();
          openUser(b.getAttribute("data-open-user"));
        });
      });
      body.querySelectorAll("tr[data-user]").forEach(function (tr) {
        tr.addEventListener("click", function (e) {
          if (e.target.closest("input")) return;
          openUser(tr.getAttribute("data-user"));
        });
      });
      body.querySelectorAll("[data-check]").forEach(function (c) {
        c.addEventListener("click", function (e) { e.stopPropagation(); });
        c.addEventListener("change", function () {
          if (c.checked) selected.add(c.getAttribute("data-check")); else selected.delete(c.getAttribute("data-check"));
          render();
        });
      });
    }

    function renderBulk() {
      var bar = document.getElementById("bulk-bar");
      if (!bar) return;
      bar.hidden = selected.size === 0;
      var n = document.getElementById("bulk-count");
      if (n) n.textContent = String(selected.size);
    }

    function openUser(email) {
      var u = USERS.filter(function (x) { return x[1] === email; })[0];
      if (!u) return;
      var drawer = document.getElementById("user-drawer");
      drawer.querySelector("[data-drawer-avatar]").textContent = u[0].slice(0, 1);
      drawer.querySelector("[data-drawer-avatar]").className = "avatar avatar--" + u[7] + " avatar--round";
      drawer.querySelector("[data-drawer-name]").textContent = u[0];
      drawer.querySelector("[data-drawer-mail]").textContent = u[1];
      drawer.querySelector("[data-drawer-status]").innerHTML = pill(u[4]);
      drawer.querySelector("[data-drawer-dept]").textContent = u[2];
      drawer.querySelector("[data-drawer-login]").textContent = u[5];
      drawer.querySelector("[data-drawer-ip]").textContent = u[6];
      drawer.querySelector("[data-drawer-roles]").innerHTML = u[3].map(function (r, j) {
        return '<span class="pill ' + ["pill--sky", "pill--lilac", "pill--sun"][j % 3] + '">' + r + "</span>";
      }).join("");
      openOverlay(drawer, document.activeElement);
    }

    statusChips.forEach(function (c) {
      c.addEventListener("click", function () {
        statusChips.forEach(function (x) { x.setAttribute("aria-pressed", "false"); });
        c.setAttribute("aria-pressed", "true");
        filter.status = c.getAttribute("data-user-status");
        render();
      });
    });
    if (search) search.addEventListener("input", function () { filter.q = search.value; render(); });

    var selectAll = document.getElementById("user-select-all");
    if (selectAll) selectAll.addEventListener("change", function () {
      if (selectAll.checked) rows().forEach(function (u) { selected.add(u[1]); });
      else selected.clear();
      render();
    });

    document.querySelectorAll("[data-bulk-delete]").forEach(function (b) {
      b.addEventListener("click", function () {
        toast("已提交批量停用申请（" + selected.size + " 个账号）", "warn");
        selected.clear();
        render();
      });
    });
    document.querySelectorAll("[data-bulk-export]").forEach(function (b) {
      b.addEventListener("click", function () { toast("已导出 " + selected.size + " 条用户记录"); });
    });

    render();
    var params = new URLSearchParams(location.search);
    if (params.get("new") === "1") {
      var modal = document.getElementById("new-user-modal");
      if (modal) openOverlay(modal, document.body);
    }
  }

  /* ---------------- 角色权限 ---------------- */
  function initRoles() {
    var matrix = document.getElementById("perm-matrix");
    if (!matrix) return;
    var save = document.getElementById("perm-save");
    var dirty = document.getElementById("perm-dirty");
    var boxes = Array.prototype.slice.call(matrix.querySelectorAll("tbody input[type=checkbox]"));
    var selects = Array.prototype.slice.call(matrix.querySelectorAll("[data-col-all]"));

    function markDirty() {
      boxes.forEach(function (b) { b.closest("tr").classList.add("is-dirty"); });
      if (dirty) dirty.hidden = false;
      if (save) save.disabled = false;
    }
    boxes.forEach(function (b) { b.addEventListener("change", markDirty); });
    selects.forEach(function (s) {
      s.addEventListener("change", function () {
        var col = s.getAttribute("data-col-all");
        matrix.querySelectorAll('tbody input[data-col="' + col + '"]').forEach(function (b) { b.checked = s.checked; });
        markDirty();
      });
    });
    if (save) save.addEventListener("click", function () {
      boxes.forEach(function (b) { b.closest("tr").classList.remove("is-dirty"); });
      if (dirty) dirty.hidden = true;
      save.disabled = true;
      toast("角色权限已保存，变更将在 5 分钟内生效");
    });
    document.querySelectorAll(".role-item").forEach(function (r) {
      r.addEventListener("click", function () {
        document.querySelectorAll(".role-item").forEach(function (x) { x.setAttribute("aria-selected", "false"); });
        r.setAttribute("aria-selected", "true");
        var name = document.getElementById("perm-role-name");
        if (name) name.textContent = r.getAttribute("data-role");
        toast("已切换到「" + r.getAttribute("data-role") + "」");
      });
    });
  }

  /* ---------------- 组织架构 ---------------- */
  var ORGS = {
    root: { name: "SimFramework 集团", leader: "林知远", count: 128, sub: "总部 · 上海", members: 5 },
    pd: { name: "产品设计中心", leader: "苏晚", count: 24, sub: "设计 / 体验 / 品牌", members: 4 },
    rd: { name: "研发中心", leader: "赵一鸣", count: 46, sub: "前端 / 后端 / 数据平台", members: 5 },
    mkt: { name: "市场部", leader: "周雨桐", count: 18, sub: "品牌增长 / 内容运营", members: 4 },
    fin: { name: "财务共享", leader: "何舟", count: 12, sub: "核算 / 结算 / 预算", members: 3 },
    it: { name: "信息技术部", leader: "王牧", count: 15, sub: "基础设施 / 安全合规", members: 4 }
  };

  function initOrg() {
    var tree = document.getElementById("org-tree");
    if (!tree) return;
    var members = document.getElementById("org-members");
    tree.querySelectorAll("[data-org]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        tree.querySelectorAll("[data-org]").forEach(function (b) { b.classList.remove("is-active"); });
        btn.classList.add("is-active");
        var key = btn.getAttribute("data-org");
        var org = ORGS[key];
        if (!org) return;
        var nameEl = document.getElementById("org-name");
        if (nameEl) nameEl.textContent = org.name;
        var subEl = document.getElementById("org-sub");
        if (subEl) subEl.textContent = org.sub;
        var leadEl = document.getElementById("org-leader");
        if (leadEl) leadEl.textContent = org.leader;
        var countEl = document.getElementById("org-count");
        if (countEl) countEl.textContent = String(org.count);
        var rows = members.querySelectorAll("tr");
        rows.forEach(function (tr, i) { tr.hidden = i >= org.members; });
      });
    });
    tree.querySelectorAll("[data-expand]").forEach(function (b) {
      b.addEventListener("click", function (e) {
        e.stopPropagation();
        var target = document.getElementById(b.getAttribute("data-expand"));
        if (!target) return;
        target.hidden = !target.hidden;
        b.querySelector("svg").style.transform = target.hidden ? "rotate(-90deg)" : "";
      });
    });
  }

  /* ---------------- 系统配置 ---------------- */
  function initSettings() {
    var form = document.getElementById("settings-form");
    if (!form) return;
    var saveBar = document.getElementById("settings-save");
    form.addEventListener("input", function () {
      if (saveBar) saveBar.hidden = false;
    });
    form.addEventListener("change", function () {
      if (saveBar) saveBar.hidden = false;
    });
    form.addEventListener("submit", function (e) {
      e.preventDefault();
      if (saveBar) saveBar.hidden = true;
      toast("配置已保存，部分变更将在下次请求时生效");
    });
    var hash = location.hash.replace("#", "");
    if (hash) {
      var tab = document.querySelector('[role="tab"][aria-controls="' + hash + '"]');
      if (tab) tab.click();
    }
  }

  /* ---------------- 通知中心 ---------------- */
  function initNotifications() {
    document.querySelectorAll("[data-provider-test]").forEach(function (b) {
      b.addEventListener("click", function () {
        var name = b.getAttribute("data-provider-test");
        b.disabled = true;
        b.textContent = "发送中…";
        setTimeout(function () {
          b.disabled = false;
          b.textContent = "发送测试";
          toast(name + "测试消息已发送至 lin.zhiyuan@sim.dev");
        }, 700);
      });
    });
    document.querySelectorAll(".switch input").forEach(function (input) {
      input.addEventListener("change", function () {
        var label = input.getAttribute("aria-label") || "渠道";
        toast(label + (input.checked ? " 已启用" : " 已停用"));
      });
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    mountShell();
    bindCommon();
    initDashboard();
    initApproval();
    initUsers();
    initRoles();
    initOrg();
    initSettings();
    initNotifications();
  });
})();
