let vpnSession = null;
let currentUrl = '';

// ????? DOM
const connectBtn = document.getElementById('connectBtn');
const disconnectBtn = document.getElementById('disconnectBtn');
const goBtn = document.getElementById('goBtn');
const refreshBtn = document.getElementById('refreshBtn');
const targetUrlInput = document.getElementById('targetUrl');
const contentFrame = document.getElementById('contentFrame');
const loadingOverlay = document.getElementById('loadingOverlay');
const connectionStatus = document.getElementById('connectionStatus');
const loadTime = document.getElementById('loadTime');
const cacheStatus = document.getElementById('cacheStatus');

// ????? VPN
connectBtn.addEventListener('click', async () => {
    try {
        showLoading(true);
        const response = await fetch('/api/proxy/connect', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify('user-' + Math.random().toString(36).substr(2, 9))
        });
        
        if (!response.ok) throw new Error('Connection failed');
        
        const data = await response.json();
        vpnSession = data;
        
        updateConnectionStatus(true);
        showNotification('? VPN Connected Successfully!', 'success');
        
        // ??? URL ????? ?????? ???????? ??
        if (targetUrlInput.value) {
            loadUrl(targetUrlInput.value);
        }
    } catch (error) {
        showNotification('? Connection failed: ' + error.message, 'error');
    } finally {
        showLoading(false);
    }
});

// ??? ?????
disconnectBtn.addEventListener('click', async () => {
    try {
        await fetch('/api/proxy/disconnect', {
            method: 'POST',
            headers: { 'X-VPN-Session': vpnSession.sessionId }
        });
        
        vpnSession = null;
        currentUrl = '';
        contentFrame.srcdoc = '';
        updateConnectionStatus(false);
        showNotification('VPN Disconnected', 'info');
    } catch (error) {
        showNotification('Disconnection error: ' + error.message, 'error');
    }
});

// ???????? URL
goBtn.addEventListener('click', () => {
    const url = targetUrlInput.value.trim();
    if (url) {
        loadUrl(url);
    }
});

// ?????????
refreshBtn.addEventListener('click', () => {
    if (currentUrl) {
        loadUrl(currentUrl, false);
    }
});

// Enter key ?? input
targetUrlInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter' && !goBtn.disabled) {
        goBtn.click();
    }
});

// ???? ???????? URL
async function loadUrl(url, useCache = true) {
    if (!vpnSession) {
        showNotification('Please connect to VPN first', 'warning');
        return;
    }

    // ????? ???? https ??? ?????
    if (!url.startsWith('http://') && !url.startsWith('https://')) {
        url = 'https://' + url;
    }

    try {
        showLoading(true);
        loadTime.textContent = '';
        cacheStatus.textContent = '';
        
        const startTime = Date.now();
        
        const response = await fetch('/api/proxy/forward', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-VPN-Session': vpnSession.sessionId
            },
            body: JSON.stringify({
                targetUrl: url,
                method: 'GET',
                useCache: useCache,
                rewriteUrls: true,
                followRedirects: true
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();
        
        if (data.error) {
            throw new Error(data.error);
        }

        // ??????????? ???????
        const endTime = Date.now();
        loadTime.textContent = `?? ${data.responseTime}ms`;
        cacheStatus.textContent = data.fromCache ? '?? Cached' : '?? Live';
        
        // ????? ?????
        if (data.isHtml && data.content) {
            contentFrame.srcdoc = data.content;
            currentUrl = url;
            targetUrlInput.value = url;
        } else {
            contentFrame.srcdoc = `
                <html>
                    <body style="font-family: Arial; padding: 20px;">
                        <h3>Non-HTML Content</h3>
                        <p><strong>Content-Type:</strong> ${data.contentType}</p>
                        <p><strong>Status:</strong> ${data.statusCode}</p>
                        <pre style="background: #f5f5f5; padding: 15px; border-radius: 5px; overflow: auto;">${escapeHtml(data.content)}</pre>
                    </body>
                </html>
            `;
        }
        
        showNotification('? Page loaded successfully!', 'success');
    } catch (error) {
        showNotification('? Error: ' + error.message, 'error');
        contentFrame.srcdoc = `
            <html>
                <body style="font-family: Arial; padding: 40px; text-align: center;">
                    <h1 style="color: #dc3545;">?? Error Loading Page</h1>
                    <p style="color: #666; font-size: 18px;">${escapeHtml(error.message)}</p>
                    <button onclick="window.parent.location.reload()" style="padding: 10px 20px; font-size: 16px; cursor: pointer;">
                        Try Again
                    </button>
                </body>
            </html>
        `;
    } finally {
        showLoading(false);
    }
}

// ??????????? ????? ?????
function updateConnectionStatus(connected) {
    if (connected) {
        connectionStatus.textContent = '?? Connected';
        connectionStatus.className = 'badge bg-success';
        connectBtn.disabled = true;
        disconnectBtn.disabled = false;
        goBtn.disabled = false;
        refreshBtn.disabled = false;
    } else {
        connectionStatus.textContent = '?? Disconnected';
        connectionStatus.className = 'badge bg-secondary';
        connectBtn.disabled = false;
        disconnectBtn.disabled = true;
        goBtn.disabled = true;
        refreshBtn.disabled = true;
    }
}

// ?????/???? ???? loading
function showLoading(show) {
    loadingOverlay.style.display = show ? 'flex' : 'none';
}

// ????? ??????????
function showNotification(message, type) {
    const colors = {
        success: '#28a745',
        error: '#dc3545',
        warning: '#ffc107',
        info: '#17a2b8'
    };
    
    const notification = document.createElement('div');
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: ${colors[type] || colors.info};
        color: white;
        padding: 15px 25px;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        z-index: 10000;
        animation: slideIn 0.3s ease-out;
        max-width: 400px;
    `;
    notification.textContent = message;
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.style.animation = 'slideOut 0.3s ease-in';
        setTimeout(() => notification.remove(), 300);
    }, 3000);
}

// Escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ????? ???? ??????????
const style = document.createElement('style');
style.textContent = `
    @keyframes slideIn {
        from { transform: translateX(400px); opacity: 0; }
        to { transform: translateX(0); opacity: 1; }
    }
    @keyframes slideOut {
        from { transform: translateX(0); opacity: 1; }
        to { transform: translateX(400px); opacity: 0; }
    }
`;
document.head.appendChild(style);

// ????? URL ?????
window.addEventListener('load', () => {
    if (targetUrlInput.value) {
        connectBtn.click();
    }
});