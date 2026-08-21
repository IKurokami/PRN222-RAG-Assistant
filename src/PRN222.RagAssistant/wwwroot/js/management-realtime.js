(() => {
    const marker = document.querySelector('[data-management-realtime]');
    if (!marker || !window.signalR) {
        return;
    }

    const scope = marker.dataset.realtimeScope;
    const subjectId = marker.dataset.subjectId;
    const subjectIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    const subjectScope = scope === 'subject';

    if (!['subject', 'admin-users', 'admin-subjects', 'subject-catalog'].includes(scope)
        || (subjectScope && !subjectIdPattern.test(subjectId ?? ''))) {
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/management')
        .withAutomaticReconnect()
        .build();

    let startInFlight = false;
    let retryTimer = null;
    let reloadScheduled = false;

    const subscribe = async () => {
        if (connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        try {
            switch (scope) {
                case 'subject':
                    await connection.invoke('SubscribeToSubject', subjectId);
                    break;
                case 'admin-users':
                    await connection.invoke('SubscribeToAdminUsers');
                    break;
                case 'admin-subjects':
                    await connection.invoke('SubscribeToAdminSubjects');
                    break;
                case 'subject-catalog':
                    await connection.invoke('SubscribeToSubjectCatalog');
                    break;
            }
        } catch (error) {
            console.error('Management realtime subscription failed.', error);
        }
    };

    const scheduleStart = (delay = 5000) => {
        if (retryTimer !== null) {
            return;
        }

        retryTimer = window.setTimeout(() => {
            retryTimer = null;
            void startConnection();
        }, delay);
    };

    const startConnection = async () => {
        if (startInFlight || connection.state !== signalR.HubConnectionState.Disconnected) {
            return;
        }

        startInFlight = true;
        try {
            await connection.start();
            await subscribe();
        } catch (error) {
            console.error('Management realtime connection failed.', error);
            scheduleStart();
        } finally {
            startInFlight = false;
        }
    };

    const reloadPage = () => {
        if (reloadScheduled) {
            return;
        }

        reloadScheduled = true;
        window.setTimeout(() => {
            window.location.reload();
        }, 0);
    };

    connection.on('ManagementChanged', reloadPage);
    connection.onreconnected(() => {
        void subscribe();
    });
    connection.onclose(() => {
        scheduleStart();
    });

    void startConnection();
})();
