window.getBrowserLanguage = function() {
    return navigator.language || navigator.userLanguage || 'en';
};

window.setLanguageCookie = function(culture) {
    document.cookie = `language=${culture}; path=/; max-age=31536000; SameSite=Lax`;
};
