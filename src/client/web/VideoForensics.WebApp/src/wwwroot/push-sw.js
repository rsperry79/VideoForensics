"use strict";
// Minimal service worker for handling web push notifications.
// Compiled to wwwroot/push-sw.js (not wwwroot/scripts/) so its scope stays at the site root;
// webpush.ts registers it at '/push-sw.js'.
const sw = self;
sw.addEventListener('push', (event) => {
    let data;
    try {
        data = event.data.json();
    }
    catch {
        // If JSON parsing fails, treat the push data as plain text
        data = {
            title: 'Video Forensics Notification',
            body: event.data ? event.data.text() : 'You have a new notification'
        };
    }
    const title = data.title || 'Video Forensics';
    const options = {
        body: data.body || 'You have a new notification'
    };
    event.waitUntil(sw.registration.showNotification(title, options));
});
sw.addEventListener('notificationclick', (event) => {
    event.notification.close();
    event.waitUntil(sw.clients.openWindow('/'));
});
