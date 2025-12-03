// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

; (() => {
    // --- Core API --------------------------------------------------------------
    const el = document.getElementById('app-loader');
    let visibleCount = 0;         // support nested show/hide
    let showTimer = null;         // throttle: avoid flicker for very fast ops
    const THROTTLE_MS = 120;      // wait before showing (tweak as you like)

    function _apply() {
        if (!el) return;
        if (visibleCount > 0) {
            el.classList.add('is-visible');
            el.setAttribute('aria-hidden', 'false');
        } else {
            el.classList.remove('is-visible');
            el.setAttribute('aria-hidden', 'true');
        }
    }

    const Loader = {
        show() {
            // throttle to avoid flashing loader for sub-100ms calls
            if (showTimer) return;
            showTimer = setTimeout(() => {
                visibleCount++;
                _apply();
                clearTimeout(showTimer);
                showTimer = null;
            }, THROTTLE_MS);
        },
        hide(force = false) {
            if (showTimer) { clearTimeout(showTimer); showTimer = null; }
            if (force) { visibleCount = 0; _apply(); return; }
            visibleCount = Math.max(0, visibleCount - 1);
            _apply();
        },
        reset() { visibleCount = 0; _apply(); }
    };

    // Expose globally
    window.AppLoader = Loader;

    // --- jQuery AJAX integration (if jQuery present) --------------------------
    if (window.jQuery) {
        const $ = window.jQuery;

        // Only show loader for "global" AJAX (skip if opts.global=false)
        $(document).ajaxStart(() => Loader.show());
        $(document).ajaxStop(() => Loader.hide(true)); // force in case of mismatch
        $(document).ajaxError(() => Loader.hide(true));
    }

    // --- Native fetch integration ---------------------------------------------
    if (!window._originalFetch) {
        window._originalFetch = window.fetch ? window.fetch.bind(window) : null;
        if (window._originalFetch) {
            window.fetch = async function wrappedFetch(input, init) {
                // allow opt-out: { loader: false } in init
                const noLoader = init && (init.loader === false);
                if (!noLoader) Loader.show();

                try {
                    const res = await window._originalFetch(input, init);
                    return res;
                } finally {
                    if (!noLoader) Loader.hide();
                }
            };
        }
    }

    // --- Full page navigation hooks -------------------------------------------
    // Show on navigations triggered by links/forms
    window.addEventListener('beforeunload', () => {
        // Avoid showing when the page is already cached by bfcache
        // This still improves perceived responsiveness when leaving the page
        Loader.show();
    });

    // When coming back via bfcache, make sure loader is hidden
    window.addEventListener('pageshow', (e) => {
        if (e.persisted) Loader.reset();
    });

    // Optional: auto wrap form submits (progress on same page posts)
    document.addEventListener('submit', (e) => {
        const form = e.target;
        if (form && !form.hasAttribute('data-no-loader')) {
            Loader.show();
            // If it’s an AJAX form you handle manually, remember to call Loader.hide()
            // in your success/error callbacks.
        }
    });

    // --- Helpers for button-level spinner (optional) --------------------------
    window.withButtonLoading = async function (btn, fn) {
        try {
            if (btn) btn.classList.add('btn-loading');
            AppLoader.show();
            return await fn();
        } finally {
            if (btn) btn.classList.remove('btn-loading');
            AppLoader.hide();
        }
    };
})();