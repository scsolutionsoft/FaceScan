(() => {
    function pad2(value) {
        return String(value).padStart(2, "0");
    }

    function toThaiDisplay(isoValue) {
        const normalizedValue = normalizeIsoValue(isoValue);
        if (!normalizedValue) {
            return "";
        }

        const match = normalizedValue.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (!match) {
            return "";
        }

        const year = Number(match[1]) + 543;
        return `${match[3]}/${match[2]}/${year}`;
    }

    function toIsoValue(thaiDisplay) {
        const value = (thaiDisplay || "").trim();
        if (!value) {
            return "";
        }

        const match = value.match(/^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/);
        if (!match) {
            return null;
        }

        const day = Number(match[1]);
        const month = Number(match[2]);
        let year = Number(match[3]);

        if (year >= 2400) {
            year -= 543;
        }

        if (month < 1 || month > 12 || day < 1 || day > 31) {
            return null;
        }

        const parsed = new Date(year, month - 1, day);
        if (
            parsed.getFullYear() !== year ||
            parsed.getMonth() !== month - 1 ||
            parsed.getDate() !== day
        ) {
            return null;
        }

        return `${String(year).padStart(4, "0")}-${pad2(month)}-${pad2(day)}`;
    }

    function normalizeIsoValue(isoValue) {
        if (!isoValue) {
            return "";
        }

        const match = isoValue.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (!match) {
            return isoValue;
        }

        let year = Number(match[1]);
        if (year >= 2400) {
            year -= 543;
        }

        return `${String(year).padStart(4, "0")}-${match[2]}-${match[3]}`;
    }

    function toComparableDate(isoValue) {
        const normalizedValue = normalizeIsoValue(isoValue);
        if (!normalizedValue) {
            return null;
        }

        const match = normalizedValue.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (!match) {
            return null;
        }

        return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
    }

    function isInRange(sourceInput, isoValue) {
        const selected = toComparableDate(isoValue);
        if (!selected) {
            return false;
        }

        const minDate = toComparableDate(sourceInput.min);
        if (minDate && selected < minDate) {
            return false;
        }

        const maxDate = toComparableDate(sourceInput.max);
        if (maxDate && selected > maxDate) {
            return false;
        }

        return true;
    }

    function enhanceThaiDateInput(sourceInput) {
        if (sourceInput.dataset.thaiDateEnhanced === "1") {
            return;
        }

        sourceInput.dataset.thaiDateEnhanced = "1";
        sourceInput.classList.add("thai-date-native");

        const wrapper = document.createElement("div");
        wrapper.className = "thai-date-wrapper";
        sourceInput.parentNode.insertBefore(wrapper, sourceInput);
        wrapper.appendChild(sourceInput);

        const displayInput = document.createElement("input");
        displayInput.type = "text";
        const sourceClasses = Array.from(sourceInput.classList)
            .filter(className => className !== "thai-date-native");
        displayInput.className = sourceClasses.join(" ");
        if (!displayInput.classList.contains("form-control")) {
            displayInput.classList.add("form-control");
        }
        displayInput.classList.add("thai-date-display");
        displayInput.placeholder = "วว/ดด/ปปปป";
        displayInput.inputMode = "numeric";
        displayInput.autocomplete = "off";
        displayInput.disabled = sourceInput.disabled;
        displayInput.readOnly = sourceInput.readOnly;
        if (sourceInput.required) {
            displayInput.required = true;
        }
        wrapper.appendChild(displayInput);

        const pickerButton = document.createElement("button");
        pickerButton.type = "button";
        pickerButton.className = "btn btn-outline-secondary thai-date-picker-btn";
        pickerButton.setAttribute("aria-label", "เปิดปฏิทิน");
        pickerButton.innerHTML = "<i class=\"bi bi-calendar3\"></i>";
        pickerButton.disabled = sourceInput.disabled;
        wrapper.appendChild(pickerButton);

        function syncDisplayFromSource() {
            const normalizedValue = normalizeIsoValue(sourceInput.value);
            if (normalizedValue && normalizedValue !== sourceInput.value) {
                sourceInput.value = normalizedValue;
            }

            displayInput.value = toThaiDisplay(normalizedValue);
            displayInput.setCustomValidity("");
        }

        function syncSourceFromDisplay() {
            const trimmed = (displayInput.value || "").trim();
            if (!trimmed) {
                sourceInput.value = "";
                displayInput.setCustomValidity("");
                return true;
            }

            const isoValue = toIsoValue(trimmed);
            if (!isoValue || !isInRange(sourceInput, isoValue)) {
                displayInput.setCustomValidity("กรุณาระบุวันที่ในรูปแบบ วว/ดด/ปปปป (พ.ศ.)");
                return false;
            }

            sourceInput.value = isoValue;
            sourceInput.dispatchEvent(new Event("change", { bubbles: true }));
            displayInput.setCustomValidity("");
            return true;
        }

        function openNativePicker() {
            if (typeof sourceInput.showPicker === "function") {
                sourceInput.showPicker();
                return;
            }

            sourceInput.focus();
        }

        sourceInput.addEventListener("change", syncDisplayFromSource);
        sourceInput.addEventListener("input", syncDisplayFromSource);

        displayInput.addEventListener("blur", () => {
            if (!syncSourceFromDisplay()) {
                displayInput.reportValidity();
            }
            syncDisplayFromSource();
        });

        displayInput.addEventListener("keydown", (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                displayInput.blur();
            }
        });

        displayInput.addEventListener("click", openNativePicker);
        pickerButton.addEventListener("click", openNativePicker);

        const form = sourceInput.closest("form");
        if (form) {
            form.addEventListener("submit", (event) => {
                if (!syncSourceFromDisplay()) {
                    event.preventDefault();
                    displayInput.reportValidity();
                }
            });
        }

        syncDisplayFromSource();
    }

    function initThaiDateInputs() {
        document.querySelectorAll("input[type=\"date\"]").forEach(enhanceThaiDateInput);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initThaiDateInputs);
    } else {
        initThaiDateInputs();
    }
})();
