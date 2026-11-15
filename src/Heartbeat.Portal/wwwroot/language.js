window.heartbeatLanguage = {
    set(culture) {
        document.cookie = `.AspNetCore.Culture=c=${culture}|uic=${culture}; path=/; max-age=31536000; samesite=lax`;
        window.location.reload();
    }
};
