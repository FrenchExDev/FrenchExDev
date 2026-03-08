/**
 * DocumentView: renders a document in the main content area with metadata header.
 */
export class DocumentView {
    constructor(containerEl, api) {
        this.el = containerEl;
        this.api = api;
        this.currentDoc = null;
        this.zoom = 100;
    }

    async show(docId) {
        this.el.innerHTML = '<div class="loading">Loading...</div>';

        const [meta, rendered] = await Promise.all([
            this.api.getDocument(docId),
            this.api.renderDocument(docId)
        ]);

        if (!meta) {
            this.el.innerHTML = '<div class="error">Document not found</div>';
            return;
        }

        this.currentDoc = meta;
        this.zoom = 100;

        let html = '<div class="doc-view">';

        // Header
        html += `<div class="doc-header">
            <h2 class="doc-title">${this.esc(meta.name)}</h2>
            <div class="doc-meta">
                <span class="doc-meta-type">${this.esc(meta.fileType?.toUpperCase())}</span>
                <span class="doc-meta-size">${this.formatSize(meta.fileSize)}</span>
                <span class="doc-meta-date">${this.formatDate(meta.lastModified)}</span>
                <a href="${this.api.getRawUrl(docId)}" class="doc-download" download>Download</a>
            </div>`;

        // Entity pills
        if (meta.entities && meta.entities.length > 0) {
            html += '<div class="doc-pills">';
            for (const e of meta.entities)
                html += `<span class="pill pill-entity">${this.esc(e)}</span>`;
            html += '</div>';
        }

        // Taxonomy pills
        if (meta.taxonomyAssignments) {
            html += '<div class="doc-pills">';
            for (const [tax, nodes] of Object.entries(meta.taxonomyAssignments)) {
                for (const n of nodes)
                    html += `<span class="pill pill-${tax}">${this.esc(n)}</span>`;
            }
            html += '</div>';
        }

        html += '</div>'; // end doc-header

        // Zoom controls
        html += `<div class="doc-zoom">
            <button class="zoom-btn" data-zoom="-10">-</button>
            <span class="zoom-level">${this.zoom}%</span>
            <button class="zoom-btn" data-zoom="+10">+</button>
            <button class="zoom-btn" data-zoom="reset">Reset</button>
        </div>`;

        // Rendered content
        html += '<div class="doc-content-wrapper">';
        if (rendered) {
            if (rendered.renderMode === 'pdf-embed') {
                html += `<embed src="${this.api.getRawUrl(docId)}" type="application/pdf" class="doc-pdf-embed">`;
            } else {
                html += `<div class="doc-content" style="transform:scale(1);transform-origin:top left">${rendered.html}</div>`;
            }

            // XLSX sheet tabs
            if (rendered.metadata?.sheets) {
                html += '<script type="module">document.querySelectorAll(".xlsx-tab").forEach(btn=>{btn.onclick=()=>{const i=btn.dataset.sheet;document.querySelectorAll(".xlsx-tab").forEach(b=>b.classList.remove("active"));btn.classList.add("active");document.querySelectorAll(".xlsx-sheet").forEach(s=>{s.style.display=s.dataset.sheet===i?"":"none"})}})</script>';
            }
        } else {
            html += '<div class="error">Could not render this document</div>';
        }
        html += '</div>'; // end doc-content-wrapper

        // Related documents
        if (meta.links && meta.links.length > 0) {
            html += '<div class="doc-links"><h3>Related documents</h3><ul>';
            for (const link of meta.links.slice(0, 10)) {
                html += `<li class="doc-link-item" data-id="${this.esc(link.targetId)}">
                    <span class="link-type">${this.esc(link.linkType)}</span>
                    <span class="link-name">${this.esc(link.targetId)}</span>
                    <span class="link-strength">${(link.strength * 100).toFixed(0)}%</span>
                </li>`;
            }
            html += '</ul></div>';
        }

        html += '</div>'; // end doc-view
        this.el.innerHTML = html;

        // Zoom event listeners
        this.el.querySelectorAll('.zoom-btn').forEach(btn => {
            btn.onclick = () => {
                const val = btn.dataset.zoom;
                if (val === 'reset') this.zoom = 100;
                else this.zoom = Math.max(25, Math.min(200, this.zoom + parseInt(val)));
                const content = this.el.querySelector('.doc-content');
                if (content) content.style.transform = `scale(${this.zoom / 100})`;
                const level = this.el.querySelector('.zoom-level');
                if (level) level.textContent = `${this.zoom}%`;
            };
        });

        // Link click
        this.el.querySelectorAll('.doc-link-item').forEach(el => {
            el.onclick = () => this.show(el.dataset.id);
        });
    }

    esc(s) {
        const d = document.createElement('div');
        d.textContent = s || '';
        return d.innerHTML;
    }

    formatSize(bytes) {
        if (!bytes) return '';
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    formatDate(d) {
        if (!d) return '';
        return new Date(d).toLocaleDateString('fr-FR');
    }
}
