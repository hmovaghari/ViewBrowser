let vpnSession = null;

document.getElementById('connectBtn').addEventListener('click', async () => {
    try {
        const response = await fetch('/api/proxy/connect', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify('user-' + Math.random().toString(36).substr(2, 9))
        });
        
        const data = await response.json();
        vpnSession = data;
        
        document.getElementById('connectionStatus').textContent = 'Connected';
        document.getElementById('connectionStatus').className = 'badge bg-success';
        document.getElementById('sessionInfo').textContent = `Session: ${data.sessionId.substr(0, 8)}...`;
        document.getElementById('connectBtn').disabled = true;
        document.getElementById('disconnectBtn').disabled = false;
        document.getElementById('sendBtn').disabled = false;
        
        console.log('VPN Connected:', data);
    } catch (error) {
        alert('Connection failed: ' + error.message);
    }
});

document.getElementById('disconnectBtn').addEventListener('click', async () => {
    try {
        await fetch('/api/proxy/disconnect', {
            method: 'POST',
            headers: { 'X-VPN-Session': vpnSession.sessionId }
        });
        
        vpnSession = null;
        document.getElementById('connectionStatus').textContent = 'Disconnected';
        document.getElementById('connectionStatus').className = 'badge bg-secondary';
        document.getElementById('sessionInfo').textContent = '';
        document.getElementById('connectBtn').disabled = false;
        document.getElementById('disconnectBtn').disabled = true;
        document.getElementById('sendBtn').disabled = true;
    } catch (error) {
        alert('Disconnection failed: ' + error.message);
    }
});

document.getElementById('sendBtn').addEventListener('click', async () => {
    const targetUrl = document.getElementById('targetUrl').value;
    const method = document.getElementById('requestMethod').value;
    const useEncryption = document.getElementById('useEncryption').checked;
    const useCache = document.getElementById('useCache').checked;
    
    if (!targetUrl) {
        alert('Please enter a target URL');
        return;
    }
    
    try {
        document.getElementById('sendBtn').disabled = true;
        document.getElementById('sendBtn').textContent = '⏳ Loading...';
        
        const response = await fetch('/api/proxy/forward', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-VPN-Session': vpnSession.sessionId
            },
            body: JSON.stringify({
                targetUrl: targetUrl,
                method: method,
                useEncryption: useEncryption,
                useCache: useCache
            })
        });
        
        const data = await response.json();
        
        document.getElementById('responseStatus').textContent = data.statusCode;
        document.getElementById('responseTime').textContent = data.responseTime + ' ms';
        document.getElementById('fromCache').textContent = data.fromCache ? 'Yes ✅' : 'No ❌';
        document.getElementById('responseContent').textContent = data.content;
        document.getElementById('responseCard').style.display = 'block';
        
    } catch (error) {
        alert('Request failed: ' + error.message);
    } finally {
        document.getElementById('sendBtn').disabled = false;
        document.getElementById('sendBtn').textContent = '🚀 Send Request';
    }
});