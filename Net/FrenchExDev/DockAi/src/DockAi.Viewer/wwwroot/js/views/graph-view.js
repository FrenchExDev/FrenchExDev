/**
 * GraphView: vis-network entity/document graph.
 * Loaded dynamically on first activation.
 */
export class GraphView {
    constructor(containerEl, api) {
        this.el = containerEl;
        this.api = api;
        this.network = null;
        this.loaded = false;
        this.onDocSelect = null;
    }

    async show(onDocSelect) {
        this.onDocSelect = onDocSelect;
        this.el.style.display = '';

        if (!this.loaded) {
            this.el.innerHTML = '<div class="loading">Loading graph library...</div>';
            await this._loadVisNetwork();
            this.loaded = true;
        }

        this.el.innerHTML = '<div class="graph-filters"></div><div class="graph-canvas" style="width:100%;height:calc(100% - 40px)"></div>';
        await this.buildGraph();
    }

    hide() {
        this.el.style.display = 'none';
    }

    async buildGraph() {
        const ontology = await this.api.getOntology();
        if (!ontology) { this.el.innerHTML = '<div class="error">Could not load ontology</div>'; return; }

        const searchResult = await this.api.search('', 200);
        const docs = searchResult.results || [];

        // Nodes: one per document
        const nodes = docs.map(d => ({
            id: d.id,
            label: d.name.length > 25 ? d.name.substring(0, 25) + '...' : d.name,
            title: `${d.name}\nEntities: ${(d.entities || []).join(', ')}`,
            color: this._catColor(d.category),
            size: 10 + (d.entities?.length || 0) * 3
        }));

        // Edges: from links (shared_entity type)
        const edges = [];
        const edgeSet = new Set();
        for (const doc of docs) {
            if (!doc.links) continue;
            for (const link of doc.links) {
                const key = [doc.id, link.targetId].sort().join('|');
                if (edgeSet.has(key)) continue;
                edgeSet.add(key);
                if (docs.some(d => d.id === link.targetId)) {
                    edges.push({
                        from: doc.id,
                        to: link.targetId,
                        value: link.strength,
                        title: `${link.linkType}: ${(link.via || []).join(', ')}`
                    });
                }
            }
        }

        const canvas = this.el.querySelector('.graph-canvas');
        if (!canvas || typeof vis === 'undefined') return;

        const data = { nodes: new vis.DataSet(nodes), edges: new vis.DataSet(edges) };
        const options = {
            physics: { barnesHut: { gravitationalConstant: -3000, springLength: 150 }, stabilization: { iterations: 100 } },
            interaction: { hover: true, tooltipDelay: 200 },
            edges: { smooth: { type: 'continuous' }, scaling: { min: 1, max: 5 } },
            nodes: { shape: 'dot', font: { size: 11 } }
        };

        this.network = new vis.Network(canvas, data, options);
        this.network.on('click', (params) => {
            if (params.nodes.length > 0 && this.onDocSelect) {
                this.onDocSelect(params.nodes[0]);
            }
        });

        // Entity filter buttons
        const entities = [...new Set(docs.flatMap(d => d.entities || []))].sort();
        const filtersEl = this.el.querySelector('.graph-filters');
        filtersEl.innerHTML = entities.slice(0, 20).map(e =>
            `<button class="graph-filter-btn" data-entity="${e}">${e}</button>`
        ).join('');
        filtersEl.querySelectorAll('.graph-filter-btn').forEach(btn => {
            btn.onclick = () => {
                btn.classList.toggle('active');
                // Filter: show only docs matching active entities
                const active = [...filtersEl.querySelectorAll('.graph-filter-btn.active')].map(b => b.dataset.entity);
                if (active.length === 0) {
                    data.nodes.forEach(n => data.nodes.update({ id: n.id, hidden: false }));
                } else {
                    for (const doc of docs) {
                        const match = active.some(e => (doc.entities || []).includes(e));
                        data.nodes.update({ id: doc.id, hidden: !match });
                    }
                }
            };
        });
    }

    _catColor(cat) {
        const colors = {
            juridique: '#C55A11', technique: '#002060', financier: '#00B050', preuve: '#70AD47',
            synthese: '#FF6B6B', strategie: '#1F4E79', media: '#FFC000', acteurs: '#4ECDC4',
            med_poinat: '#7030A0', medical: '#A6A6A6', personnel: '#E7E6E6', archive: '#595959'
        };
        return colors[cat] || '#888888';
    }

    async _loadVisNetwork() {
        if (typeof vis !== 'undefined') return;
        return new Promise((resolve, reject) => {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://cdn.jsdelivr.net/npm/vis-network@9.1.9/dist/dist/vis-network.min.css';
            document.head.appendChild(link);

            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/vis-network@9.1.9/dist/vis-network.min.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }
}
