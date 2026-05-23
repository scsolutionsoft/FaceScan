(function () {
    'use strict';

    function renderPipelineItem(el, state) {
        if (!el || !state) {
            return;
        }

        el.classList.remove('ok', 'warn', 'bad');
        el.classList.add(state.level || 'warn');
        var labelEl = el.querySelector('strong');
        if (labelEl) {
            labelEl.textContent = state.text || '-';
        }
    }

    function renderPipelineHealth(elements, healthState) {
        if (!elements || !healthState) {
            return;
        }

        renderPipelineItem(elements.videoHealth, healthState.video);
        renderPipelineItem(elements.detectHealth, healthState.detect);
        renderPipelineItem(elements.matchHealth, healthState.match);
    }

    function getMatchPipelineState(activeWorkers, queueLength, queueMax) {
        if (activeWorkers > 0 || queueLength > 0) {
            var threshold = Math.max(6, Math.floor((queueMax || 24) * 0.6));
            return {
                level: queueLength >= threshold ? 'bad' : 'warn',
                text: 'q:' + queueLength + ' w:' + activeWorkers
            };
        }

        return { level: 'ok', text: 'idle' };
    }

    window.IpCameraPageModule = {
        renderPipelineHealth: renderPipelineHealth,
        getMatchPipelineState: getMatchPipelineState
    };
})();
