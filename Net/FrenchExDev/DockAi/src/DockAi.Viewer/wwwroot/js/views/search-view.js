/**
 * SearchView: DSL search bar with auto-complete and faceted results.
 */
export class SearchView {
    constructor(searchInputEl, facetsEl, api, onResults) {
        this.input = searchInputEl;
        this.facetsEl = facetsEl;
        this.api = api;
        this.onResults = onResults;
        this.suggestEl = null;
        this.debounceTimer = null;

        this.init();
    }

    init() {
        // Create suggestion dropdown
        this.suggestEl = document.createElement('div');
        this.suggestEl.className = 'search-suggest';
        this.input.parentElement.style.position = 'relative';
        this.input.parentElement.appendChild(this.suggestEl);

        // Search on Enter
        this.input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                this.suggestEl.innerHTML = '';
                this.doSearch();
            }
            if (e.key === 'Escape') {
                this.suggestEl.innerHTML = '';
                this.input.value = '';
            }
        });

        // Auto-suggest on input
        this.input.addEventListener('input', () => {
            clearTimeout(this.debounceTimer);
            this.debounceTimer = setTimeout(() => this.suggest(), 300);
        });
    }

    async doSearch() {
        const q = this.input.value.trim();
        const result = await this.api.search(q);
        this.renderFacets(result.facets || {});
        this.onResults(result);
    }

    async suggest() {
        const q = this.input.value.trim();
        if (q.length < 2) { this.suggestEl.innerHTML = ''; return; }

        const suggestions = await this.api.getSuggestions(q);
        if (!suggestions || suggestions.length === 0) {
            this.suggestEl.innerHTML = '';
            return;
        }

        let html = '';
        for (const s of suggestions) {
            html += `<div class="suggest-item" data-value="${this.esc(s.value)}">
                <span class="suggest-type">${this.esc(s.type)}</span>
                <span class="suggest-label">${this.esc(s.label)}</span>
            </div>`;
        }
        this.suggestEl.innerHTML = html;

        this.suggestEl.querySelectorAll('.suggest-item').forEach(el => {
            el.onclick = () => {
                this.input.value = el.dataset.value;
                this.suggestEl.innerHTML = '';
                this.doSearch();
            };
        });
    }

    renderFacets(facets) {
        if (!this.facetsEl) return;
        let html = '';
        for (const [name, values] of Object.entries(facets)) {
            if (!values || values.length === 0) continue;
            html += `<div class="facet-group"><h4 class="facet-title">${this.esc(name)}</h4>`;
            for (const v of values) {
                html += `<span class="facet-chip" data-field="${this.esc(name)}" data-value="${this.esc(v.id)}">
                    ${this.esc(v.label || v.id)} <span class="facet-count">${v.count}</span>
                </span>`;
            }
            html += '</div>';
        }
        this.facetsEl.innerHTML = html;

        // Click facet chip to add to search
        this.facetsEl.querySelectorAll('.facet-chip').forEach(el => {
            el.onclick = () => {
                const current = this.input.value.trim();
                const filter = `${el.dataset.field}:${el.dataset.value}`;
                this.input.value = current ? `${current} ${filter}` : filter;
                this.doSearch();
            };
        });
    }

    esc(s) {
        const d = document.createElement('div');
        d.textContent = s || '';
        return d.innerHTML;
    }
}
