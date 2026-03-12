window.sigilHighlightCode = function (code, language) {
    try {
        const r = language && hljs.getLanguage(language)
            ? hljs.highlight(code, { language, ignoreIllegals: true })
            : hljs.highlightAuto(code);
        return r.value;
    } catch { return hljs.highlightAuto(code).value; }
};
