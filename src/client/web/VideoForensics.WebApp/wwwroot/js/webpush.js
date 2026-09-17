// Web Push API interop for VideoForensics - mirrors webauthn.js IIFE pattern.
// Exposes window.vfWebPush with async methods for push subscription management.

window.vfWebPush = (() => {
    // Helper: convert base64url string to Uint8Array for VAPID public key
    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding)
            .replace(/\-/g, '+')
            .replace(/_/g, '/');

        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);
        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    return {
        /**
         * Check if the browser supports Web Push API.
         * @returns {boolean} True if push API is available
         */
        isSupported: async () => {
            return 'serviceWorker' in navigator && 'PushManager' in window;
        },

        /**
         * Get the current notification permission state.
         * @returns {string} "granted", "denied", or "default"
         */
        getPermissionState: async () => {
            return Notification.permission;
        },

        /**
         * Subscribe to push notifications.
         * @param {string} vapidPublicKeyBase64 - Base64url-encoded VAPID public key
         * @returns {object} { endpoint, p256dhKey, authKey }
         * @throws {Error} If permission denied or browser doesn't support push
         */
        subscribe: async (vapidPublicKeyBase64) => {
            // Register the service worker
            const registration = await navigator.serviceWorker.register('/push-sw.js');

            // Request notification permission if needed
            if (Notification.permission === 'default') {
                const permission = await Notification.requestPermission();
                if (permission !== 'granted') {
                    throw new Error('Notification permission denied');
                }
            } else if (Notification.permission === 'denied') {
                throw new Error('Notification permission denied by user');
            }

            // Subscribe to push manager
            const subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(vapidPublicKeyBase64)
            });

            // Extract subscription details
            const subJson = subscription.toJSON();
            return {
                endpoint: subscription.endpoint,
                p256dhKey: subJson.keys.p256dh,
                authKey: subJson.keys.auth
            };
        },

        /**
         * Unsubscribe from push notifications.
         * @returns {string|null} The endpoint that was unsubscribed, or null if no subscription
         */
        unsubscribe: async () => {
            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();

            if (!subscription) {
                return null;
            }

            const endpoint = subscription.endpoint;
            await subscription.unsubscribe();
            return endpoint;
        },

        /**
         * Get the current subscription endpoint without re-subscribing.
         * @returns {string|null} The current subscription endpoint, or null if none
         */
        getCurrentSubscriptionEndpoint: async () => {
            try {
                const registration = await navigator.serviceWorker.ready;
                const subscription = await registration.pushManager.getSubscription();
                return subscription ? subscription.endpoint : null;
            } catch (e) {
                return null;
            }
        }
    };
})();
