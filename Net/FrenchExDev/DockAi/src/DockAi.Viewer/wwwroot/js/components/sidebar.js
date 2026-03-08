/**
 * Sidebar: document list grouped by category.
 */
export class Sidebar {
    constructor(containerEl, api, onDocSelect) {
        this.el = containerEl;
        this.api = api;
        this.onDocSelect = onDocSelect;
        this.documents = [];
        this.categories = {};
        this.activeDocId = null;
        this.collapsed = new Set();

        this.el.innerHTML = '<div class="sidebar-docs"></div>';
    }

    async load() {
        const result = await this.api.search('', 500);
        const docs = result.results || [];
        this.setDocuments(docs);
    }

    renderResults(docs) {
        this.setDocuments(docs);
    }

    setDocuments(docs) {
        this.documents = docs;
        this.categories = {};
        for (const doc of docs) {
            const cat = doc.category || 'other';
            if (!this.categories[cat]) this.categories[cat] = [];
            this.categories[cat].push(doc);
        }
        this.render();
    }

    setActive(docId) {
        this.activeDocId = docId;
        this.el.querySelectorAll('.sidebar-doc').forEach(el => {
            el.classList.toggle('active', el.dataset.id === docId);
        });
    }

    render() {
        const cats = Object.keys(this.categories).sort();
        let html = '';
        for (const cat of cats) {
            const docs = this.categories[cat];
            const isCollapsed = this.collapsed.has(cat);
            html += `<div class="sidebar-group">
                <div class="sidebar-group-header" data-cat="${this.escHtml(cat)}">
                    <span class="arrow${isCollapsed ? ' collapsed' : ''}">\u25bc</span>
                    <span>${this.escHtml(cat)}</span>
                    <span class="group-count">${docs.length}</span>
                </div>
                <div class="sidebar-items${isCollapsed ? ' collapsed' : ''}">`;
            for (const doc of docs) {
                const active = doc.id === this.activeDocId ? ' active' : '';
                const icon = this.typeIcon(doc.fileType);
                html += `<div class="sidebar-item${active}" data-id="${this.escHtml(doc.id)}" data-name="${this.escHtml(doc.name)}" title="${this.escHtml(doc.name)}">
                    <span class="file-icon">${icon}</span>
                    <span class="file-name">${this.escHtml(doc.name)}</span>
                </div>`;
            }
            html += `</div></div>`;
        }

        const container = this.el.querySelector('.sidebar-docs');
        if (container) container.innerHTML = html;

        // Events
        this.el.querySelectorAll('.sidebar-group-header').forEach(el => {
            el.onclick = () => {
                const cat = el.dataset.cat;
                if (this.collapsed.has(cat)) this.collapsed.delete(cat); else this.collapsed.add(cat);
                this.render();
            };
        });
        this.el.querySelectorAll('.sidebar-item').forEach(el => {
            el.onclick = () => this.onDocSelect(el.dataset.id, el.dataset.name);
        });
    }

    typeIcon(type) {
        const icons = { docx: '\ud83d\udcc4', pdf: '\ud83d\udcc5', xlsx: '\ud83d\udcca', csv: '\ud83d\udcca', md: '\ud83d\udcdd', html: '\ud83c\udf10', txt: '\ud83d\udcc3' };
        return icons[type] || '\ud83d\udcc1';
    }

    escHtml(s) {
        const d = document.createElement('div');
        d.textContent = s || '';
        return d.innerHTML;
    }
}
