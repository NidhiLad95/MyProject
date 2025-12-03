// ====== JWT helpers ======
(function () {
    let refreshTimer = null;

    window.saveTokens= function (data) {
        if (!data || !data.accessToken) return;
        localStorage.setItem('accessToken', data.accessToken);
        if (data.refreshToken) localStorage.setItem('refreshToken', data.refreshToken);
        if (data.expiresUtc) localStorage.setItem('accessTokenExpires', data.expiresUtc);
        scheduleRefresh(data.expiresUtc);
    }

    window.getAccessToken=function () {
        return localStorage.getItem('accessToken') || '';
    }

    function getRefreshToken() {
        return localStorage.getItem('refreshToken') || '';
    }

    async function refreshAccessToken() {
        const rt = getRefreshToken();
        if (!rt) throw new Error('No refresh token');
        const res = await fetch('/api/auth/refresh', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken: rt })
        });
        if (!res.ok) throw new Error('Refresh failed');
        const payload = await res.json(); // { accessToken, expiresUtc }
        saveTokens(payload);
        return payload.accessToken;
    }

    function scheduleRefresh(expiresUtc) {
        try { clearTimeout(refreshTimer); } catch (_) { }
        if (!expiresUtc) return;
        const msUntil = Date.parse(expiresUtc) - Date.now() - 60_000; // refresh 1 min early
        if (msUntil > 0) {
            refreshTimer = setTimeout(async () => {
                try { await refreshAccessToken(); } catch (e) { console.warn('Auto refresh failed', e); }
            }, msUntil);
        }
    }

    // ====== Attach token to every jQuery AJAX automatically ======
    if (window.jQuery) {
        debugger;
        $.ajaxPrefilter(function (options, originalOptions, jqXHR) {
            const t = getAccessToken();
            if (t) jqXHR.setRequestHeader('Authorization', 'Bearer ' + t);
        });

        // If a request returns 401, try one silent refresh and retry once
        $(document).ajaxError(async function (evt, jqXHR, settings) {
            if (jqXHR.status === 401 && !settings.__retried) {
                try {
                    await refreshAccessToken();
                    const newSettings = Object.assign({}, settings, { __retried: true });
                    $.ajax(newSettings);
                } catch (e) {
                    console.warn('401 and refresh failed; redirecting to login');
                    // Optional: window.location.href = '/Account/Login';
                }
            }
        });
    }

     // Expose a helper to log out (optional)
     window.jwtLogout = function () {
       try { clearTimeout(refreshTimer); } catch(_) {}
       localStorage.removeItem('accessToken');
       localStorage.removeItem('refreshToken');
       localStorage.removeItem('accessTokenExpires');
       localStorage.removeItem('groupId');
       localStorage.removeItem('lastConvId');
     };

     //// ====== Hook your existing login success to fetch tokens ======
     //// Call this AFTER your current login success (no need to change your backend login flow)
     //window.fetchAndStoreJwtAfterLogin = async function (email, password, extras) {
     //  // If your AuthController expects extra claims, pass them via `extras`
     //  const payload = Object.assign({
     //    email: email,
     //    password: password
     //  }, (extras || {}));

     //  const res = await fetch('/api/auth/login', {
     //    method: 'POST',
     //    headers: { 'Content-Type': 'application/json' },
     //    body: JSON.stringify(payload)
     //  });

     //  if (!res.ok) {
     //    const txt = await res.text();
     //    throw new Error('Token login failed: ' + txt);
     //  }
     //  const data = await res.json(); // { accessToken, refreshToken, expiresUtc }
     //  saveTokens(data);
     //  return data;
     //};
})();