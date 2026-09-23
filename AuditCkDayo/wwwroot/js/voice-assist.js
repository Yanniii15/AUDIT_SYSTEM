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
        // Replace ₱ symbol and pesos with "Philippine pesos" to prevent browser speech engines from defaulting to dollars
        const cleanText = text.trim()
            .replace(/₱/g, ' Philippine pesos ')
            .replace(/\bpesos\b/gi, 'Philippine pesos')
            .replace(/\bdollars?\b/gi, 'Philippine pesos');
        const utterance = new SpeechSynthesisUtterance(cleanText);
        utterance.lang = "en-PH";
        utterance.rate = 0.95;
        utterance.pitch = 1;
        const voices = window.speechSynthesis.getVoices();
        if (voices && voices.length > 0) {
            const phVoice = voices.find(v => v.lang === 'en-PH' || v.lang === 'fil-PH' || v.lang.startsWith('fil') || v.name.toLowerCase().includes('philippine'));
            if (phVoice) {
                utterance.voice = phVoice;
            }
        }
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
