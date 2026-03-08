/**
 * TimelineView: vis-timeline chronological document view.
 * Loaded dynamically on first activation.
 */
export class TimelineView {
    constructor(containerEl, api) {
        this.el = containerEl;
        this.api = api;
        this.timeline = null;
        this.loaded = false;
        this.onDocSelect = null;
    }

    async show(onDocSelect) {
        this.onDocSelect = onDocSelect;
        this.el.style.display = '';

        if (!this.loaded) {
            this.el.innerHTML = '<div class="loading">Loading timeline library...</div>';
            await this._loadVisTimeline();
            this.loaded = true;
        }

        this.el.innerHTML = `
            <div class="timeline-controls">
                <button class="tl-btn" data-action="fit">Fit All</button>
                <button class="tl-btn" data-action="zoomin">+</button>
                <button class="tl-btn" data-action="zoomout">-</button>
            </div>
            <div class="timeline-canvas" style="width:100%;height:calc(100% - 40px)"></div>`;

        await this.buildTimeline();

        // Control buttons
        this.el.querySelectorAll('.tl-btn').forEach(btn => {
            btn.onclick = () => {
                if (!this.timeline) return;
                const a = btn.dataset.action;
                if (a === 'fit') this.timeline.fit();
                else if (a === 'zoomin') this.timeline.zoomIn(0.3);
                else if (a === 'zoomout') this.timeline.zoomOut(0.3);
            };
        });
    }

    hide() {
        this.el.style.display = 'none';
    }

    async buildTimeline() {
        const trackTax = await this.api.getTaxonomy('track');
        const searchResult = await this.api.search('', 200);
        const docs = searchResult.results || [];

        // Groups = legal tracks
        const trackColors = {};
        const groups = [];
        if (trackTax?.nodes) {
            for (const node of trackTax.nodes) {
                if (node.parentId) continue; // Only root nodes
                groups.push({ id: node.id, content: node.label, style: `color:${node.color || '#888'}` });
                trackColors[node.id] = node.color || '#888';
            }
        }
        if (groups.length === 0) groups.push({ id: 'default', content: 'Documents' });

        // Items = documents
        const items = [];
        for (const doc of docs) {
            if (!doc.date) continue;
            const track = (doc.tracks && doc.tracks[0]) || 'default';
            const color = trackColors[track] || '#888';
            items.push({
                id: doc.id,
                group: track,
                content: `<span style="color:${color}">${this._esc(doc.name)}</span>`,
                start: doc.date,
                title: `${doc.name}\n${doc.date}`,
                style: `border-color:${color};background-color:${color}22`
            });
        }

        const canvas = this.el.querySelector('.timeline-canvas');
        if (!canvas || typeof vis === 'undefined') return;

        this.timeline = new vis.Timeline(canvas, new vis.DataSet(items), new vis.DataSet(groups), {
            stack: true,
            showCurrentTime: true,
            zoomMin: 1000 * 60 * 60 * 24 * 7,
            zoomMax: 1000 * 60 * 60 * 24 * 365 * 15,
            orientation: { axis: 'top' }
        });

        this.timeline.on('select', (props) => {
            if (props.items.length > 0 && this.onDocSelect)
                this.onDocSelect(props.items[0]);
        });

        this.timeline.fit();
    }

    _esc(s) {
        const d = document.createElement('div');
        d.textContent = s || '';
        return d.innerHTML;
    }

    async _loadVisTimeline() {
        if (typeof vis !== 'undefined' && vis.Timeline) return;
        return new Promise((resolve, reject) => {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://cdn.jsdelivr.net/npm/vis-timeline@7.7.3/dist/vis-timeline-graph2d.min.css';
            document.head.appendChild(link);

            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/vis-timeline@7.7.3/dist/vis-timeline-graph2d.min.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }
}
