(function () {
    const storageKey = "auditckdayo.voiceAssist";
    const toggle = document.getElementById("voiceAssistToggle");
    const toggleText = document.getElementById("voiceAssistToggleText");
    const summary = document.getElementById("pageVoiceSummary");
    const status = document.getElementById("globalA11yStatus");

    function isSupported() {
        return "speechSynthesis" in window && "SpeechSynthesisUtterance" in window;
    }

    function isEnabled() {
        return localStorage.getItem(storageKey) === "on";
    }

    function setEnabled(enabled) {
        localStorage.setItem(storageKey, enabled ? "on" : "off");
        updateButton();
    }

    function speak(text) {
        if (!isSupported() || !isEnabled() || !text || !text.trim()) return;
        window.speechSynthesis.cancel();
        // Replace ₱ symbol with "pesos" to avoid browser speech engines misinterpreting it
        const cleanText = text.trim().replace(/₱/g, ' pesos ');
        const utterance = new SpeechSynthesisUtterance(cleanText);
        utterance.rate = 0.95;
        utterance.pitch = 1;
        window.speechSynthesis.speak(utterance);
    }

    function stop() {
        if (isSupported()) {
            window.speechSynthesis.cancel();
        }
    }

    function readPageSummary() {
        const text = summary?.textContent || document.title;
        speak(text);
    }

    function announce(text) {
        if (status) {
            status.textContent = text;
        }
        speak(text);
    }

    function updateButton() {
        if (!toggle || !toggleText) return;
        const enabled = isEnabled();
        toggle.setAttribute("aria-pressed", enabled ? "true" : "false");
        toggle.setAttribute("aria-label", enabled ? "Turn voice assist off" : "Turn voice assist on");
        toggleText.textContent = enabled ? "Voice Assist On" : "Voice Assist Off";
    }

    if (toggle) {
        toggle.addEventListener("click", function () {
            const next = !isEnabled();
            setEnabled(next);
            if (next) {
                readPageSummary();
            } else {
                stop();
            }
        });
    }

    window.auditVoiceAssist = { speak, stop, readPageSummary, announce };
    updateButton();

    // Auto-read on load if allowed and enabled
    if (isEnabled()) {
        setTimeout(readPageSummary, 800);
    }
})();
