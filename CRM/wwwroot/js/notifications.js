"use strict";

const NOTIFICATION_HUB_URL = "https://localhost:7163/hubs/notifications";
const currentUserIdInput = document.getElementById('currentUserId');
const currentUserId = currentUserIdInput ? currentUserIdInput.value : null;

// Запрашиваем разрешение на системные уведомления сразу при загрузке
if (typeof Notification !== 'undefined' && Notification.permission !== "granted") {
    Notification.requestPermission();
}

if (currentUserId) {
    const urlWithParams = `${NOTIFICATION_HUB_URL}?userId=${currentUserId}`;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(urlWithParams)
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", function (data) {
        console.log("🔔 Получено уведомление:", data);

        // 1. Проигрываем звук (файл должен быть по этому пути)
        const audio = new Audio('/sounds/notify.mp3');
        audio.play().catch(e => console.warn("Звук не воспроизвелся (нужно взаимодействие со страницей):", e));

        // 2. Системное (Native) уведомление браузера
        if (Notification.permission === "granted") {
            new Notification(data.title, {
                body: data.message,
                icon: '/favicon.ico' // можно заменить на свою иконку
            });
        }

        // 3. Добавляем в список в боковой панели
        addNotificationToSidebar(data);

        // 4. Обновляем счетчик на колокольчике
        updateBadgeCount();
    });

    connection.start().then(function () {
        console.log(">>>SignalR подключен к Notifications!");
    }).catch(function (err) {
        console.error(">>>Ошибка SignalR: " + err.toString());
    });
}

function updateBadgeCount() {
    const badge = document.getElementById("notificationBadge");
    if (badge) {
        let count = parseInt(badge.innerText) || 0;
        badge.innerText = count + 1;
        badge.style.display = "inline-block";
    }
}

function addNotificationToSidebar(data) {
    const list = document.getElementById("notificationList");
    if (!list) return;

    // Убираем заглушку "Нет новых уведомлений", если она есть
    if (list.querySelector('p.text-muted')) {
        list.innerHTML = '';
    }

    const html = `
        <div class="notification-item p-3 border-bottom shadow-sm mb-2 bg-light rounded" style="cursor: pointer;" onclick="location.href='${data.url || '#'}'">
            <div class="d-flex justify-content-between align-items-start">
                <strong class="text-primary" style="font-size: 0.9rem;">${data.title}</strong>
                <small class="text-muted" style="font-size: 0.7rem;">сейчас</small>
            </div>
            <div style="font-size: 0.85rem;" class="mt-1">${data.message}</div>
        </div>
    `;

    list.insertAdjacentHTML('afterbegin', html);
}