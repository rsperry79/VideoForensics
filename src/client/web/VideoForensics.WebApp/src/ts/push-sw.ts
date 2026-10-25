// Minimal service worker for handling web push notifications.
// Compiled to wwwroot/push-sw.js (not wwwroot/scripts/) so its scope stays at the site root;
// webpush.ts registers it at '/push-sw.js'.

// The WebWorker lib types `self` as a plain worker scope, which lacks the service-worker-only
// members used below. Declared locally rather than pulling in @types/serviceworker for one file.
interface ServiceWorkerScope {
    registration: ServiceWorkerRegistration;
    clients: Clients;
    addEventListener(type: "push", listener: (event: PushEvent) => void): void;
    addEventListener(type: "notificationclick", listener: (event: NotificationEvent) => void): void;
}

const sw = self as unknown as ServiceWorkerScope;

sw.addEventListener('push', (event) => {
    let data: { title?: string; body?: string };
    try {
        data = event.data!.json();
    } catch (e) {
        // If JSON parsing fails, treat the push data as plain text
        data = {
            title: 'Video Forensics Notification',
            body: event.data ? event.data.text() : 'You have a new notification'
        };
    }

    const title = data.title || 'Video Forensics';
    const options: NotificationOptions = {
        body: data.body || 'You have a new notification'
    };

    event.waitUntil(sw.registration.showNotification(title, options));
});

sw.addEventListener('notificationclick', (event) => {
    event.notification.close();
    event.waitUntil(sw.clients.openWindow('/'));
});
