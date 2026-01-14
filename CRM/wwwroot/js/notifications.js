"use strict";

const NOTIFICATION_SERVICE_URL = "https://localhost:7163";
const NOTIFICATION_HUB_URL = `${NOTIFICATION_SERVICE_URL}/hubs/notifications`;

const currentUserIdInput = document.getElementById('currentUserId');
const currentUserId = currentUserIdInput ? currentUserIdInput.value : null;

// Запрашиваем разрешение на системные уведомления при первом запуске
if (typeof Notification !== 'undefined' && Notification.permission !== "granted") {
    Notification.requestPermission();
}

if (currentUserId) {
    const urlWithParams = `${NOTIFICATION_HUB_URL}?userId=${currentUserId}`;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(urlWithParams)
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

    connection.start().then(function () {
        console.log(">>> ✅ SignalR подключен.");
        fetchHistory();
    }).catch(err => console.error(">>> ❌ Ошибка SignalR:", err));
}

// Загрузка истории
async function fetchHistory() {
    try {
        const response = await fetch(`${NOTIFICATION_SERVICE_URL}/api/notifications/history/${currentUserId}`);
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
        const badge = document.getElementById("notificationBadge");
        if (!badge || badge.style.display === "none") return;

        try {
            const response = await fetch(`${NOTIFICATION_SERVICE_URL}/api/notifications/mark-as-read/${currentUserId}`, {
                method: 'POST'
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

    const html = `
        <div class="notification-item p-3 border-bottom mb-1 ${itemClass}" 
             style="cursor: pointer;" 
             onclick="location.href='${data.url || '#'}'">
            <div class="d-flex justify-content-between">
                <strong class="text-dark" style="font-size: 0.85rem;">${data.title}</strong>
                <small class="text-muted" style="font-size: 0.7rem;">
                    ${new Date(data.createdAt || Date.now()).toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}
                </small>
            </div>
            <div class="text-secondary mt-1" style="font-size: 0.8rem;">${data.message}</div>
        </div>`;

    list.insertAdjacentHTML('afterbegin', html);
}