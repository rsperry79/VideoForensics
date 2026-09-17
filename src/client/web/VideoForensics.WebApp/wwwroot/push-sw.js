// Minimal service worker for handling web push notifications.
self.addEventListener('push', (event) => {
    let data;
    try {
        data = event.data.json();
    } catch (e) {
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

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    event.waitUntil(clients.openWindow('/'));
});
