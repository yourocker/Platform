"use strict";

const NOTIFICATION_API_BASE = "/api/notifications";
const NOTIFICATION_HUB_URL = "/hubs/notifications";
const notificationAntiforgeryTokenInput = document.getElementById("notificationAntiforgeryToken");
const notificationAntiforgeryToken = notificationAntiforgeryTokenInput
    ? notificationAntiforgeryTokenInput.value
    : null;

// Запрашиваем разрешение на системные уведомления при первом запуске
if (typeof Notification !== 'undefined' && Notification.permission !== "granted") {
    Notification.requestPermission();
}

if (document.getElementById("notificationSidebar")) {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(NOTIFICATION_HUB_URL)
        .withAutomaticReconnect()
        .build();

    // При получении нового уведомления
    connection.on("ReceiveNotification", function (data) {
        console.log("🔔 Получен сигнал уведомления:", data);

        // 1. Звук (Проверяем флаг playSound, пришедший с сервера)
        if (data.playSound) {
            new Audio('/sounds/notify.mp3').play().catch(() => {
                console.warn("Автовоспроизведение звука заблокировано браузером до первого клика пользователя.");
            });
        }

        // 2. Системный пуш (Проверяем флаг showDesktop И разрешение браузера)
        if (data.showDesktop && typeof Notification !== 'undefined' && Notification.permission === "granted") {
            new Notification(data.title, {
                body: data.message,
                icon: '/favicon.ico'
            });
        }

        // 3. Обновление интерфейса (делаем всегда, так как это не "беспокоит", а просто обновляет данные)
        addNotificationToSidebar(data, true);
        updateBadgeCount(1);
    });

    connection.onreconnected(function () {
        console.log(">>> 🔄 SignalR переподключен.");
        fetchHistory();
    });

    connection.start().then(function () {
        console.log(">>> ✅ SignalR подключен.");
    }).catch(err => console.error(">>> ❌ Ошибка SignalR:", err));

    fetchHistory();
}

// Загрузка истории
async function fetchHistory() {
    try {
        const response = await fetch(`${NOTIFICATION_API_BASE}/history`, {
            credentials: "same-origin"
        });
        if (!response.ok) return;

        const notifications = await response.json();
        const list = document.getElementById("notificationList");
        if (list) list.innerHTML = '';

        let unreadCount = 0;
        notifications.forEach(n => {
            addNotificationToSidebar(n, false);
            if (!n.isRead) unreadCount++;
        });

        if (unreadCount > 0) updateBadgeCount(unreadCount, true);
    } catch (e) {
        console.error("Ошибка загрузки истории:", e);
    }
}

// Пометка как прочитанных
const notificationSidebar = document.getElementById('notificationSidebar');
if (notificationSidebar) {
    notificationSidebar.addEventListener('show.bs.offcanvas', async function () {
        await fetchHistory();

        const badge = document.getElementById("notificationBadge");
        if (!badge || badge.style.display === "none") return;

        try {
            const response = await fetch(`${NOTIFICATION_API_BASE}/mark-as-read`, {
                method: 'POST',
                credentials: "same-origin",
                headers: buildPostHeaders()
            });
            if (response.ok) {
                badge.innerText = "0";
                badge.style.display = "none";
                document.querySelectorAll('.notification-item').forEach(el => el.classList.remove('bg-light', 'border-primary'));
            }
        } catch (e) {
            console.error("Ошибка при прочтении:", e);
        }
    });
}

function buildPostHeaders() {
    const headers = {
        "X-Requested-With": "XMLHttpRequest"
    };

    if (notificationAntiforgeryToken) {
        headers["X-CSRF-TOKEN"] = notificationAntiforgeryToken;
    }

    return headers;
}

function updateBadgeCount(amount, isAbsolute = false) {
    const badge = document.getElementById("notificationBadge");
    if (!badge) return;

    let current = isAbsolute ? 0 : (parseInt(badge.innerText) || 0);
    let total = current + amount;

    if (total > 0) {
        badge.innerText = total;
        badge.style.display = "inline-block";
    } else {
        badge.style.display = "none";
    }
}

function addNotificationToSidebar(data, isNew) {
    const list = document.getElementById("notificationList");
    if (!list) return;

    const isRead = data.isRead !== undefined ? data.isRead : !isNew;
    const itemClass = isRead ? "" : "bg-light border-start border-primary border-4";

    const item = document.createElement('div');
    item.className = `notification-item p-3 border-bottom mb-1 ${itemClass}`.trim();
    item.style.cursor = 'pointer';

    const normalizedUrl = normalizeNotificationUrl(data.url);
    item.addEventListener('click', () => {
        if (normalizedUrl !== '#') {
            location.href = normalizedUrl;
        }
    });

    const header = document.createElement('div');
    header.className = 'd-flex justify-content-between';

    const title = document.createElement('strong');
    title.className = 'text-dark';
    title.style.fontSize = '0.85rem';
    title.textContent = data.title || 'Уведомление';

    const createdAt = document.createElement('small');
    createdAt.className = 'text-muted';
    createdAt.style.fontSize = '0.7rem';
    createdAt.textContent = new Date(data.createdAt || Date.now())
        .toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    header.append(title, createdAt);

    const message = document.createElement('div');
    message.className = 'text-secondary mt-1';
    message.style.fontSize = '0.8rem';
    message.textContent = data.message || '';

    item.append(header, message);
    list.prepend(item);
}

function normalizeNotificationUrl(url) {
    return typeof url === 'string' && url.startsWith('/') ? url : '#';
}
