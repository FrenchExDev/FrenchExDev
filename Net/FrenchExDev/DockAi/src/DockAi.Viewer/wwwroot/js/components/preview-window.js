/**
 * PreviewWindow: floating, draggable, stackable document preview windows.
 */
export class PreviewWindowManager {
    constructor(layerEl, api) {
        this.layer = layerEl;
        this.api = api;
        this.windows = new Map();
        this.nextZ = 1000;
        this.nextOffset = 0;

        // Global mouse handlers for dragging
        this._dragging = null;
        document.addEventListener('mousemove', (e) => this._onMouseMove(e));
        document.addEventListener('mouseup', () => this._onMouseUp());
    }

    async open(docId, docName) {
        // Don't reopen if already open
        if (this.windows.has(docId)) {
            this.focus(docId);
            return;
        }

        const id = docId;
        const offset = (this.nextOffset % 8) * 30;
        this.nextOffset++;

        const win = document.createElement('div');
        win.className = 'pw-window';
        win.dataset.id = id;
        win.style.cssText = `left:${200 + offset}px;top:${80 + offset}px;width:600px;height:450px;z-index:${this.nextZ++}`;

        win.innerHTML = `
            <div class="pw-titlebar">
                <span class="pw-title">${this._esc(docName || docId)}</span>
                <div class="pw-controls">
                    <button class="pw-btn pw-maximize">\u2610</button>
                    <button class="pw-btn pw-close">\u2715</button>
                </div>
            </div>
            <div class="pw-body"><div class="loading">Loading...</div></div>
            <div class="pw-footer">
                <a href="${this.api.getRawUrl(docId)}" class="pw-download" download>Download</a>
            </div>`;

        this.layer.appendChild(win);
        this.windows.set(id, { el: win, maximized: false, savedRect: null });

        // Events
        win.querySelector('.pw-close').onclick = () => this.close(id);
        win.querySelector('.pw-maximize').onclick = () => this.toggleMaximize(id);
        win.querySelector('.pw-titlebar').addEventListener('mousedown', (e) => {
            if (e.target.closest('.pw-btn')) return;
            this.focus(id);
            this._dragging = { id, startX: e.clientX, startY: e.clientY, origLeft: win.offsetLeft, origTop: win.offsetTop };
        });
        win.addEventListener('mousedown', () => this.focus(id));

        // Load content
        const rendered = await this.api.renderDocument(docId);
        const body = win.querySelector('.pw-body');
        if (rendered) {
            if (rendered.renderMode === 'pdf-embed') {
                body.innerHTML = `<embed src="${this.api.getRawUrl(docId)}" type="application/pdf" style="width:100%;height:100%">`;
            } else {
                body.innerHTML = rendered.html;
            }
        } else {
            body.innerHTML = '<div class="error">Could not render</div>';
        }
    }

    close(id) {
        const w = this.windows.get(id);
        if (!w) return;
        w.el.remove();
        this.windows.delete(id);
    }

    closeAll() {
        for (const [id] of this.windows) this.close(id);
    }

    focus(id) {
        const w = this.windows.get(id);
        if (!w) return;
        w.el.style.zIndex = this.nextZ++;
        this.windows.forEach((v, k) => v.el.classList.toggle('pw-focused', k === id));
    }

    toggleMaximize(id) {
        const w = this.windows.get(id);
        if (!w) return;
        if (w.maximized) {
            const r = w.savedRect;
            w.el.style.cssText = `left:${r.left}px;top:${r.top}px;width:${r.width}px;height:${r.height}px;z-index:${w.el.style.zIndex}`;
            w.maximized = false;
        } else {
            w.savedRect = { left: w.el.offsetLeft, top: w.el.offsetTop, width: w.el.offsetWidth, height: w.el.offsetHeight };
            w.el.style.cssText = `left:10px;top:10px;width:calc(100vw - 20px);height:calc(100vh - 20px);z-index:${this.nextZ++}`;
            w.maximized = true;
        }
    }

    _onMouseMove(e) {
        if (!this._dragging) return;
        const d = this._dragging;
        const w = this.windows.get(d.id);
        if (!w) return;
        w.el.style.left = Math.max(0, d.origLeft + e.clientX - d.startX) + 'px';
        w.el.style.top = Math.max(0, d.origTop + e.clientY - d.startY) + 'px';
        if (w.maximized) w.maximized = false;
    }

    _onMouseUp() {
        this._dragging = null;
    }

    _esc(s) {
        const d = document.createElement('div');
        d.textContent = s || '';
        return d.innerHTML;
    }
}
