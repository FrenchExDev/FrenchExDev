/**
 * HTTP client for DockAi.Viewer — talks to proxy endpoints and rendering endpoints.
 */
export class ApiClient {
    constructor(baseUrl = '') {
        this.baseUrl = baseUrl;
    }

    async search(query, max = 50) {
        const res = await fetch(`${this.baseUrl}/proxy/search?q=${encodeURIComponent(query)}&max=${max}`);
        return res.ok ? res.json() : { results: [], totalHits: 0, facets: {} };
    }

    async getDocument(id) {
        const res = await fetch(`${this.baseUrl}/proxy/document/${encodeURIComponent(id)}`);
        return res.ok ? res.json() : null;
    }

    async renderDocument(id) {
        const res = await fetch(`${this.baseUrl}/render/${encodeURIComponent(id)}`);
        return res.ok ? res.json() : null;
    }

    async getLinks(id) {
        const res = await fetch(`${this.baseUrl}/proxy/links/${encodeURIComponent(id)}`);
        return res.ok ? res.json() : [];
    }

    async getOntology() {
        const res = await fetch(`${this.baseUrl}/proxy/ontology`);
        return res.ok ? res.json() : null;
    }

    async getTaxonomy(name) {
        const res = await fetch(`${this.baseUrl}/proxy/taxonomy/${encodeURIComponent(name)}`);
        return res.ok ? res.json() : null;
    }

    async getSuggestions(prefix) {
        if (!prefix || prefix.length < 2) return [];
        const res = await fetch(`${this.baseUrl}/proxy/suggest?q=${encodeURIComponent(prefix)}`);
        return res.ok ? res.json() : [];
    }

    async triggerIndex() {
        const res = await fetch(`${this.baseUrl}/proxy/index`, { method: 'POST' });
        return res.ok ? res.json() : null;
    }

    getRawUrl(id) {
        return `${this.baseUrl}/raw/${encodeURIComponent(id)}`;
    }
}
